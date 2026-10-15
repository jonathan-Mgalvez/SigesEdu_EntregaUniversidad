-- ============================================================================
-- SCRIPT DDL: BASE DE DATOS SIGESEDU (SQLITE 3)
-- DIRECTIVAS OBLIGATORIAS DE OPERACIÓN
-- ============================================================================
PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA busy_timeout = 5000;

-- ----------------------------------------------------------------------------
-- TABLA: Usuarios (Actualizada con control Anti-Fuerza Bruta)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Usuarios (
    UsuarioId INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash TEXT NOT NULL,
    Salt TEXT NOT NULL,
    Rol TEXT NOT NULL CHECK(Rol IN ('Administrador', 'Coordinador', 'Tesorero', 'Docente', 'Padre', 'Estudiante')),
    Activo INTEGER NOT NULL DEFAULT 1 CHECK(Activo IN (0, 1)),
    IntentosFallidos INTEGER NOT NULL DEFAULT 0 CHECK(IntentosFallidos >= 0),
    BloqueadoHasta TEXT,
    FechaCreacion TEXT NOT NULL DEFAULT (DATETIME('now')),
    UltimoAcceso TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_usuarios_username_unique ON Usuarios(Username);
CREATE INDEX IF NOT EXISTS idx_usuarios_rol ON Usuarios(Rol);

-- ----------------------------------------------------------------------------
-- TABLA: PadresDeFamilia
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS PadresDeFamilia (
    PadreId INTEGER PRIMARY KEY AUTOINCREMENT,
    UsuarioId INTEGER NOT NULL UNIQUE,
    DocumentoIdentidad TEXT NOT NULL UNIQUE,
    Nombres TEXT NOT NULL,
    Apellidos TEXT NOT NULL,
    Telefono TEXT NOT NULL,
    Email TEXT NOT NULL,
    Direccion TEXT NOT NULL,
    FOREIGN KEY (UsuarioId) REFERENCES Usuarios(UsuarioId) ON DELETE RESTRICT
);

-- ----------------------------------------------------------------------------
-- TABLA: Estudiantes
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Estudiantes (
    EstudianteId INTEGER PRIMARY KEY AUTOINCREMENT,
    PadreId INTEGER NOT NULL,
    CodigoCarnet TEXT UNIQUE,
    DocumentoIdentidad TEXT NOT NULL UNIQUE,
    Nombres TEXT NOT NULL,
    Apellidos TEXT NOT NULL,
    FechaNacimiento TEXT NOT NULL,
    EstadoAcademico TEXT NOT NULL DEFAULT 'Aspirante' CHECK(EstadoAcademico IN ('Aspirante','Activo','Suspendido','Retirado','Graduado')),
    FOREIGN KEY (PadreId) REFERENCES PadresDeFamilia(PadreId) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS idx_estudiantes_carnet ON Estudiantes(CodigoCarnet);

-- ----------------------------------------------------------------------------
-- TABLA: AniosLectivos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS AniosLectivos (
    AnioLectivoId INTEGER PRIMARY KEY AUTOINCREMENT,
    AnioCalendario INTEGER NOT NULL UNIQUE,
    FechaInicio TEXT NOT NULL,
    FechaFin TEXT NOT NULL,
    EsActivo INTEGER NOT NULL DEFAULT 0 CHECK(EsActivo IN (0, 1))
);

-- ----------------------------------------------------------------------------
-- TABLA: Grados
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Grados (
    GradoId INTEGER PRIMARY KEY AUTOINCREMENT,
    AnioLectivoId INTEGER NOT NULL,
    Nivel TEXT NOT NULL CHECK(Nivel IN ('Preprimaria','Primaria','Basico')),
    NombreGrado TEXT NOT NULL,
    FOREIGN KEY (AnioLectivoId) REFERENCES AniosLectivos(AnioLectivoId) ON DELETE RESTRICT
);

-- ----------------------------------------------------------------------------
-- TABLA: Secciones
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Secciones (
    SeccionId INTEGER PRIMARY KEY AUTOINCREMENT,
    GradoId INTEGER NOT NULL,
    NombreSeccion TEXT NOT NULL,
    CapacidadMaxima INTEGER NOT NULL CHECK(CapacidadMaxima > 0),
    CuposOcupados INTEGER NOT NULL DEFAULT 0 CHECK(CuposOcupados >= 0 AND CuposOcupados <= CapacidadMaxima),
    FOREIGN KEY (GradoId) REFERENCES Grados(GradoId) ON DELETE RESTRICT,
    UNIQUE(GradoId, NombreSeccion)
);

-- ----------------------------------------------------------------------------
-- TABLA: SolicitudesMatricula
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SolicitudesMatricula (
    SolicitudId INTEGER PRIMARY KEY AUTOINCREMENT,
    EstudianteId INTEGER NOT NULL,
    SeccionId INTEGER NOT NULL,
    CodigoSeguimiento TEXT NOT NULL UNIQUE,
    EstadoMatricula TEXT NOT NULL CHECK(EstadoMatricula IN (
        'PendienteDocumentos',
        'DocumentosCargados',
        'DocumentosObservados',
        'PreMatriculado',
        'CaducadaPorFaltaDePago',
        'PagoVerificado',
        'MatriculaActiva',
        'Cancelada'
    )),
    FechaSolicitud TEXT NOT NULL DEFAULT (DATETIME('now')),
    Observaciones TEXT,
    FOREIGN KEY (EstudianteId) REFERENCES Estudiantes(EstudianteId) ON DELETE RESTRICT,
    FOREIGN KEY (SeccionId) REFERENCES Secciones(SeccionId) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS idx_solicitudes_estado ON SolicitudesMatricula(EstadoMatricula);

-- ----------------------------------------------------------------------------
-- TABLA: Cupos 
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Cupos (
    CupoId INTEGER PRIMARY KEY AUTOINCREMENT,
    SeccionId INTEGER NOT NULL,
    SolicitudId INTEGER UNIQUE,
    NumeroCupo INTEGER NOT NULL,
    EstadoCupo TEXT NOT NULL CHECK(EstadoCupo IN ('Disponible', 'Reservado', 'Ocupado')),
    BloqueoHasta TEXT,
    FOREIGN KEY (SeccionId) REFERENCES Secciones(SeccionId) ON DELETE RESTRICT,
    FOREIGN KEY (SolicitudId) REFERENCES SolicitudesMatricula(SolicitudId) ON DELETE SET NULL,
    UNIQUE(SeccionId, NumeroCupo)
);
CREATE INDEX IF NOT EXISTS idx_cupos_seccion_estado ON Cupos(SeccionId, EstadoCupo);

-- ----------------------------------------------------------------------------
-- TABLA: LegajosDocumento
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS LegajosDocumento (
    DocumentoId INTEGER PRIMARY KEY AUTOINCREMENT,
    SolicitudId INTEGER NOT NULL,
    TipoDocumento TEXT NOT NULL CHECK(TipoDocumento IN ('CertificadoNacimiento','ConstanciaEstudios','SolvenciaFinanciera','FichaMedica')),
    RutaArchivo TEXT NOT NULL,
    EstadoRevision TEXT NOT NULL DEFAULT 'Pendiente' CHECK(EstadoRevision IN ('Pendiente','Aprobado','Observado')),
    MotivoObservacion TEXT,
    FechaCarga TEXT NOT NULL DEFAULT (DATETIME('now')),
    FOREIGN KEY (SolicitudId) REFERENCES SolicitudesMatricula(SolicitudId) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- TABLA: ComprobantesPago
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ComprobantesPago (
    ComprobanteId INTEGER PRIMARY KEY AUTOINCREMENT,
    SolicitudId INTEGER NOT NULL,
    NumeroBoleta TEXT NOT NULL UNIQUE,
    Monto REAL NOT NULL CHECK(Monto > 0),
    FechaEmision TEXT NOT NULL,
    RutaComprobante TEXT NOT NULL,
    EstadoPago TEXT NOT NULL DEFAULT 'En Revision' CHECK(EstadoPago IN ('En Revision','Verificado','Rechazado')),
    MotivoRechazo TEXT,
    FechaValidacion TEXT,
    TesoreroId INTEGER,
    FOREIGN KEY (SolicitudId) REFERENCES SolicitudesMatricula(SolicitudId) ON DELETE RESTRICT,
    FOREIGN KEY (TesoreroId) REFERENCES Usuarios(UsuarioId) ON DELETE RESTRICT
);

-- ----------------------------------------------------------------------------
-- TABLA: Cursos
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Cursos (
    CursoId INTEGER PRIMARY KEY AUTOINCREMENT,
    SeccionId INTEGER NOT NULL,
    DocenteId INTEGER NOT NULL,
    NombreCurso TEXT NOT NULL,
    PonderacionTotal REAL NOT NULL DEFAULT 100.0,
    FOREIGN KEY (SeccionId) REFERENCES Secciones(SeccionId) ON DELETE RESTRICT,
    FOREIGN KEY (DocenteId) REFERENCES Usuarios(UsuarioId) ON DELETE RESTRICT
);

-- ----------------------------------------------------------------------------
-- TABLA: ActasCalificaciones 
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ActasCalificaciones (
    ActaId INTEGER PRIMARY KEY AUTOINCREMENT,
    CursoId INTEGER NOT NULL,
    Periodo TEXT NOT NULL CHECK(Periodo IN ('Bimestre 1','Bimestre 2','Bimestre 3','Bimestre 4','Final')),
    EsCerrada INTEGER NOT NULL DEFAULT 0 CHECK(EsCerrada IN (0, 1)),
    FechaCierre TEXT,
    CoordinadorId INTEGER,
    FirmaHash TEXT,
    FOREIGN KEY (CursoId) REFERENCES Cursos(CursoId) ON DELETE RESTRICT,
    FOREIGN KEY (CoordinadorId) REFERENCES Usuarios(UsuarioId) ON DELETE RESTRICT,
    UNIQUE(CursoId, Periodo)
);
CREATE INDEX IF NOT EXISTS idx_actas_cierre ON ActasCalificaciones(EsCerrada);

-- ----------------------------------------------------------------------------
-- TABLA: Calificaciones 
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Calificaciones (
    CalificacionId INTEGER PRIMARY KEY AUTOINCREMENT,
    ActaId INTEGER NOT NULL,
    EstudianteId INTEGER NOT NULL,
    NotaParcial REAL NOT NULL CHECK(NotaParcial >= 0.0 AND NotaParcial <= 100.0),
    NotaFinal REAL NOT NULL CHECK(NotaFinal >= 0.0 AND NotaFinal <= 100.0),
    EsAprobado INTEGER NOT NULL CHECK(EsAprobado IN (0, 1)),
    FOREIGN KEY (ActaId) REFERENCES ActasCalificaciones(ActaId) ON DELETE RESTRICT,
    FOREIGN KEY (EstudianteId) REFERENCES Estudiantes(EstudianteId) ON DELETE RESTRICT,
    UNIQUE(ActaId, EstudianteId)
);

-- ----------------------------------------------------------------------------
-- TABLA: AuditoriaEventos (Actualizada a Eventos de Aplicación)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS AuditoriaEventos (
    AuditoriaId INTEGER PRIMARY KEY AUTOINCREMENT,
    UsuarioId INTEGER,
    Accion TEXT NOT NULL,
    Modulo TEXT NOT NULL,
    Detalle TEXT NOT NULL,
    DireccionIP TEXT,
    FechaHora TEXT NOT NULL DEFAULT (DATETIME('now')),
    FOREIGN KEY (UsuarioId) REFERENCES Usuarios(UsuarioId) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_auditoria_usuario_fecha ON AuditoriaEventos(UsuarioId, FechaHora);
-- ----------------------------------------------------------------------------
-- TABLA: Asistencias (Control diario RF-15)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Asistencias (
    AsistenciaId INTEGER PRIMARY KEY AUTOINCREMENT,
    CursoId INTEGER NOT NULL,
    EstudianteId INTEGER NOT NULL,
    Fecha TEXT NOT NULL,
    Estado TEXT NOT NULL CHECK(Estado IN ('Presente', 'Ausente', 'Tardanza', 'Justificado')),
    Observacion TEXT,
    FOREIGN KEY (CursoId) REFERENCES Cursos(CursoId) ON DELETE RESTRICT,
    FOREIGN KEY (EstudianteId) REFERENCES Estudiantes(EstudianteId) ON DELETE RESTRICT,
    UNIQUE(CursoId, EstudianteId, Fecha)
);
CREATE INDEX IF NOT EXISTS idx_asistencias_curso_fecha ON Asistencias(CursoId, Fecha);

-- ----------------------------------------------------------------------------
-- TABLA: RecibosCaja
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS RecibosCaja (
    ReciboId INTEGER PRIMARY KEY AUTOINCREMENT,
    ComprobanteId INTEGER NOT NULL UNIQUE,
    NumeroRecibo TEXT NOT NULL UNIQUE,
    MontoTotal REAL NOT NULL CHECK(MontoTotal > 0),
    FechaEmision TEXT NOT NULL DEFAULT (DATETIME('now')),
    FOREIGN KEY (ComprobanteId) REFERENCES ComprobantesPago(ComprobanteId) ON DELETE RESTRICT
);

-- ----------------------------------------------------------------------------
-- TRIGGERS DE INMUTABILIDAD FÍSICA (RN-05)
-- ----------------------------------------------------------------------------
CREATE TRIGGER IF NOT EXISTS trg_bloqueo_calificaciones_acta_cerrada
BEFORE UPDATE ON Calificaciones
FOR EACH ROW
WHEN (SELECT EsCerrada FROM ActasCalificaciones WHERE ActaId = OLD.ActaId) = 1
BEGIN
    SELECT RAISE(ABORT, 'Violación RN-05: No se permite modificar calificaciones de un acta cerrada e inmutable.');
END;

CREATE TRIGGER IF NOT EXISTS trg_bloqueo_eliminar_calificaciones_acta_cerrada
BEFORE DELETE ON Calificaciones
FOR EACH ROW
WHEN (SELECT EsCerrada FROM ActasCalificaciones WHERE ActaId = OLD.ActaId) = 1
BEGIN
    SELECT RAISE(ABORT, 'Violación RN-05: No se permite eliminar registros de un acta cerrada.');
END;
-- ----------------------------------------------------------------------------
-- TRIGGERS DE INMUTABILIDAD (RN-05 / Cierre de Actas)
-- ----------------------------------------------------------------------------
CREATE TRIGGER IF NOT EXISTS trg_bloqueo_calificaciones_acta_cerrada
BEFORE UPDATE ON Calificaciones
FOR EACH ROW
WHEN (SELECT EsCerrada FROM ActasCalificaciones WHERE ActaId = OLD.ActaId) = 1
BEGIN
    SELECT RAISE(ABORT, 'Violación RN-05: No se permite modificar calificaciones de un acta cerrada e inmutable.');
END;

CREATE TRIGGER IF NOT EXISTS trg_bloqueo_eliminar_calificaciones_acta_cerrada
BEFORE DELETE ON Calificaciones
FOR EACH ROW
WHEN (SELECT EsCerrada FROM ActasCalificaciones WHERE ActaId = OLD.ActaId) = 1
BEGIN
    SELECT RAISE(ABORT, 'Violación RN-05: No se permite eliminar registros de un acta cerrada.');
END;

-- ----------------------------------------------------------------------------
-- VISTAS ANALÍTICAS PARA REPORTERÍA (RF-20)
-- ----------------------------------------------------------------------------

CREATE VIEW IF NOT EXISTS vw_ActaCalificacionesConsolidada AS
SELECT 
    a.ActaId, c.CursoId, c.NombreCurso, s.SeccionId, g.NombreGrado, s.NombreSeccion,
    al.AnioCalendario, a.Periodo, a.EsCerrada, a.FechaCierre, a.FirmaHash,
    e.EstudianteId, e.CodigoCarnet, e.Apellidos || ', ' || e.Nombres AS NombreCompletoEstudiante,
    cal.NotaParcial, cal.NotaFinal,
    CASE WHEN cal.EsAprobado = 1 THEN 'APROBADO' ELSE 'REPROBADO' END AS EstadoCalificacion
FROM ActasCalificaciones a
INNER JOIN Cursos c ON a.CursoId = c.CursoId
INNER JOIN Secciones s ON c.SeccionId = s.SeccionId
INNER JOIN Grados g ON s.GradoId = g.GradoId
INNER JOIN AniosLectivos al ON g.AnioLectivoId = al.AnioLectivoId
INNER JOIN Calificaciones cal ON a.ActaId = cal.ActaId
INNER JOIN Estudiantes e ON cal.EstudianteId = e.EstudianteId;

CREATE VIEW IF NOT EXISTS vw_NominaMatriculaMinisterial AS
SELECT 
    al.AnioCalendario AS CicloLectivo, g.Nivel, g.NombreGrado, s.NombreSeccion,
    s.CapacidadMaxima, s.CuposOcupados, e.CodigoCarnet, e.DocumentoIdentidad AS DocumentoEstudiante,
    e.Apellidos AS ApellidosEstudiante, e.Nombres AS NombresEstudiante, e.FechaNacimiento,
    p.DocumentoIdentidad AS DocumentoPadre, p.Nombres || ' ' || p.Apellidos AS NombreTutorLegal,
    p.Telefono AS TelefonoContacto, sm.FechaSolicitud AS FechaInscripcion, sm.EstadoMatricula
FROM SolicitudesMatricula sm
INNER JOIN Estudiantes e ON sm.EstudianteId = e.EstudianteId
INNER JOIN PadresDeFamilia p ON e.PadreId = p.PadreId
INNER JOIN Secciones s ON sm.SeccionId = s.SeccionId
INNER JOIN Grados g ON s.GradoId = g.GradoId
INNER JOIN AniosLectivos al ON g.AnioLectivoId = al.AnioLectivoId
WHERE sm.EstadoMatricula = 'MatriculaActiva';

CREATE VIEW IF NOT EXISTS vw_EstadoCuentaEscolar AS
SELECT 
    e.EstudianteId, e.CodigoCarnet, e.Apellidos || ', ' || e.Nombres AS Estudiante,
    p.Nombres || ' ' || p.Apellidos AS TutorResponsable, s.NombreSeccion, g.NombreGrado,
    IFNULL(SUM(rc.MontoTotal), 0.0) AS TotalPagado,
    850.00 AS ArancelRequerido,
    (850.00 - IFNULL(SUM(rc.MontoTotal), 0.0)) AS SaldoPendiente,
    CASE WHEN (850.00 - IFNULL(SUM(rc.MontoTotal), 0.0)) <= 0 THEN 'SOLVENTE' ELSE 'PENDIENTE' END AS CondicionArancelaria
FROM Estudiantes e
INNER JOIN PadresDeFamilia p ON e.PadreId = p.PadreId
INNER JOIN SolicitudesMatricula sm ON e.EstudianteId = sm.EstudianteId AND sm.EstadoMatricula = 'MatriculaActiva'
INNER JOIN Secciones s ON sm.SeccionId = s.SeccionId
INNER JOIN Grados g ON s.GradoId = g.GradoId
LEFT JOIN ComprobantesPago cp ON sm.SolicitudId = cp.SolicitudId AND cp.EstadoPago = 'Verificado'
LEFT JOIN RecibosCaja rc ON cp.ComprobanteId = rc.ComprobanteId
GROUP BY e.EstudianteId, e.CodigoCarnet, e.Apellidos, e.Nombres, p.Nombres, p.Apellidos, s.NombreSeccion, g.NombreGrado;

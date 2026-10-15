using System;
using System.Data;
using System.IO;
using SigesEdu.Domain.Interfaces;
using SigesEdu.Application.Services;

namespace SigesEdu.Infrastructure.Persistence;

public class DbInitializer : IDbInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPasswordHasher _passwordHasher;

    // Inyectamos IPasswordHasher para poder encriptar las cuentas semilla
    public DbInitializer(IDbConnectionFactory connectionFactory, IPasswordHasher passwordHasher)
    {
        _connectionFactory = connectionFactory;
        _passwordHasher = passwordHasher;
    }

    public void Initialize()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // 1. Ejecutar el script DDL de creación de tablas e índices
        var sqlScriptPath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Migrations", "001_InitialSchema.sql");
        
        // Alternativa de ruta si se corre directamente desde desarrollo
        if (!File.Exists(sqlScriptPath))
        {
            sqlScriptPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "SigesEdu.Infrastructure", "Persistence", "Migrations", "001_InitialSchema.sql");
        }

        if (!File.Exists(sqlScriptPath))
        {
            throw new FileNotFoundException($"No se localizó el script DDL en: {sqlScriptPath}");
        }

        var ddlSql = File.ReadAllText(sqlScriptPath);

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ddlSql;
            cmd.ExecuteNonQuery();
        }

        // 2. Verificar formalmente la creación de las tablas
        VerificarTablas(connection);

        // 3. Inyectar datos semilla para iniciar sesión
        InyectarUsuariosPrueba(connection);

        // 4. Inyectar catálogo académico (Grados y Secciones)
        InyectarEstructuraAcademica(connection);
    }

    private void VerificarTablas(IDbConnection connection)
    {
        var tablasEsperadas = new[]
        {
            "Usuarios", "PadresDeFamilia", "Estudiantes", "AniosLectivos",
            "Grados", "Secciones", "SolicitudesMatricula", "Cupos",
            "LegajosDocumento", "ComprobantesPago", "Cursos",
            "ActasCalificaciones", "Calificaciones", "AuditoriaEventos",
            "Asistencias", "RecibosCaja"
        };

        foreach (var tabla in tablasEsperadas)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@nombre;";
            
            var param = cmd.CreateParameter();
            param.ParameterName = "@nombre";
            param.Value = tabla;
            cmd.Parameters.Add(param);

            var count = Convert.ToInt32(cmd.ExecuteScalar());
            if (count == 0)
            {
                throw new InvalidOperationException($"La tabla obligatoria '{tabla}' no fue creada en la base de datos.");
            }
        }
    }

    private void InyectarUsuariosPrueba(IDbConnection connection)
    {
        using var cmdCheck = connection.CreateCommand();
        cmdCheck.CommandText = "SELECT COUNT(*) FROM Usuarios";
        var count = Convert.ToInt32(cmdCheck.ExecuteScalar());

        // Solo insertamos si la base de datos está completamente vacía
        if (count == 0)
        {
            var usuariosSeed = new[]
            {
                new { Username = "padre1", Rol = "Padre" },
                new { Username = "tesoreria", Rol = "Tesorero" },
                new { Username = "coordinacion", Rol = "Coordinador" },
                new { Username = "admin", Rol = "Administrador" },
                new { Username = "docente1", Rol = "Docente" },
                new { Username = "estudiante1", Rol = "Estudiante" }
            };

            // Contraseña universal para las pruebas de desarrollo
            string passwordPrueba = "123456";

            foreach (var user in usuariosSeed)
            {
                // Generamos el Hash y la Sal usando la criptografía real del sistema
                var (hash, salt) = _passwordHasher.GenerarHash(passwordPrueba);

                using var cmdInsert = connection.CreateCommand();
                cmdInsert.CommandText = @"
                    INSERT INTO Usuarios (Username, PasswordHash, Salt, Rol, Activo, IntentosFallidos, FechaCreacion)
                    VALUES (@username, @hash, @salt, @rol, 1, 0, DATETIME('now'))";

                var pUser = cmdInsert.CreateParameter(); pUser.ParameterName = "@username"; pUser.Value = user.Username; cmdInsert.Parameters.Add(pUser);
                var pHash = cmdInsert.CreateParameter(); pHash.ParameterName = "@hash"; pHash.Value = hash; cmdInsert.Parameters.Add(pHash);
                var pSalt = cmdInsert.CreateParameter(); pSalt.ParameterName = "@salt"; pSalt.Value = salt; cmdInsert.Parameters.Add(pSalt);
                var pRol = cmdInsert.CreateParameter(); pRol.ParameterName = "@rol"; pRol.Value = user.Rol; cmdInsert.Parameters.Add(pRol);

                cmdInsert.ExecuteNonQuery();
            }
        }
    }
    private void InyectarEstructuraAcademica(IDbConnection connection)
    {
        using var cmdCheck = connection.CreateCommand();
        cmdCheck.CommandText = "SELECT COUNT(*) FROM Grados";
        var count = Convert.ToInt32(cmdCheck.ExecuteScalar());

        if (count == 0)
        {
            using var cmdInsert = connection.CreateCommand();
            cmdInsert.CommandText = @"
                -- 1. Crear Año Lectivo activo
                INSERT INTO AniosLectivos (AnioCalendario, FechaInicio, FechaFin, EsActivo) 
                VALUES (2026, '2026-01-15', '2026-10-31', 1);

                -- 2. Insertar Grados
                INSERT INTO Grados (AnioLectivoId, Nivel, NombreGrado) VALUES 
                (1, 'Preprimaria', 'Párvulos'),
                (1, 'Primaria', 'Primero Primaria'),
                (1, 'Primaria', 'Segundo Primaria'),
                (1, 'Primaria', 'Tercero Primaria'),
                (1, 'Primaria', 'Cuarto Primaria'),
                (1, 'Primaria', 'Quinto Primaria'),
                (1, 'Primaria', 'Sexto Primaria'),
                (1, 'Basico', 'Primero Básico'),
                (1, 'Basico', 'Segundo Básico'),
                (1, 'Basico', 'Tercero Básico');

                -- 3. Insertar Secciones (A y B para todos los grados, con 30 cupos)
                INSERT INTO Secciones (GradoId, NombreSeccion, CapacidadMaxima, CuposOcupados)
                SELECT GradoId, 'A', 30, 0 FROM Grados;
                
                INSERT INTO Secciones (GradoId, NombreSeccion, CapacidadMaxima, CuposOcupados)
                SELECT GradoId, 'B', 30, 0 FROM Grados;
            ";
            cmdInsert.ExecuteNonQuery();
        }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SigesEdu.Application.Services;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.UI.Controllers;

public class MatriculaController : Controller
{
    private readonly IMatriculaAppService _matriculaService;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly string _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "LegajosDigitales");

    public MatriculaController(IMatriculaAppService matriculaService, IDbConnectionFactory connectionFactory)
    {
        _matriculaService = matriculaService;
        _connectionFactory = connectionFactory;
    }

    // GET: /Matricula/Solicitud
    [HttpGet]
    public IActionResult Solicitud()
    {
        var rol = HttpContext.Session.GetString("UsuarioRol");
        if (rol != "Padre")
        {
            return RedirectToAction("Login", "Auth");
        }

        return View();
    }

    // GET: /Matricula/ObtenerGradosPorNivel
    [HttpGet]
    public IActionResult ObtenerGradosPorNivel(string nivel)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT GradoId, NombreGrado FROM Grados WHERE Nivel = @nivel ORDER BY GradoId";
        
        var param = cmd.CreateParameter();
        param.ParameterName = "@nivel";
        param.Value = nivel;
        cmd.Parameters.Add(param);

        var grados = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            grados.Add(new { 
                gradoId = Convert.ToInt32(reader["GradoId"]), 
                nombreGrado = reader["NombreGrado"].ToString() 
            });
        }
        return Json(grados);
    }

    // GET: /Matricula/ObtenerSeccionesPorGrado
    [HttpGet]
    public IActionResult ObtenerSeccionesPorGrado(int gradoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT SeccionId, NombreSeccion, CapacidadMaxima, CuposOcupados FROM Secciones WHERE GradoId = @gradoId ORDER BY NombreSeccion";
        
        var param = cmd.CreateParameter();
        param.ParameterName = "@gradoId";
        param.Value = gradoId;
        cmd.Parameters.Add(param);

        var secciones = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            secciones.Add(new { 
                seccionId = Convert.ToInt32(reader["SeccionId"]), 
                nombreSeccion = reader["NombreSeccion"].ToString(),
                capacidadMaxima = Convert.ToInt32(reader["CapacidadMaxima"]),
                cuposOcupados = Convert.ToInt32(reader["CuposOcupados"])
            });
        }
        return Json(secciones);
    }

    // POST: /Matricula/CargarLegajo
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CargarLegajo(
        string documentoIdentidad, 
        string nombres, 
        string apellidos, 
        string fechaNacimiento, 
        int seccionId,
        IFormFile certificadoNacimiento, 
        IFormFile constanciaEstudios, 
        IFormFile solvenciaFinanciera)
    {
        try
        {
            if (certificadoNacimiento == null || constanciaEstudios == null || solvenciaFinanciera == null || seccionId == 0)
            {
                ViewBag.Error = "Faltan datos o documentos obligatorios.";
                return View("Solicitud");
            }

            long maxBytes = 5 * 1024 * 1024;
            if (certificadoNacimiento.Length > maxBytes || constanciaEstudios.Length > maxBytes || solvenciaFinanciera.Length > maxBytes)
            {
                ViewBag.Error = "Uno o más archivos superan el límite de 5MB.";
                return View("Solicitud");
            }

            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }

            // 1. Guardar archivos físicamente y obtener las rutas generadas
            string rutaCertificado = await GuardarArchivoFisicoAsync(certificadoNacimiento);
            string rutaConstancia = await GuardarArchivoFisicoAsync(constanciaEstudios);
            string rutaSolvencia = await GuardarArchivoFisicoAsync(solvenciaFinanciera);

            // =======================================================
            // 2. INICIO DE TRANSACCIÓN RELACIONAL HACIA SQLITE
            // =======================================================
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                int usuarioId = HttpContext.Session.GetInt32("UsuarioId") ?? 1;

                // A. Crear registro vinculante de PadreDeFamilia (si es que no existe)
                using var cmdPadre = connection.CreateCommand();
                cmdPadre.Transaction = transaction;
                cmdPadre.CommandText = @"
                    INSERT OR IGNORE INTO PadresDeFamilia (UsuarioId, DocumentoIdentidad, Nombres, Apellidos, Telefono, Email, Direccion) 
                    VALUES (@uid, '0000000000000', 'Padre', 'Prueba', '00000000', 'padre@demo.com', 'N/A');
                    SELECT PadreId FROM PadresDeFamilia WHERE UsuarioId = @uid;";
                var pUid = cmdPadre.CreateParameter(); pUid.ParameterName = "@uid"; pUid.Value = usuarioId; cmdPadre.Parameters.Add(pUid);
                int padreId = Convert.ToInt32(cmdPadre.ExecuteScalar());

                // B. Insertar Estudiante
                using var cmdEstudiante = connection.CreateCommand();
                cmdEstudiante.Transaction = transaction;
                cmdEstudiante.CommandText = @"
                    INSERT INTO Estudiantes (PadreId, DocumentoIdentidad, Nombres, Apellidos, FechaNacimiento) 
                    VALUES (@padre, @cui, @nom, @ape, @fec);
                    SELECT last_insert_rowid();";
                var pPadre = cmdEstudiante.CreateParameter(); pPadre.ParameterName = "@padre"; pPadre.Value = padreId; cmdEstudiante.Parameters.Add(pPadre);
                var pCui = cmdEstudiante.CreateParameter(); pCui.ParameterName = "@cui"; pCui.Value = documentoIdentidad; cmdEstudiante.Parameters.Add(pCui);
                var pNom = cmdEstudiante.CreateParameter(); pNom.ParameterName = "@nom"; pNom.Value = nombres; cmdEstudiante.Parameters.Add(pNom);
                var pApe = cmdEstudiante.CreateParameter(); pApe.ParameterName = "@ape"; pApe.Value = apellidos; cmdEstudiante.Parameters.Add(pApe);
                var pFec = cmdEstudiante.CreateParameter(); pFec.ParameterName = "@fec"; pFec.Value = fechaNacimiento; cmdEstudiante.Parameters.Add(pFec);
                int estudianteId = Convert.ToInt32(cmdEstudiante.ExecuteScalar());

                // C. Actualizar Cupos en la Sección (Restricción física de concurrencia)
                using var cmdCupo = connection.CreateCommand();
                cmdCupo.Transaction = transaction;
                cmdCupo.CommandText = @"
                    UPDATE Secciones 
                    SET CuposOcupados = CuposOcupados + 1 
                    WHERE SeccionId = @sec AND CuposOcupados < CapacidadMaxima;
                    SELECT changes();";
                var pSec = cmdCupo.CreateParameter(); pSec.ParameterName = "@sec"; pSec.Value = seccionId; cmdCupo.Parameters.Add(pSec);
                int filasActualizadas = Convert.ToInt32(cmdCupo.ExecuteScalar());

                if (filasActualizadas == 0)
                {
                    throw new Exception("El cupo en esta sección ya ha sido ocupado. Por favor, seleccione otra.");
                }

                // D. Generar Solicitud de Matrícula
                string codSeguimiento = "SOL-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                using var cmdSolicitud = connection.CreateCommand();
                cmdSolicitud.Transaction = transaction;
                cmdSolicitud.CommandText = @"
                    INSERT INTO SolicitudesMatricula (EstudianteId, SeccionId, CodigoSeguimiento, EstadoMatricula) 
                    VALUES (@estId, @secId, @cod, 'DocumentosCargados');
                    SELECT last_insert_rowid();";
                var pEstId = cmdSolicitud.CreateParameter(); pEstId.ParameterName = "@estId"; pEstId.Value = estudianteId; cmdSolicitud.Parameters.Add(pEstId);
                var pSecId = cmdSolicitud.CreateParameter(); pSecId.ParameterName = "@secId"; pSecId.Value = seccionId; cmdSolicitud.Parameters.Add(pSecId);
                var pCod = cmdSolicitud.CreateParameter(); pCod.ParameterName = "@cod"; pCod.Value = codSeguimiento; cmdSolicitud.Parameters.Add(pCod);
                int solicitudId = Convert.ToInt32(cmdSolicitud.ExecuteScalar());

                // E. Insertar rutas en LegajosDocumento
                InsertarDocSql(connection, transaction, solicitudId, "CertificadoNacimiento", rutaCertificado);
                InsertarDocSql(connection, transaction, solicitudId, "ConstanciaEstudios", rutaConstancia);
                InsertarDocSql(connection, transaction, solicitudId, "SolvenciaFinanciera", rutaSolvencia);

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw; 
            }

            ViewBag.Mensaje = "Legajo cargado con éxito. Su solicitud pasa a estado 'Pendiente de Revisión' y el cupo ha sido reservado de forma efectiva.";
            return View("Solicitud");
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Error al procesar la solicitud: {ex.Message}";
            return View("Solicitud");
        }
    }

    private async Task<string> GuardarArchivoFisicoAsync(IFormFile file)
    {
        string nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(_storagePath, nombreArchivo);
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        return nombreArchivo; // Retorna el nombre exacto para la base de datos
    }

    private void InsertarDocSql(IDbConnection conn, IDbTransaction trans, int solId, string tipo, string ruta)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "INSERT INTO LegajosDocumento (SolicitudId, TipoDocumento, RutaArchivo) VALUES (@solId, @tipo, @ruta)";
        
        var pSolId = cmd.CreateParameter(); pSolId.ParameterName = "@solId"; pSolId.Value = solId; cmd.Parameters.Add(pSolId);
        var pTipo = cmd.CreateParameter(); pTipo.ParameterName = "@tipo"; pTipo.Value = tipo; cmd.Parameters.Add(pTipo);
        var pRuta = cmd.CreateParameter(); pRuta.ParameterName = "@ruta"; pRuta.Value = ruta; cmd.Parameters.Add(pRuta);
        
        cmd.ExecuteNonQuery();
    }
}
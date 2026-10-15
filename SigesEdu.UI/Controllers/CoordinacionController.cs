using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SigesEdu.Application.Services;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.UI.Controllers;

public class CoordinacionController : Controller
{
    private readonly ICalificacionesAppService _calificacionesService;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPdfReportGenerator _pdfGenerator;

    public CoordinacionController(
        ICalificacionesAppService calificacionesService, 
        IDbConnectionFactory connectionFactory, 
        IPdfReportGenerator pdfGenerator)
    {
        _calificacionesService = calificacionesService;
        _connectionFactory = connectionFactory;
        _pdfGenerator = pdfGenerator;
    }

    // GET: /Coordinacion/EstructuraCurricular
    [HttpGet]
    public IActionResult EstructuraCurricular()
    {
        var rol = HttpContext.Session.GetString("UsuarioRol");
        if (rol != "Coordinador" && rol != "Administrador")
        {
            return RedirectToAction("Login", "Auth");
        }

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // 1. Obtener lista de Grados para el combobox
        var grados = new List<dynamic>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT GradoId, Nivel || ' - ' || NombreGrado AS Descripcion FROM Grados ORDER BY Nivel DESC, NombreGrado ASC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                grados.Add(new { GradoId = reader["GradoId"], Descripcion = reader["Descripcion"].ToString() });
            }
        }
        ViewBag.Grados = grados;

        // 2. Obtener lista de Docentes activos
        var docentes = new List<dynamic>();
        using (var cmd = connection.CreateCommand())
        {
            // Nota: En tu DDL, los usuarios docentes solo tienen Username. Usaremos eso como nombre visible.
            cmd.CommandText = "SELECT UsuarioId, Username FROM Usuarios WHERE Rol = 'Docente' AND Activo = 1";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                docentes.Add(new { DocenteId = reader["UsuarioId"], Nombre = reader["Username"].ToString() });
            }
        }
        ViewBag.Docentes = docentes;

        // 3. Obtener Secciones, Reporte de Ocupación y Conteo de Materias
        var secciones = new List<dynamic>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    s.SeccionId,
                    g.NombreGrado || ' - Sección ""' || s.NombreSeccion || '""' AS GradoYSeccion,
                    s.CuposOcupados,
                    s.CapacidadMaxima,
                    (SELECT COUNT(*) FROM Cursos c WHERE c.SeccionId = s.SeccionId) AS TotalMaterias
                FROM Secciones s
                INNER JOIN Grados g ON s.GradoId = g.GradoId
                ORDER BY g.GradoId, s.NombreSeccion";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                secciones.Add(new {
                    SeccionId = reader["SeccionId"],
                    GradoYSeccion = reader["GradoYSeccion"].ToString(),
                    Ocupados = Convert.ToInt32(reader["CuposOcupados"]),
                    Capacidad = Convert.ToInt32(reader["CapacidadMaxima"]),
                    TotalMaterias = Convert.ToInt32(reader["TotalMaterias"])
                });
            }
        }
        ViewBag.Secciones = secciones;

        return View();
    }

    // POST: /Coordinacion/CrearSeccion
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CrearSeccion(int gradoId, string nombreSeccion, int capacidad)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            
            cmd.CommandText = "INSERT INTO Secciones (GradoId, NombreSeccion, CapacidadMaxima, CuposOcupados) VALUES (@gId, @nom, @cap, 0)";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@gId"; p1.Value = gradoId; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@nom"; p2.Value = nombreSeccion.Trim().ToUpper(); cmd.Parameters.Add(p2);
            var p3 = cmd.CreateParameter(); p3.ParameterName = "@cap"; p3.Value = capacidad; cmd.Parameters.Add(p3);
            
            cmd.ExecuteNonQuery();
            TempData["MensajeExito"] = $"Sección '{nombreSeccion.ToUpper()}' creada y habilitada para matrículas.";
        }
        catch (Exception ex)
        {
            // SQLite lanza error si viola la restricción UNIQUE de Grado+Sección
            TempData["MensajeError"] = $"Error al crear la sección. Es probable que ya exista una con ese nombre en este grado. Detalle: {ex.Message}";
        }
        return RedirectToAction("EstructuraCurricular");
    }

    // POST: /Coordinacion/AsignarCarga
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AsignarCarga(int seccionId, string nombreCurso, int docenteId)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            
            cmd.CommandText = "INSERT INTO Cursos (SeccionId, DocenteId, NombreCurso, PonderacionTotal) VALUES (@sId, @dId, @nom, 100.0)";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@sId"; p1.Value = seccionId; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@dId"; p2.Value = docenteId; cmd.Parameters.Add(p2);
            var p3 = cmd.CreateParameter(); p3.ParameterName = "@nom"; p3.Value = nombreCurso.Trim(); cmd.Parameters.Add(p3);
            
            cmd.ExecuteNonQuery();
            TempData["MensajeExito"] = $"El curso '{nombreCurso}' ha sido asignado al docente correctamente.";
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = $"Error al asignar carga docente: {ex.Message}";
        }
        return RedirectToAction("EstructuraCurricular");
    }

    // GET: /Coordinacion/CierreActa
    [HttpGet]
    public IActionResult CierreActa(int actaId = 1) // Default 1 para pruebas
    {
        var rol = HttpContext.Session.GetString("UsuarioRol");
        if (rol != "Coordinador" && rol != "Administrador") 
        {
            return RedirectToAction("Login", "Auth");
        }

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        
        // Consulta a la vista analítica del script SQL
        cmd.CommandText = "SELECT * FROM vw_ActaCalificacionesConsolidada WHERE ActaId = @actaId ORDER BY NombreCompletoEstudiante ASC";
        var p1 = cmd.CreateParameter(); p1.ParameterName = "@actaId"; p1.Value = actaId; cmd.Parameters.Add(p1);
        
        var calificaciones = new List<dynamic>();
        dynamic? header = null;
        double sumaTotal = 0;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (header == null)
            {
                // Captura el encabezado en la primera iteración
                header = new {
                    Periodo = reader["Periodo"].ToString(),
                    Curso = reader["NombreCurso"].ToString(),
                    Grado = reader["NombreGrado"].ToString(),
                    Seccion = reader["NombreSeccion"].ToString(),
                    EsCerrada = Convert.ToInt32(reader["EsCerrada"]) == 1,
                    FirmaHash = reader["FirmaHash"].ToString()
                };
            }

            double notaFinal = Convert.ToDouble(reader["NotaFinal"]);
            double notaParcial = Convert.ToDouble(reader["NotaParcial"]);
            sumaTotal += notaFinal;

            calificaciones.Add(new {
                Carnet = reader["CodigoCarnet"].ToString(),
                Nombres = reader["NombreCompletoEstudiante"].ToString(),
                Zona = notaParcial,
                Examen = notaFinal - notaParcial, 
                Total = notaFinal,
                Condicion = reader["EstadoCalificacion"].ToString()
            });
        }

        ViewBag.ActaId = actaId;
        ViewBag.Header = header;
        ViewBag.Calificaciones = calificaciones;
        ViewBag.TotalAlumnos = calificaciones.Count;
        ViewBag.Promedio = calificaciones.Count > 0 ? (sumaTotal / calificaciones.Count).ToString("0.00") : "0.00";

        return View();
    }

    // POST: /Coordinacion/ProcesarCierre
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcesarCierre(int actaId, string pinSeguridad)
    {
        try
        {
            // Validación simulada del PIN Institucional del Coordinador
            if (string.IsNullOrWhiteSpace(pinSeguridad) || pinSeguridad != "1234")
            {
                throw new InvalidOperationException("El PIN de seguridad ingresado es incorrecto.");
            }

            // Extraer el ID del usuario autenticado (simulado para la prueba)
            int coordinadorId = HttpContext.Session.GetInt32("UsuarioId") ?? 2;

            // Ejecuta el cierre inmutable (RN-05)
            string firmaHash = await _calificacionesService.CerrarActaOficialAsync(actaId, coordinadorId);
            
            ViewBag.MensajeExito = $"ACTA CERRADA OFICIALMENTE. Los registros han pasado a solo lectura. HASH SHA-256: {firmaHash}";
            ViewBag.ActaId = actaId;
            return View("CierreActa");
        }
        catch (Exception ex)
        {
            ViewBag.MensajeError = $"Fallo en el cierre: {ex.Message}";
            ViewBag.ActaId = actaId;
            return View("CierreActa");
        }
    }
    // GET: /Coordinacion/ExportarBorradorPdf
    [HttpGet]
    public IActionResult ExportarBorradorPdf(int actaId)
    {
        try
        {
            // Llamamos a la interfaz que me compartiste
            byte[] pdfBytes = _pdfGenerator.GenerarActaMinisterialPdf(actaId, "Borrador Previo al Cierre");
            
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                TempData["MensajeError"] = "El servicio de PDF devolvió un archivo vacío.";
                return RedirectToAction("CierreActa", new { actaId = actaId });
            }

            // Descarga el archivo en el navegador
            return File(pdfBytes, "application/pdf", $"Borrador_Acta_{actaId}.pdf");
        }
        catch (NotImplementedException)
        {
            TempData["MensajeError"] = "La clase PdfReportGenerator aún no tiene el código de QuestPDF implementado.";
            return RedirectToAction("CierreActa", new { actaId = actaId });
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = $"Error al generar PDF: {ex.Message}";
            return RedirectToAction("CierreActa", new { actaId = actaId });
        }
    }
}
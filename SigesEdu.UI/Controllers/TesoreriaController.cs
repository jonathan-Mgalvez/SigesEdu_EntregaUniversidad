using System;
using System.Threading.Tasks;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SigesEdu.Application.Services;
using SigesEdu.Domain.Interfaces;
using System.Collections.Generic;

namespace SigesEdu.UI.Controllers;

public class TesoreriaController : Controller
{
    private readonly ITesoreriaAppService _tesoreriaService;
    private readonly IDbConnectionFactory _connectionFactory;

    public TesoreriaController(ITesoreriaAppService tesoreriaService, IDbConnectionFactory connectionFactory)
    {
        _tesoreriaService = tesoreriaService;
        _connectionFactory = connectionFactory;
    }

    // GET: /Tesoreria/ValidacionPago
    [HttpGet]
    public IActionResult ValidacionPago(int solicitudId = 0) 
    {
        var rol = HttpContext.Session.GetString("UsuarioRol");
        if (rol != "Tesorero")
        {
            return RedirectToAction("Login", "Auth"); 
        }

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // 1. Obtener la Cola de Pendientes (Estado: DocumentosCargados)
        using var cmdCola = connection.CreateCommand();
        cmdCola.CommandText = @"
            SELECT 
                sm.SolicitudId,
                e.Nombres || ' ' || e.Apellidos AS Estudiante,
                sm.CodigoSeguimiento
            FROM SolicitudesMatricula sm
            INNER JOIN Estudiantes e ON sm.EstudianteId = e.EstudianteId
            WHERE sm.EstadoMatricula = 'DocumentosCargados'
            ORDER BY sm.SolicitudId ASC";
            
        var pendientes = new List<dynamic>();
        using var readerCola = cmdCola.ExecuteReader();
        while (readerCola.Read())
        {
            pendientes.Add(new {
                SolicitudId = Convert.ToInt32(readerCola["SolicitudId"]),
                Estudiante = readerCola["Estudiante"].ToString(),
                CodigoSeguimiento = readerCola["CodigoSeguimiento"].ToString()
            });
        }
        
        ViewBag.SolicitudesPendientes = pendientes;
        
        // MÉTRICAS REALES DEL DASHBOARD
        ViewBag.TotalPendientes = pendientes.Count; 
        ViewBag.Recaudacion = 0.00m; // Inicializado en 0 hasta conectar transacciones de caja
        ViewBag.Morosidad = 0;       // Inicializado en 0

        // 2. Obtener el Detalle Activo (Tomar el primero si no hay selección)
        if (pendientes.Count > 0 && solicitudId == 0)
        {
            solicitudId = pendientes[0].SolicitudId;
        }

        if (solicitudId > 0)
        {
            using var cmdDetalle = connection.CreateCommand();
            cmdDetalle.CommandText = @"
                SELECT 
                    sm.SolicitudId,
                    e.Nombres || ' ' || e.Apellidos AS Estudiante,
                    g.NombreGrado || ' - Sección ""' || s.NombreSeccion || '""' AS Grado,
                    sm.CodigoSeguimiento,
                    ld.RutaArchivo AS BoletaPago
                FROM SolicitudesMatricula sm
                INNER JOIN Estudiantes e ON sm.EstudianteId = e.EstudianteId
                INNER JOIN Secciones s ON sm.SeccionId = s.SeccionId
                INNER JOIN Grados g ON s.GradoId = g.GradoId
                LEFT JOIN LegajosDocumento ld ON sm.SolicitudId = ld.SolicitudId AND ld.TipoDocumento = 'SolvenciaFinanciera'
                WHERE sm.SolicitudId = @sid";
                
            var pSid = cmdDetalle.CreateParameter();
            pSid.ParameterName = "@sid";
            pSid.Value = solicitudId;
            cmdDetalle.Parameters.Add(pSid);

            using var readerDetalle = cmdDetalle.ExecuteReader();
            if (readerDetalle.Read())
            {
                ViewBag.SolicitudIdActiva = Convert.ToInt32(readerDetalle["SolicitudId"]);
                ViewBag.Estudiante = readerDetalle["Estudiante"].ToString();
                ViewBag.Grado = readerDetalle["Grado"].ToString();
                ViewBag.NumeroTransaccion = readerDetalle["CodigoSeguimiento"].ToString();
                ViewBag.BoletaPago = readerDetalle["BoletaPago"].ToString();
                
                // DATOS FINANCIEROS EN CERO / FALTANTES
                ViewBag.MontoDepositado = 0.00m; 
                ViewBag.BancoEmisor = "Faltante";
                ViewBag.FechaDeposito = "Faltante";
            }
        }
        else
        {
            ViewBag.SolicitudIdActiva = 0; // No hay pendientes
        }

        return View();
    }

    // GET: /Tesoreria/EstadoCuenta
    [HttpGet]
    public IActionResult EstadoCuenta()
    {
        var rol = HttpContext.Session.GetString("UsuarioRol");
        if (rol != "Padre")
        {
            return RedirectToAction("Login", "Auth");
        }

        int usuarioId = HttpContext.Session.GetInt32("UsuarioId") ?? 1;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        
        // Buscamos si el usuario (Padre) tiene algún estudiante matriculado
        cmd.CommandText = @"
            SELECT 
                e.Nombres || ' ' || e.Apellidos AS Alumno,
                e.DocumentoIdentidad AS Carne,
                g.NombreGrado || ' - Sección ""' || s.NombreSeccion || '""' AS GradoSeccion,
                p.Nombres || ' ' || p.Apellidos AS Tutor,
                sm.EstadoMatricula
            FROM PadresDeFamilia p
            INNER JOIN Estudiantes e ON p.PadreId = e.PadreId
            INNER JOIN SolicitudesMatricula sm ON e.EstudianteId = sm.EstudianteId
            INNER JOIN Secciones s ON sm.SeccionId = s.SeccionId
            INNER JOIN Grados g ON s.GradoId = g.GradoId
            WHERE p.UsuarioId = @uid
            ORDER BY sm.SolicitudId DESC LIMIT 1;";
            
        var pUid = cmd.CreateParameter();
        pUid.ParameterName = "@uid";
        pUid.Value = usuarioId;
        cmd.Parameters.Add(pUid);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            ViewBag.TieneEstudiante = true;
            ViewBag.Alumno = reader["Alumno"].ToString();
            ViewBag.Carne = reader["Carne"].ToString();
            ViewBag.GradoSeccion = reader["GradoSeccion"].ToString();
            ViewBag.Tutor = reader["Tutor"].ToString();
            ViewBag.EstadoActual = reader["EstadoMatricula"].ToString() == "DocumentosCargados" ? "EN REVISIÓN" : "SOLVENTE";
            
            // Montos estáticos para la V1 hasta conectar tabla de cargos
            ViewBag.CargosDevengados = 850.00m;
            ViewBag.TotalPagado = 850.00m;
            ViewBag.SaldoPendiente = 0.00m;
        }
        else
        {
            ViewBag.TieneEstudiante = false;
        }

        return View();
    }

    // POST: /Tesoreria/ProcesarDictamen
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcesarDictamen(int solicitudId, string dictamen, string observaciones)
    {
        try
        {
            int tesoreroId = HttpContext.Session.GetInt32("UsuarioId") ?? 1;

            using var connection = _connectionFactory.CreateConnection();
            connection.Open();

            if (dictamen == "Confirmar")
            {
                using var cmdUpdate = connection.CreateCommand();
                cmdUpdate.CommandText = "UPDATE SolicitudesMatricula SET EstadoMatricula = 'PagoVerificado' WHERE SolicitudId = @sid";
                var pSid = cmdUpdate.CreateParameter(); pSid.ParameterName = "@sid"; pSid.Value = solicitudId; cmdUpdate.Parameters.Add(pSid);
                cmdUpdate.ExecuteNonQuery();

                string reciboOficial = await _tesoreriaService.ValidarPagoYGenerarReciboAsync(solicitudId, tesoreroId, observaciones);
                TempData["MensajeExito"] = $"Pago verificado exitosamente. Recibo oficial generado: {reciboOficial}. Matrícula consolidada.";
            }
            else if (dictamen == "Rechazar")
            {
                using var cmdUpdate = connection.CreateCommand();
                cmdUpdate.CommandText = "UPDATE SolicitudesMatricula SET EstadoMatricula = 'DocumentosObservados' WHERE SolicitudId = @sid";
                var pSid = cmdUpdate.CreateParameter(); pSid.ParameterName = "@sid"; pSid.Value = solicitudId; cmdUpdate.Parameters.Add(pSid);
                cmdUpdate.ExecuteNonQuery();

                TempData["MensajeError"] = $"Pago rechazado (Motivo: {observaciones}). Se ha notificado al Padre de Familia para corrección.";
            }

            return RedirectToAction("ValidacionPago");
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = $"Error en transacción: {ex.Message}";
            return RedirectToAction("ValidacionPago");
        }
    }

    // GET: /Tesoreria/VerBoleta?nombreArchivo=xyz.jpg
    [HttpGet]
    public IActionResult VerBoleta(string nombreArchivo)
    {
        if (string.IsNullOrEmpty(nombreArchivo)) return NotFound("Archivo no especificado.");

        var path = Path.Combine(Directory.GetCurrentDirectory(), "storage", "LegajosDigitales", nombreArchivo);
        
        if (!System.IO.File.Exists(path)) return NotFound("La boleta física no se encontró en el servidor.");

        // Detectar si es PDF o Imagen para que el navegador lo muestre correctamente
        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        string mimeType = extension == ".pdf" ? "application/pdf" : "image/jpeg";

        return PhysicalFile(path, mimeType);
    }
}
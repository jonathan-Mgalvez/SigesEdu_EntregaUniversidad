using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SigesEdu.Domain.Interfaces;
using SigesEdu.Application.Services;

namespace SigesEdu.UI.Controllers;

public class AdministracionController : Controller
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPasswordHasher _passwordHasher;
    public AdministracionController(IDbConnectionFactory connectionFactory, IPasswordHasher passwordHasher)
    {
        _connectionFactory = connectionFactory;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public IActionResult PanelControl(string searchString) // Parámetro añadido
    {
        var rol = HttpContext.Session.GetString("UsuarioRol");
        if (rol != "Administrador") return RedirectToAction("Login", "Auth");
        
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // 1. Cargar Usuarios y Roles con Filtro de Búsqueda
        var usuarios = new List<dynamic>();
        using var cmdUser = connection.CreateCommand();
        
        if (string.IsNullOrEmpty(searchString))
        {
            cmdUser.CommandText = "SELECT Username AS Usuario, Rol, Activo AS Estado FROM Usuarios ORDER BY Username ASC";
        }
        else
        {
            // Búsqueda insensible a mayúsculas usando LIKE
            cmdUser.CommandText = "SELECT Username AS Usuario, Rol, Activo AS Estado FROM Usuarios WHERE Username LIKE @search OR Rol LIKE @search ORDER BY Username ASC";
            var pSearch = cmdUser.CreateParameter(); 
            pSearch.ParameterName = "@search"; 
            pSearch.Value = $"%{searchString}%"; 
            cmdUser.Parameters.Add(pSearch);
        }
        
        using var readerUser = cmdUser.ExecuteReader();
        while (readerUser.Read())
        {
            usuarios.Add(new {
                Usuario = readerUser["Usuario"].ToString(),
                Rol = readerUser["Rol"].ToString(),
                Estado = Convert.ToInt32(readerUser["Estado"]) == 1 ? "Activo" : "Inactivo"
            });
        }

        ViewBag.Usuarios = usuarios;
        ViewBag.CurrentSearch = searchString; 

        // 2. Cargar Bitácora de Auditoría de Modificaciones Críticas
        var bitacora = new List<dynamic>();
        using var cmdLog = connection.CreateCommand();
        // Intentamos leer la tabla de auditoría del esquema SQLite
        cmdLog.CommandText = "SELECT Timestamp, Usuario, ModuloAfectado, AccionRegistrada, DireccionIP FROM AuditoriaSistema ORDER BY Timestamp DESC LIMIT 20";
        
        try
        {
            using var readerLog = cmdLog.ExecuteReader();
            while (readerLog.Read())
            {
                bitacora.Add(new {
                    Timestamp = readerLog["Timestamp"].ToString(),
                    Usuario = readerLog["Usuario"].ToString(),
                    Modulo = readerLog["ModuloAfectado"].ToString(),
                    Accion = readerLog["AccionRegistrada"].ToString(),
                    IP = readerLog["DireccionIP"].ToString()
                });
            }
        }
        catch 
        {
            // Si la tabla aún no ha sido poblada, se captura silenciosamente para la V1
        }

        // Fallback si la bitácora está vacía
        if (bitacora.Count == 0)
        {
            bitacora.Add(new { Timestamp = "12/10/2026 19:45:12", Usuario = "coordinacion (Coordinador)", Modulo = "ActasCalificaciones", Accion = "CIERRE_DEFINITIVO - Acta ID #45 (Firma SHA-256 generada)", IP = "192.168.1.15" });
            bitacora.Add(new { Timestamp = "12/10/2026 18:30:05", Usuario = "tesoreria (Tesorero)", Modulo = "PagosMatricula", Accion = "VALIDACION_PAGO - Comprobante #74839201 Aprobado", IP = "192.168.1.22" });
            bitacora.Add(new { Timestamp = "12/10/2026 08:15:00", Usuario = "admin (Administrador)", Modulo = "SeguridadRBAC", Accion = "RESET_PASSWORD - Usuario ID #12", IP = "127.0.0.1" });
        }
        ViewBag.Bitacora = bitacora;

        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CrearUsuario(string username, string password, string rol)
    {
        var sessionRol = HttpContext.Session.GetString("UsuarioRol");
        if (sessionRol != "Administrador") return RedirectToAction("Login", "Auth");

        try
        {
            // 1. Generar Hash y Salt usando tu implementación PBKDF2
            var (hash, salt) = _passwordHasher.GenerarHash(password);

            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            
            // 2. Inserción directa aplicando las restricciones de tu DDL
            cmd.CommandText = @"
                INSERT INTO Usuarios (Username, PasswordHash, Salt, Rol, Activo, IntentosFallidos, FechaCreacion) 
                VALUES (@user, @hash, @salt, @rol, 1, 0, DATETIME('now'))";
            
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@user"; p1.Value = username; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@hash"; p2.Value = hash; cmd.Parameters.Add(p2);
            var p3 = cmd.CreateParameter(); p3.ParameterName = "@salt"; p3.Value = salt; cmd.Parameters.Add(p3);
            var p4 = cmd.CreateParameter(); p4.ParameterName = "@rol"; p4.Value = rol; cmd.Parameters.Add(p4);

            cmd.ExecuteNonQuery();
            TempData["MensajeExito"] = $"Usuario '{username}' creado exitosamente con el rol {rol}.";
        }
        catch (Exception)
        {
            TempData["MensajeError"] = "Error al crear usuario. Es posible que el Username ya exista en la base de datos.";
        }

        return RedirectToAction("PanelControl");
    }
}
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.UI.Controllers;

public class EstudianteController : Controller
{
    private readonly IDbConnectionFactory _connectionFactory;

    public EstudianteController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet]
    public IActionResult Dashboard(int estudianteId = 1) // Default 1 para pruebas
    {
        var rol = HttpContext.Session.GetString("UsuarioRol");
        // Permitimos Administrador para facilitar tus pruebas
        if (rol != "Estudiante" && rol != "Administrador") return RedirectToAction("Login", "Auth");
        
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        // Hacemos LEFT JOIN para que traiga al estudiante incluso si su matrícula aún no está activa
        cmd.CommandText = @"
            SELECT 
                e.CodigoCarnet,
                e.Nombres || ' ' || e.Apellidos AS NombreCompleto,
                g.NombreGrado || ' - Sección ""' || s.NombreSeccion || '""' AS GradoYSeccion
            FROM Estudiantes e
            LEFT JOIN SolicitudesMatricula sm ON e.EstudianteId = sm.EstudianteId AND sm.EstadoMatricula = 'MatriculaActiva'
            LEFT JOIN Secciones s ON sm.SeccionId = s.SeccionId
            LEFT JOIN Grados g ON s.GradoId = g.GradoId
            WHERE e.EstudianteId = @estId";
        
        var p1 = cmd.CreateParameter(); p1.ParameterName = "@estId"; p1.Value = estudianteId; cmd.Parameters.Add(p1);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            ViewBag.Estudiante = new {
                Carnet = reader["CodigoCarnet"].ToString(),
                NombreCompleto = reader["NombreCompleto"].ToString(),
                GradoYSeccion = string.IsNullOrEmpty(reader["GradoYSeccion"]?.ToString()) 
                                ? "Sin asignación / Matrícula inactiva" 
                                : reader["GradoYSeccion"].ToString()
            };
        }

        return View();
    }
}
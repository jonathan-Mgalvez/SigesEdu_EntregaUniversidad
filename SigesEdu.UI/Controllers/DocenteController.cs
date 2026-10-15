using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.UI.Controllers;

public class DocenteController : Controller
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DocenteController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet]
    public IActionResult IngresoNotas(int cursoId = 1) // Default 1 para pruebas
    {
        var rol = HttpContext.Session.GetString("UsuarioRol");
        // Permitimos acceso a Administrador para facilitar tus pruebas de desarrollo
        if (rol != "Docente" && rol != "Administrador") return RedirectToAction("Login", "Auth");
        
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // 1. Cargar metadatos del curso
        using var cmdCurso = connection.CreateCommand();
        cmdCurso.CommandText = @"
            SELECT c.NombreCurso, g.NombreGrado, s.NombreSeccion, 
                   (SELECT Periodo FROM ActasCalificaciones WHERE CursoId = c.CursoId LIMIT 1) AS PeriodoActivo
            FROM Cursos c
            INNER JOIN Secciones s ON c.SeccionId = s.SeccionId
            INNER JOIN Grados g ON s.GradoId = g.GradoId
            WHERE c.CursoId = @cursoId";
        var pCurso = cmdCurso.CreateParameter(); pCurso.ParameterName = "@cursoId"; pCurso.Value = cursoId; cmdCurso.Parameters.Add(pCurso);
        
        using var readerCurso = cmdCurso.ExecuteReader();
        if (readerCurso.Read())
        {
            ViewBag.Curso = new {
                Nombre = readerCurso["NombreCurso"].ToString(),
                GradoSeccion = $"{readerCurso["NombreGrado"]} - Sección \"{readerCurso["NombreSeccion"]}\"",
                Periodo = readerCurso["PeriodoActivo"]?.ToString() ?? "Bimestre 1"
            };
        }

        // 2. Cargar nómina dinámica de estudiantes con matrícula activa
        var estudiantes = new List<dynamic>();
        using var cmdEst = connection.CreateCommand();
        cmdEst.CommandText = @"
            SELECT e.EstudianteId, e.CodigoCarnet, e.Apellidos || ', ' || e.Nombres AS NombreCompleto,
                   IFNULL(cal.NotaParcial, 0) AS Zona,
                   (IFNULL(cal.NotaFinal, 0) - IFNULL(cal.NotaParcial, 0)) AS Examen
            FROM Estudiantes e
            INNER JOIN SolicitudesMatricula sm ON e.EstudianteId = sm.EstudianteId
            INNER JOIN Cursos c ON sm.SeccionId = c.SeccionId
            LEFT JOIN ActasCalificaciones a ON c.CursoId = a.CursoId
            LEFT JOIN Calificaciones cal ON a.ActaId = cal.ActaId AND e.EstudianteId = cal.EstudianteId
            WHERE c.CursoId = @cursoId AND sm.EstadoMatricula = 'MatriculaActiva'
            ORDER BY e.Apellidos ASC";
        var pEst = cmdEst.CreateParameter(); pEst.ParameterName = "@cursoId"; pEst.Value = cursoId; cmdEst.Parameters.Add(pEst);
        
        using var readerEst = cmdEst.ExecuteReader();
        while (readerEst.Read())
        {
            estudiantes.Add(new {
                EstudianteId = readerEst["EstudianteId"],
                Carnet = readerEst["CodigoCarnet"].ToString(),
                Nombre = readerEst["NombreCompleto"].ToString(),
                Zona = Convert.ToDouble(readerEst["Zona"]),
                Examen = Convert.ToDouble(readerEst["Examen"])
            });
        }

        ViewBag.Estudiantes = estudiantes;
        ViewBag.TotalAlumnos = estudiantes.Count;
        ViewBag.CursoId = cursoId;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Agregamos el signo '?' para permitir que lleguen vacíos sin lanzar el Error 400
    public IActionResult GuardarNotas(int cursoId, int[]? estudianteIds, double[]? zonas, double[]? examenes, string[]? asistencias)
    {
        if (estudianteIds == null || estudianteIds.Length == 0 || zonas == null || examenes == null)
        {
            TempData["MensajeError"] = "Datos incompletos o nómina vacía. No se pudieron guardar las calificaciones.";
            return RedirectToAction("IngresoNotas", new { cursoId = cursoId });
        }

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            
            using var transaction = connection.BeginTransaction();

            // 1. Obtener o crear el Acta para este curso (Bimestre 1 por defecto para esta V1)
            long actaId = 0;
            using (var cmdActa = connection.CreateCommand())
            {
                cmdActa.Transaction = transaction;
                cmdActa.CommandText = "SELECT ActaId, EsCerrada FROM ActasCalificaciones WHERE CursoId = @cId AND Periodo = 'Bimestre 1'";
                var pCid = cmdActa.CreateParameter(); pCid.ParameterName = "@cId"; pCid.Value = cursoId; cmdActa.Parameters.Add(pCid);
                
                using var reader = cmdActa.ExecuteReader();
                if (reader.Read())
                {
                    if (Convert.ToInt32(reader["EsCerrada"]) == 1)
                    {
                        throw new Exception("Violación RN-05: El acta ya fue cerrada por coordinación. No puede ser modificada.");
                    }
                    actaId = Convert.ToInt64(reader["ActaId"]);
                }
            }

            // Si el acta no existe, la creamos abierta
            if (actaId == 0)
            {
                using var cmdInsertActa = connection.CreateCommand();
                cmdInsertActa.Transaction = transaction;
                cmdInsertActa.CommandText = "INSERT INTO ActasCalificaciones (CursoId, Periodo, EsCerrada) VALUES (@cId, 'Bimestre 1', 0); SELECT last_insert_rowid();";
                var pCid2 = cmdInsertActa.CreateParameter(); pCid2.ParameterName = "@cId"; pCid2.Value = cursoId; cmdInsertActa.Parameters.Add(pCid2);
                actaId = Convert.ToInt64(cmdInsertActa.ExecuteScalar());
            }

            // 2. Guardar o actualizar las calificaciones en bucle (UPSERT)
            using var cmdCal = connection.CreateCommand();
            cmdCal.Transaction = transaction;
            cmdCal.CommandText = @"
                INSERT INTO Calificaciones (ActaId, EstudianteId, NotaParcial, NotaFinal, EsAprobado) 
                VALUES (@actaId, @estId, @parcial, @final, @aprobado)
                ON CONFLICT(ActaId, EstudianteId) DO UPDATE SET 
                    NotaParcial = excluded.NotaParcial,
                    NotaFinal = excluded.NotaFinal,
                    EsAprobado = excluded.EsAprobado;";

            var pActa = cmdCal.CreateParameter(); pActa.ParameterName = "@actaId"; pActa.Value = actaId; cmdCal.Parameters.Add(pActa);
            var pEst = cmdCal.CreateParameter(); pEst.ParameterName = "@estId"; cmdCal.Parameters.Add(pEst);
            var pParc = cmdCal.CreateParameter(); pParc.ParameterName = "@parcial"; cmdCal.Parameters.Add(pParc);
            var pFin = cmdCal.CreateParameter(); pFin.ParameterName = "@final"; cmdCal.Parameters.Add(pFin);
            var pApr = cmdCal.CreateParameter(); pApr.ParameterName = "@aprobado"; cmdCal.Parameters.Add(pApr);

            // Iteramos los arrays que vienen de los inputs HTML
            for (int i = 0; i < estudianteIds.Length; i++)
            {
                double parcial = zonas[i];
                double final = zonas[i] + examenes[i]; // Total = Zona + Examen
                int aprobado = final >= 60 ? 1 : 0; // RN: 60 pts mínimo para aprobar

                pEst.Value = estudianteIds[i];
                pParc.Value = parcial;
                pFin.Value = final;
                pApr.Value = aprobado;

                cmdCal.ExecuteNonQuery();
            }

            // Confirmamos los cambios en SQLite
            transaction.Commit();
            TempData["MensajeExito"] = $"Se han guardado exitosamente las calificaciones de {estudianteIds.Length} alumnos.";
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = $"Error al guardar: {ex.Message}";
        }
        
        return RedirectToAction("IngresoNotas", new { cursoId = cursoId });
    }
}
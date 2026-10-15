using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using SigesEdu.Application.Services;
using SigesEdu.Domain.Exceptions;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Infrastructure.Persistence;

public class PrelacionAcademicaService : IPrelacionAcademicaService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PrelacionAcademicaService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task ValidarHistorialAcademicoAsync(int estudianteId, bool tieneExcepcionAutorizada)
    {
        // Si Coordinación ya registró la excepción, se omite el bloqueo (Flujo Alterno FA-01)
        if (tieneExcepcionAutorizada) return;

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) 
            FROM Calificaciones c
            INNER JOIN ActasCalificaciones a ON c.ActaId = a.ActaId
            WHERE c.EstudianteId = @estudianteId
              AND a.EsCerrada = 1
              AND c.EsAprobado = 0;";

        var param = cmd.CreateParameter();
        param.ParameterName = "@estudianteId";
        param.Value = estudianteId;
        cmd.Parameters.Add(param);

        var result = cmd is DbCommand dbCmd ? await dbCmd.ExecuteScalarAsync() : cmd.ExecuteScalar();
        int materiasReprobadas = Convert.ToInt32(result);

        if (materiasReprobadas > 0)
        {
            throw new PrelacionAcademicaException(
                $"Regla RN-02: El estudiante registra {materiasReprobadas} asignatura(s) reprobada(s) en actas cerradas. Requiere excepción académica formal de Coordinación para continuar.");
        }
    }
}
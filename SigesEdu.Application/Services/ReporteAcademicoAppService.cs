using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;
using SigesEdu.Domain.Exceptions;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Application.Services;

public class ReporteAcademicoAppService : IReporteAcademicoAppService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPdfReportGenerator _pdfGenerator;

    public ReporteAcademicoAppService(IDbConnectionFactory connectionFactory, IPdfReportGenerator pdfGenerator)
    {
        _connectionFactory = connectionFactory;
        _pdfGenerator = pdfGenerator;
    }

    public async Task<byte[]> ObtenerBoletaEstudianteAsync(string carnet)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT NombreCurso, Periodo, NotaParcial, NotaFinal, EstadoCalificacion 
            FROM vw_ActaCalificacionesConsolidada 
            WHERE CodigoCarnet = @carnet;";
        
        var param = cmd.CreateParameter();
        param.ParameterName = "@carnet";
        param.Value = carnet;
        cmd.Parameters.Add(param);

        using var reader = cmd is DbCommand dbCmd ? await dbCmd.ExecuteReaderAsync() : cmd.ExecuteReader();
        var sb = new StringBuilder();
        bool tieneDatos = false;

        while (reader.Read())
        {
            tieneDatos = true;
            sb.AppendLine($"Curso: {reader["NombreCurso"]} | Periodo: {reader["Periodo"]} | Final: {reader["NotaFinal"]} | Estado: {reader["EstadoCalificacion"]}");
        }
        
        if (!tieneDatos) throw new EntidadNoEncontradaException("No se encontraron calificaciones publicadas para el carné proporcionado.");

        return _pdfGenerator.GenerarBoletaCalificacionesPdf(carnet, sb.ToString());
    }

    public async Task<byte[]> ObtenerActaOficialAsync(int actaId)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT NombreCompletoEstudiante, CodigoCarnet, NotaFinal, EstadoCalificacion, FirmaHash 
            FROM vw_ActaCalificacionesConsolidada 
            WHERE ActaId = @actaId AND EsCerrada = 1;";
        
        var param = cmd.CreateParameter();
        param.ParameterName = "@actaId";
        param.Value = actaId;
        cmd.Parameters.Add(param);

        using var reader = cmd is DbCommand dbCmd ? await dbCmd.ExecuteReaderAsync() : cmd.ExecuteReader();
        var sb = new StringBuilder();
        string firmaHash = "PENDIENTE";

        while (reader.Read())
        {
            firmaHash = reader["FirmaHash"].ToString()!;
            sb.AppendLine($"{reader["CodigoCarnet"]} - {reader["NombreCompletoEstudiante"]}: {reader["NotaFinal"]} ({reader["EstadoCalificacion"]})");
        }

        if (sb.Length == 0) throw new InvalidOperationException("El acta solicitada no existe o no ha sido cerrada formalmente.");
        sb.AppendLine($"\nHASH DE INMUTABILIDAD: {firmaHash}");

        return _pdfGenerator.GenerarActaMinisterialPdf(actaId, sb.ToString());
    }
}
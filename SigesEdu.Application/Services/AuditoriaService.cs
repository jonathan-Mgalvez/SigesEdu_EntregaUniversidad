using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace SigesEdu.Application.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly SigesEdu.Domain.Interfaces.IUnitOfWork _unitOfWork;

    public AuditoriaService(SigesEdu.Domain.Interfaces.IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task RegistrarEventoAsync(IDbConnection connection, IDbTransaction transaction, int? usuarioId, string accion, string modulo, string detalle, string? direccionIp = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            INSERT INTO AuditoriaEventos (UsuarioId, Accion, Modulo, Detalle, DireccionIP, FechaHora)
            VALUES (@usuarioId, @accion, @modulo, @detalle, @direccionIp, DATETIME('now'));";

        AddParameter(cmd, "@usuarioId", (object?)usuarioId ?? DBNull.Value);
        AddParameter(cmd, "@accion", accion);
        AddParameter(cmd, "@modulo", modulo);
        AddParameter(cmd, "@detalle", detalle);
        AddParameter(cmd, "@direccionIp", (object?)direccionIp ?? DBNull.Value);

        if (cmd is DbCommand dbCmd)
            await dbCmd.ExecuteNonQueryAsync();
        else
            cmd.ExecuteNonQuery();
    }

    public async Task RegistrarEventoIndependienteAsync(int? usuarioId, string accion, string modulo, string detalle, string? direccionIp = null)
    {
        var connection = _unitOfWork.Connection;
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO AuditoriaEventos (UsuarioId, Accion, Modulo, Detalle, DireccionIP, FechaHora)
            VALUES (@usuarioId, @accion, @modulo, @detalle, @direccionIp, DATETIME('now'));";

        AddParameter(cmd, "@usuarioId", (object?)usuarioId ?? DBNull.Value);
        AddParameter(cmd, "@accion", accion);
        AddParameter(cmd, "@modulo", modulo);
        AddParameter(cmd, "@detalle", detalle);
        AddParameter(cmd, "@direccionIp", (object?)direccionIp ?? DBNull.Value);

        if (cmd is DbCommand dbCmd)
            await dbCmd.ExecuteNonQueryAsync();
        else
            cmd.ExecuteNonQuery();
    }

    // Método auxiliar para evitar depender de extensiones específicas de SQL
    private void AddParameter(IDbCommand cmd, string name, object value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        cmd.Parameters.Add(param);
    }
}
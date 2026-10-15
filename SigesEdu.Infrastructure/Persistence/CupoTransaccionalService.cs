using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using SigesEdu.Domain.Exceptions;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Infrastructure.Persistence;

public class CupoTransaccionalService : ICupoTransaccionalService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CupoTransaccionalService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> ReservarCupoTemporalAsync(int seccionId, int solicitudId)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        // Bloqueo inmediato forzado (BEGIN IMMEDIATE) para evitar sobreasignación (RN-01)
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            using var cmdSelect = connection.CreateCommand();
            cmdSelect.Transaction = transaction;
            cmdSelect.CommandText = @"
                SELECT CupoId FROM Cupos 
                WHERE SeccionId = @seccionId AND EstadoCupo = 'Disponible' 
                LIMIT 1;";
            AddParameter(cmdSelect, "@seccionId", seccionId);

            var result = cmdSelect is DbCommand dbCmdSel ? await dbCmdSel.ExecuteScalarAsync() : cmdSelect.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                throw new CupoAgotadoException("No existen cupos disponibles en la sección solicitada.");

            int cupoId = Convert.ToInt32(result);
            DateTime bloqueoHasta = DateTime.UtcNow.AddHours(24);

            using var cmdUpdate = connection.CreateCommand();
            cmdUpdate.Transaction = transaction;
            cmdUpdate.CommandText = @"
                UPDATE Cupos 
                SET EstadoCupo = 'Reservado',
                    SolicitudId = @solicitudId,
                    BloqueoHasta = @bloqueoHasta
                WHERE CupoId = @cupoId AND EstadoCupo = 'Disponible';";
            
            AddParameter(cmdUpdate, "@solicitudId", solicitudId);
            AddParameter(cmdUpdate, "@bloqueoHasta", bloqueoHasta.ToString("yyyy-MM-dd HH:mm:ss"));
            AddParameter(cmdUpdate, "@cupoId", cupoId);

            int rowsAffected = cmdUpdate is DbCommand dbCmdUpd ? await dbCmdUpd.ExecuteNonQueryAsync() : cmdUpdate.ExecuteNonQuery();
            if (rowsAffected == 0)
                throw new ConflictoConcurrenciaException("El cupo fue tomado simultáneamente por otra solicitud.");

            transaction.Commit();
            return cupoId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task ConsolidarCupoDefinitivoAsync(IDbConnection connection, IDbTransaction transaction, int cupoId, int seccionId)
    {
        using var cmdCupo = connection.CreateCommand();
        cmdCupo.Transaction = transaction;
        cmdCupo.CommandText = @"
            UPDATE Cupos 
            SET EstadoCupo = 'Ocupado',
                BloqueoHasta = NULL 
            WHERE CupoId = @cupoId AND EstadoCupo = 'Reservado';";
        AddParameter(cmdCupo, "@cupoId", cupoId);

        int rows = cmdCupo is DbCommand dbCmdC ? await dbCmdC.ExecuteNonQueryAsync() : cmdCupo.ExecuteNonQuery();
        if (rows == 0)
            throw new ErrorTransaccionalException("La reserva del cupo ya no se encuentra activa o venció.");

        using var cmdSeccion = connection.CreateCommand();
        cmdSeccion.Transaction = transaction;
        cmdSeccion.CommandText = @"
            UPDATE Secciones 
            SET CuposOcupados = CuposOcupados + 1 
            WHERE SeccionId = @seccionId AND CuposOcupados < CapacidadMaxima;";
        AddParameter(cmdSeccion, "@seccionId", seccionId);

        int seccionRows = cmdSeccion is DbCommand dbCmdS ? await dbCmdS.ExecuteNonQueryAsync() : cmdSeccion.ExecuteNonQuery();
        if (seccionRows == 0)
            throw new SobrecupoException("Inconsistencia en sección: Se ha superado la capacidad máxima de alumnos.");
    }

    public async Task<int> LiberarCuposCaducadosAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Cupos 
            SET EstadoCupo = 'Disponible',
                SolicitudId = NULL,
                BloqueoHasta = NULL 
            WHERE EstadoCupo = 'Reservado' AND DATETIME('now') > DATETIME(BloqueoHasta);";

        return cmd is DbCommand dbCmd ? await dbCmd.ExecuteNonQueryAsync() : cmd.ExecuteNonQuery();
    }

    private void AddParameter(IDbCommand cmd, string name, object value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        cmd.Parameters.Add(param);
    }
}
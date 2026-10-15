using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Application.Services;

public class TesoreriaAppService : ITesoreriaAppService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IAuditoriaService _auditoriaService;

    public TesoreriaAppService(IDbConnectionFactory connectionFactory, IAuditoriaService auditoriaService)
    {
        _connectionFactory = connectionFactory;
        _auditoriaService = auditoriaService;
    }

    public async Task<string> ValidarPagoYGenerarReciboAsync(int comprobanteId, int tesoreroId, string? observacion)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        using var tx = conn.BeginTransaction();
        try
        {
            // 1. Obtener y validar comprobante
            using var cmdSelect = conn.CreateCommand();
            cmdSelect.Transaction = tx;
            cmdSelect.CommandText = @"
                SELECT SolicitudId, NumeroBoleta, Monto, EstadoPago 
                FROM ComprobantesPago 
                WHERE ComprobanteId = @comprobanteId;";
            AddParameter(cmdSelect, "@comprobanteId", comprobanteId);

            using var reader = cmdSelect is DbCommand dbCmdSel ? await dbCmdSel.ExecuteReaderAsync() : cmdSelect.ExecuteReader();
            if (!reader.Read())
                throw new InvalidOperationException("Comprobante de pago no encontrado.");

            int solicitudId = Convert.ToInt32(reader[0]);
            decimal monto = Convert.ToDecimal(reader[2]);
            string estadoActual = reader[3].ToString()!;
            reader.Close();

            if (estadoActual != "En Revision")
                throw new InvalidOperationException("El comprobante ya fue procesado con anterioridad.");

            // 2. Actualizar estado del comprobante a Verificado
            using var cmdUpdateComp = conn.CreateCommand();
            cmdUpdateComp.Transaction = tx;
            cmdUpdateComp.CommandText = @"
                UPDATE ComprobantesPago 
                SET EstadoPago = 'Verificado',
                    FechaValidacion = DATETIME('now'),
                    TesoreroId = @tesoreroId,
                    MotivoRechazo = @observacion 
                WHERE ComprobanteId = @comprobanteId;";
            
            AddParameter(cmdUpdateComp, "@tesoreroId", tesoreroId);
            AddParameter(cmdUpdateComp, "@observacion", (object?)observacion ?? DBNull.Value);
            AddParameter(cmdUpdateComp, "@comprobanteId", comprobanteId);
            
            if (cmdUpdateComp is DbCommand dbCmdUpd) await dbCmdUpd.ExecuteNonQueryAsync(); else cmdUpdateComp.ExecuteNonQuery();

            // 3. Crear tabla RecibosCaja dinámicamente si no existe
            using var cmdCreateTable = conn.CreateCommand();
            cmdCreateTable.Transaction = tx;
            cmdCreateTable.CommandText = @"
                CREATE TABLE IF NOT EXISTS RecibosCaja (
                    ReciboId INTEGER PRIMARY KEY AUTOINCREMENT,
                    ComprobanteId INTEGER NOT NULL,
                    Serie TEXT NOT NULL,
                    Correlativo INTEGER NOT NULL,
                    NumeroRecibo TEXT NOT NULL UNIQUE,
                    MontoTotal REAL NOT NULL,
                    FechaEmision TEXT NOT NULL,
                    TesoreroId INTEGER NOT NULL,
                    FOREIGN KEY (ComprobanteId) REFERENCES ComprobantesPago(ComprobanteId)
                );";
            if (cmdCreateTable is DbCommand dbCmdCT) await dbCmdCT.ExecuteNonQueryAsync(); else cmdCreateTable.ExecuteNonQuery();

            // 4. Generar número correlativo inmutable de recibo oficial
            string serieRecibo = $"REC-{DateTime.UtcNow.Year}";
            using var cmdCorr = conn.CreateCommand();
            cmdCorr.Transaction = tx;
            cmdCorr.CommandText = "SELECT IFNULL(MAX(Correlativo), 0) + 1 FROM RecibosCaja WHERE Serie = @serie;";
            AddParameter(cmdCorr, "@serie", serieRecibo);
            
            var corrResult = cmdCorr is DbCommand dbCmdC ? await dbCmdC.ExecuteScalarAsync() : cmdCorr.ExecuteScalar();
            int nuevoCorrelativo = Convert.ToInt32(corrResult);
            string numeroReciboOficial = $"{serieRecibo}-{nuevoCorrelativo:D6}";

            // 5. Insertar Recibo de Caja
            using var cmdRecibo = conn.CreateCommand();
            cmdRecibo.Transaction = tx;
            cmdRecibo.CommandText = @"
                INSERT INTO RecibosCaja (ComprobanteId, Serie, Correlativo, NumeroRecibo, MontoTotal, FechaEmision, TesoreroId)
                VALUES (@comprobanteId, @serie, @correlativo, @numeroRecibo, @monto, DATETIME('now'), @tesoreroId);";
            
            AddParameter(cmdRecibo, "@comprobanteId", comprobanteId);
            AddParameter(cmdRecibo, "@serie", serieRecibo);
            AddParameter(cmdRecibo, "@correlativo", nuevoCorrelativo);
            AddParameter(cmdRecibo, "@numeroRecibo", numeroReciboOficial);
            AddParameter(cmdRecibo, "@monto", monto);
            AddParameter(cmdRecibo, "@tesoreroId", tesoreroId);
            
            if (cmdRecibo is DbCommand dbCmdR) await dbCmdR.ExecuteNonQueryAsync(); else cmdRecibo.ExecuteNonQuery();

            // 6. Registrar en bitácora de auditoría
            await _auditoriaService.RegistrarEventoAsync(conn, tx, tesoreroId, "VALIDACION_PAGO_EMISION_RECIBO", "Tesorería", $"ComprobanteId: {comprobanteId}");

            tx.Commit();
            return numeroReciboOficial;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private void AddParameter(IDbCommand cmd, string name, object value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        cmd.Parameters.Add(param);
    }
}
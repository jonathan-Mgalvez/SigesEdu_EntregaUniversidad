using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using SigesEdu.Domain.Entities;
using SigesEdu.Domain.Exceptions;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Application.Services;

public class CalificacionesAppService : ICalificacionesAppService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICalendarioEscolarService _calendarioService;
    private readonly IAuditoriaService _auditoriaService;

    public CalificacionesAppService(
        IDbConnectionFactory connectionFactory,
        ICalendarioEscolarService calendarioService,
        IAuditoriaService auditoriaService)
    {
        _connectionFactory = connectionFactory;
        _calendarioService = calendarioService;
        _auditoriaService = auditoriaService;
    }

    public async Task RegistrarNotaDocenteAsync(int cursoId, string periodo, int estudianteId, decimal notaParcial, decimal notaFinal)
    {
        if (notaParcial < 0.0m || notaParcial > 100.0m || notaFinal < 0.0m || notaFinal > 100.0m)
            throw new InvalidOperationException("Las notas deben ubicarse estrictamente en el rango de 0.0 a 100.0.");

        DateTime ahora = DateTime.UtcNow;
        if (!_calendarioService.EsVentanaCalificacionesAbierta(periodo, ahora))
            throw new PeriodoEvaluacionCerradoException("Operación denegada (RN-03): Fecha fuera del calendario escolar habilitado.");

        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        using var tx = conn.BeginTransaction();
        try
        {
            using var cmdEnsureActa = conn.CreateCommand();
            cmdEnsureActa.Transaction = tx;
            cmdEnsureActa.CommandText = @"
                INSERT INTO ActasCalificaciones (CursoId, Periodo, EsCerrada)
                SELECT @cursoId, @periodo, 0
                WHERE NOT EXISTS (SELECT 1 FROM ActasCalificaciones WHERE CursoId = @cursoId AND Periodo = @periodo);";
            AddParameter(cmdEnsureActa, "@cursoId", cursoId);
            AddParameter(cmdEnsureActa, "@periodo", periodo);
            if (cmdEnsureActa is DbCommand dbCmdActa) await dbCmdActa.ExecuteNonQueryAsync(); else cmdEnsureActa.ExecuteNonQuery();

            using var cmdCheck = conn.CreateCommand();
            cmdCheck.Transaction = tx;
            cmdCheck.CommandText = "SELECT EsCerrada FROM ActasCalificaciones WHERE CursoId = @cursoId AND Periodo = @periodo;";
            AddParameter(cmdCheck, "@cursoId", cursoId);
            AddParameter(cmdCheck, "@periodo", periodo);

            var esCerrada = Convert.ToInt32(cmdCheck is DbCommand dbCmdSel ? await dbCmdSel.ExecuteScalarAsync() : cmdCheck.ExecuteScalar() ?? 0);
            if (esCerrada == 1)
                throw new ActaCerradaException("Operación denegada (RN-05): Las calificaciones de este curso ya han sido selladas.");

            using var cmdUpsert = conn.CreateCommand();
            cmdUpsert.Transaction = tx;
            cmdUpsert.CommandText = @"
                INSERT INTO Calificaciones (ActaId, EstudianteId, NotaParcial, NotaFinal, EsAprobado)
                VALUES (
                    (SELECT ActaId FROM ActasCalificaciones WHERE CursoId = @cursoId AND Periodo = @periodo LIMIT 1),
                    @estudianteId, @notaParcial, @notaFinal, CASE WHEN @notaFinal >= 60.0 THEN 1 ELSE 0 END
                )
                ON CONFLICT(ActaId, EstudianteId) DO UPDATE SET
                    NotaParcial = excluded.NotaParcial,
                    NotaFinal = excluded.NotaFinal,
                    EsAprobado = excluded.EsAprobado;";

            AddParameter(cmdUpsert, "@cursoId", cursoId);
            AddParameter(cmdUpsert, "@periodo", periodo);
            AddParameter(cmdUpsert, "@estudianteId", estudianteId);
            AddParameter(cmdUpsert, "@notaParcial", notaParcial);
            AddParameter(cmdUpsert, "@notaFinal", notaFinal);

            if (cmdUpsert is DbCommand dbCmdUps) await dbCmdUps.ExecuteNonQueryAsync(); else cmdUpsert.ExecuteNonQuery();

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<string> CerrarActaOficialAsync(int actaId, int coordinadorId)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        using var tx = conn.BeginTransaction();
        try
        {
            // 1. Cargar metadatos del acta
            using var cmdActa = conn.CreateCommand();
            cmdActa.Transaction = tx;
            cmdActa.CommandText = "SELECT CursoId, Periodo, EsCerrada FROM ActasCalificaciones WHERE ActaId = @actaId;";
            AddParameter(cmdActa, "@actaId", actaId);

            using var readerActa = cmdActa is DbCommand dbCmdActa ? await dbCmdActa.ExecuteReaderAsync() : cmdActa.ExecuteReader();
            if (!readerActa.Read()) throw new InvalidOperationException("El acta no existe en el sistema.");
            
            int cursoId = Convert.ToInt32(readerActa["CursoId"]);
            string periodo = readerActa["Periodo"].ToString()!;
            bool estaCerrada = Convert.ToInt32(readerActa["EsCerrada"]) == 1;
            readerActa.Close();

            if (estaCerrada) throw new ActaCerradaException("El acta ya ha sido cerrada previamente.");

            var acta = new ActaCalificaciones(cursoId, periodo);
            typeof(ActaCalificaciones).GetProperty("ActaId")?.SetValue(acta, actaId);

            // 2. Cargar calificaciones de los estudiantes para el hash
            using var cmdCalif = conn.CreateCommand();
            cmdCalif.Transaction = tx;
            cmdCalif.CommandText = "SELECT EstudianteId, NotaParcial, NotaFinal FROM Calificaciones WHERE ActaId = @actaId;";
            AddParameter(cmdCalif, "@actaId", actaId);

            using var readerCalif = cmdCalif is DbCommand dbCmdCalif ? await dbCmdCalif.ExecuteReaderAsync() : cmdCalif.ExecuteReader();
            while (readerCalif.Read())
            {
                acta.RegistrarCalificacion(
                    Convert.ToInt32(readerCalif["EstudianteId"]),
                    Convert.ToDecimal(readerCalif["NotaParcial"]),
                    Convert.ToDecimal(readerCalif["NotaFinal"])
                );
            }
            readerCalif.Close();

            // 3. Contar la capacidad/ocupación de la sección para validar que no falten notas
            using var cmdOcupados = conn.CreateCommand();
            cmdOcupados.Transaction = tx;
            cmdOcupados.CommandText = @"
                SELECT s.CuposOcupados 
                FROM Cursos c
                INNER JOIN Secciones s ON c.SeccionId = s.SeccionId
                WHERE c.CursoId = @cursoId;";
            AddParameter(cmdOcupados, "@cursoId", cursoId);
            
            int totalMatriculados = Convert.ToInt32(cmdOcupados is DbCommand dbCmdOc ? await dbCmdOc.ExecuteScalarAsync() : cmdOcupados.ExecuteScalar());

            // 4. Ejecutar la regla de negocio de inmutabilidad (Calcula el Hash SHA-256 interno)
            acta.EjecutarCierreInmutable(coordinadorId, totalMatriculados);

            // 5. Persistir el sello en la BD
            using var cmdUpdate = conn.CreateCommand();
            cmdUpdate.Transaction = tx;
            cmdUpdate.CommandText = @"
                UPDATE ActasCalificaciones 
                SET EsCerrada = 1,
                    FechaCierre = @fechaCierre,
                    CoordinadorId = @coordinadorId,
                    FirmaHash = @firmaHash
                WHERE ActaId = @actaId AND EsCerrada = 0;";
            AddParameter(cmdUpdate, "@fechaCierre", acta.FechaCierre!.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            AddParameter(cmdUpdate, "@coordinadorId", coordinadorId);
            AddParameter(cmdUpdate, "@firmaHash", acta.FirmaHash);
            AddParameter(cmdUpdate, "@actaId", actaId);

            int rows = cmdUpdate is DbCommand dbCmdUpd ? await dbCmdUpd.ExecuteNonQueryAsync() : cmdUpdate.ExecuteNonQuery();
            if (rows == 0) throw new InvalidOperationException("Fallo al persistir el cierre del acta.");

            // 6. Registrar en bitácora de auditoría
            await _auditoriaService.RegistrarEventoAsync(conn, tx, coordinadorId, "CIERRE_ACTA_INMUTABLE", "ControlAcadémico", $"ActaId: {actaId}, Hash: {acta.FirmaHash}");

            tx.Commit();
            return acta.FirmaHash!;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
    private void AddParameter(IDbCommand cmd, string name, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
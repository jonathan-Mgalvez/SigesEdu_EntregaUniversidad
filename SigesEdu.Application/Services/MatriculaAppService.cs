using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using SigesEdu.Domain.Enums;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Application.Services;

public class MatriculaAppService : IMatriculaAppService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IAuditoriaService _auditoriaService;
    private readonly ICupoTransaccionalService _cupoService;
    private readonly IMotorEstadosMatriculaService _motorEstados;

    public MatriculaAppService(
        IDbConnectionFactory connectionFactory,
        IAuditoriaService auditoriaService,
        ICupoTransaccionalService cupoService,
        IMotorEstadosMatriculaService motorEstados)
    {
        _connectionFactory = connectionFactory;
        _auditoriaService = auditoriaService;
        _cupoService = cupoService;
        _motorEstados = motorEstados;
    }

    public async Task<string> FormalizarMatriculaAsync(int solicitudId, int tesoreroId)
    {
        using var conn = _connectionFactory.CreateConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        // Inicio de la transacción atómica (RN-04)
        using var tx = conn.BeginTransaction();
        try
        {
            // 1. Extraer datos actuales de la solicitud y el cupo reservado
            using var cmdSelect = conn.CreateCommand();
            cmdSelect.Transaction = tx;
            cmdSelect.CommandText = @"
                SELECT s.EstudianteId, s.SeccionId, s.EstadoMatricula, c.CupoId
                FROM SolicitudesMatricula s
                INNER JOIN Cupos c ON s.SolicitudId = c.SolicitudId
                WHERE s.SolicitudId = @solicitudId;";
            AddParameter(cmdSelect, "@solicitudId", solicitudId);

            using var reader = cmdSelect is DbCommand dbCmdSel ? await dbCmdSel.ExecuteReaderAsync() : cmdSelect.ExecuteReader();
            if (!reader.Read())
                throw new InvalidOperationException("No se encontró la solicitud o no tiene un cupo reservado asociado.");

            int estudianteId = Convert.ToInt32(reader["EstudianteId"]);
            int seccionId = Convert.ToInt32(reader["SeccionId"]);
            string estadoDb = reader["EstadoMatricula"].ToString()!;
            int cupoId = Convert.ToInt32(reader["CupoId"]);
            reader.Close();

            // 2. Validar transición con el Motor de Estados (FSM)
            if (!Enum.TryParse<EstadoMatricula>(estadoDb, out var estadoActual))
                throw new InvalidOperationException("Estado de matrícula desconocido en la base de datos.");

            _motorEstados.ValidarTransicion(estadoActual, EstadoMatricula.MatriculaActiva);

            // 3. Consolidar el Cupo Definitivo (invoca al servicio de concurrencia inyectando la transacción)
            await _cupoService.ConsolidarCupoDefinitivoAsync(conn, tx, cupoId, seccionId);

            // 4. Generar Código de Carné Institucional
            string anioActual = DateTime.UtcNow.Year.ToString();
            string carnet = $"{anioActual}-{estudianteId:D4}";

            // 5. Actualizar estado de la Solicitud a Matrícula Activa
            using var cmdUpdateSol = conn.CreateCommand();
            cmdUpdateSol.Transaction = tx;
            cmdUpdateSol.CommandText = @"
                UPDATE SolicitudesMatricula 
                SET EstadoMatricula = 'MatriculaActiva'
                WHERE SolicitudId = @solicitudId;";
            AddParameter(cmdUpdateSol, "@solicitudId", solicitudId);
            if (cmdUpdateSol is DbCommand dbCmdUpdSol) await dbCmdUpdSol.ExecuteNonQueryAsync(); else cmdUpdateSol.ExecuteNonQuery();

            // 6. Actualizar el Estudiante asignando el Carné y el estado 'Activo'
            using var cmdUpdateEst = conn.CreateCommand();
            cmdUpdateEst.Transaction = tx;
            cmdUpdateEst.CommandText = @"
                UPDATE Estudiantes 
                SET CodigoCarnet = @carnet,
                    EstadoAcademico = 'Activo'
                WHERE EstudianteId = @estudianteId;";
            AddParameter(cmdUpdateEst, "@carnet", carnet);
            AddParameter(cmdUpdateEst, "@estudianteId", estudianteId);
            if (cmdUpdateEst is DbCommand dbCmdUpdEst) await dbCmdUpdEst.ExecuteNonQueryAsync(); else cmdUpdateEst.ExecuteNonQuery();

            // 7. Registrar evento inmutable de auditoría
            await _auditoriaService.RegistrarEventoAsync(conn, tx, tesoreroId, "FORMALIZACION_MATRICULA", "Admisiones", $"SolicitudId: {solicitudId}, Carné: {carnet}");

            // 8. Confirmar todos los cambios (Commit)
            tx.Commit();

            return carnet;
        }
        catch
        {
            // Revertir todos los cambios si algo falla
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
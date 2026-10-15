using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SigesEdu.Application.Services;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Tests.Integration;

// 1. Stub actualizado con las firmas exactas de la interfaz
public class AuditoriaFallaSimulada : IAuditoriaService
{
    public Task RegistrarEventoAsync(IDbConnection conn, IDbTransaction tx, int? usuarioId, string accion, string modulo, string detalle, string? ip = null)
    {
        throw new Exception("Falla catastrófica simulada de servidor al registrar auditoría.");
    }

    public Task RegistrarEventoIndependienteAsync(int? usuarioId, string accion, string modulo, string detalle, string? ip = null)
    {
        throw new Exception("Falla catastrófica simulada.");
    }
}

public class AtomicidadTransaccionalTests : IDisposable
{
    private readonly string _connectionString = "Data Source=SigesEdu_Test_ACID.db;Mode=ReadWriteCreate;";
    private readonly ITesoreriaAppService _tesoreriaService;

    public AtomicidadTransaccionalTests()
    {
        InicializarBaseDatosPrueba();
        
        var connectionFactory = new TestDbConnectionFactory(_connectionString); 
        var auditoriaFalla = new AuditoriaFallaSimulada();
        
        _tesoreriaService = new TesoreriaAppService(connectionFactory, auditoriaFalla);
    }

    [Fact]
    public async Task ValidarPago_FalloEnUltimoPaso_EjecutaRollbackYPreservaEstadoAnterior()
    {
        // Arrange
        int comprobanteId = 1;
        int tesoreroId = 2;
        string observacion = "Pago válido, forzando rollback";

        // Act
        Func<Task> act = async () => await _tesoreriaService.ValidarPagoYGenerarReciboAsync(comprobanteId, tesoreroId, observacion);

        // Assert 1: Debe lanzar la excepción que forzamos
        await act.Should().ThrowAsync<Exception>().WithMessage("Falla catastrófica simulada de servidor al registrar auditoría.");

        // Assert 2: Verificamos en BD que el Rollback se ejecutó (Nada cambió)
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EstadoPago FROM ComprobantesPago WHERE ComprobanteId = 1;";
        
        // Corrección de la advertencia CS8600 permitiendo nulos
        string? estadoComprobante = (string?)await cmd.ExecuteScalarAsync();

        cmd.CommandText = "SELECT COUNT(*) FROM RecibosCaja;";
        var totalRecibos = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        // Verificaciones con FluentAssertions
        estadoComprobante.Should().Be("En Revision", "porque la transacción entera debió revertirse.");
        totalRecibos.Should().Be(0, "porque el recibo generado se descartó en el Rollback.");
    }

    private void InicializarBaseDatosPrueba()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            DROP TABLE IF EXISTS RecibosCaja;
            DROP TABLE IF EXISTS ComprobantesPago;

            CREATE TABLE ComprobantesPago (
                ComprobanteId INTEGER PRIMARY KEY AUTOINCREMENT,
                SolicitudId INTEGER NOT NULL,
                NumeroBoleta TEXT NOT NULL UNIQUE,
                Monto REAL NOT NULL,
                EstadoPago TEXT NOT NULL,
                FechaValidacion TEXT,
                TesoreroId INTEGER,
                MotivoRechazo TEXT
            );
            
            CREATE TABLE RecibosCaja (
                ReciboId INTEGER PRIMARY KEY AUTOINCREMENT,
                ComprobanteId INTEGER NOT NULL,
                Serie TEXT NOT NULL,
                Correlativo INTEGER NOT NULL,
                NumeroRecibo TEXT NOT NULL UNIQUE,
                MontoTotal REAL NOT NULL,
                FechaEmision TEXT NOT NULL,
                TesoreroId INTEGER NOT NULL
            );

            INSERT INTO ComprobantesPago (ComprobanteId, SolicitudId, NumeroBoleta, Monto, EstadoPago)
            VALUES (1, 100, 'BOL-999', 850.0, 'En Revision');";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
    }
}
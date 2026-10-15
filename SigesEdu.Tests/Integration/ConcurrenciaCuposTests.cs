using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SigesEdu.Infrastructure.Persistence;
using SigesEdu.Domain.Exceptions;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Tests.Integration;

// 1. Clase auxiliar para inyectar la conexión en la prueba
public class TestDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    public TestDbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}

public class ConcurrenciaCuposTests : IDisposable
{
    private readonly string _connectionString = "Data Source=SigesEdu_Test_Concurrencia.db;Mode=ReadWriteCreate;";
    private readonly CupoTransaccionalService _cupoService;

    public ConcurrenciaCuposTests()
    {
        // 2. Usar la clase auxiliar en lugar del string directo
        var connectionFactory = new TestDbConnectionFactory(_connectionString);
        _cupoService = new CupoTransaccionalService(connectionFactory);
        
        InicializarBaseDatosPrueba();
    }

    [Fact]
    public async Task ReservarCupo_DiezHilosCompitiendoPorUnCupo_SoloUnoTieneExitoYNueveRechazados()
    {
        // Arrange: Sección con exactamente 1 cupo disponible
        int seccionId = 101;
        int totalHilos = 10;
        var tareas = new Task<int>[totalHilos];
        int exitos = 0;
        int rechazosCupoAgotado = 0;

        // Act: Disparo simultáneo de 10 tareas concurrentes
        for (int i = 0; i < totalHilos; i++)
        {
            int solicitudId = 1000 + i;
            tareas[i] = Task.Run(async () =>
            {
                try
                {
                    int cupoId = await _cupoService.ReservarCupoTemporalAsync(seccionId, solicitudId);
                    return cupoId;
                }
                catch (CupoAgotadoException)
                {
                    return -1; // Rechazado formalmente por regla de negocio
                }
                catch (Exception ex) when (ex.Message.Contains("locked") || ex.Message.Contains("concurrencia"))
                {
                    return -1; // Rechazado por colisión en SQLite
                }
            });
        }

        int[] resultados = await Task.WhenAll(tareas);

        // Assert: Verificación de exclusión mutua estricta
        exitos = resultados.Count(r => r > 0);
        rechazosCupoAgotado = resultados.Count(r => r == -1);

        exitos.Should().Be(1, "exactamente una solicitud debe obtener la vacante disponible.");
        rechazosCupoAgotado.Should().Be(9, "los nueve intentos restantes deben ser denegados.");
    }

    private void InicializarBaseDatosPrueba()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            DROP TABLE IF EXISTS Cupos;
            CREATE TABLE Cupos (
                CupoId INTEGER PRIMARY KEY AUTOINCREMENT,
                SeccionId INTEGER NOT NULL,
                SolicitudId INTEGER,
                NumeroCupo INTEGER NOT NULL,
                EstadoCupo TEXT NOT NULL,
                BloqueoHasta TEXT
            );
            INSERT INTO Cupos (CupoId, SeccionId, SolicitudId, NumeroCupo, EstadoCupo, BloqueoHasta)
            VALUES (1, 101, NULL, 1, 'Disponible', NULL);";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
    }
}
using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace SigesEdu.Tests.Integration;

public class InmutabilidadActasTests : IDisposable
{
    private readonly string _connectionString = "Data Source=SigesEdu_Test_RN05.db;Mode=ReadWriteCreate;";

    public InmutabilidadActasTests()
    {
        InicializarBaseDatosPrueba();
    }

    [Fact]
    public async Task ModificarNota_ActaYaCerrada_TriggerSQLiteAbortaTransaccionYBloqueaEdicion()
    {
        // Arrange: Preparamos un intento de alteración directa a la base de datos
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        using var cmd = conn.CreateCommand();
        // Intentamos subirle la nota al estudiante de 75.0 a 95.0 en un acta sellada
        cmd.CommandText = "UPDATE Calificaciones SET NotaFinal = 95.0 WHERE CalificacionId = 1;";

        // Act
        Func<Task> act = async () => await cmd.ExecuteNonQueryAsync();

        // Assert: El Trigger de SQLite debe interceptar y abortar lanzando la infracción RN-05
        var excepcion = await act.Should().ThrowAsync<SqliteException>();
        excepcion.WithMessage("*Violación RN-05: No se permite modificar calificaciones de un acta cerrada e inmutable.*");
    }

    private void InicializarBaseDatosPrueba()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            DROP TABLE IF EXISTS Calificaciones;
            DROP TABLE IF EXISTS ActasCalificaciones;

            CREATE TABLE ActasCalificaciones (
                ActaId INTEGER PRIMARY KEY AUTOINCREMENT,
                CursoId INTEGER NOT NULL,
                Periodo TEXT NOT NULL,
                EsCerrada INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE Calificaciones (
                CalificacionId INTEGER PRIMARY KEY AUTOINCREMENT,
                ActaId INTEGER NOT NULL,
                EstudianteId INTEGER NOT NULL,
                NotaParcial REAL NOT NULL,
                NotaFinal REAL NOT NULL,
                EsAprobado INTEGER NOT NULL,
                FOREIGN KEY (ActaId) REFERENCES ActasCalificaciones(ActaId)
            );

            -- Replicamos el Trigger exacto del DDL de producción
            CREATE TRIGGER trg_bloqueo_calificaciones_acta_cerrada
            BEFORE UPDATE ON Calificaciones
            FOR EACH ROW
            WHEN (SELECT EsCerrada FROM ActasCalificaciones WHERE ActaId = OLD.ActaId) = 1
            BEGIN
                SELECT RAISE(ABORT, 'Violación RN-05: No se permite modificar calificaciones de un acta cerrada e inmutable.');
            END;

            -- Inserción de Datos: 1 Acta Cerrada (EsCerrada = 1) y 1 Calificación asociada
            INSERT INTO ActasCalificaciones (ActaId, CursoId, Periodo, EsCerrada) VALUES (1, 10, 'Bimestre 1', 1);
            INSERT INTO Calificaciones (CalificacionId, ActaId, EstudianteId, NotaParcial, NotaFinal, EsAprobado) VALUES (1, 1, 100, 70.0, 75.0, 1);
        ";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
    }
}
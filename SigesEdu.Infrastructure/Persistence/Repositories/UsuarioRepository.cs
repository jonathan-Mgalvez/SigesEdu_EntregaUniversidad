using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SigesEdu.Domain.Entities;
using SigesEdu.Domain.Enums;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Infrastructure.Persistence.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UsuarioRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Usuario?> ObtenerPorUsernameAsync(string username)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = (SqliteCommand)connection.CreateCommand();
        // COLLATE NOCASE permite buscar sin importar mayúsculas/minúsculas
        cmd.CommandText = "SELECT * FROM Usuarios WHERE Username = @username COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@username", username);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapearUsuario(reader);
        }
        return null;
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int usuarioId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = (SqliteCommand)connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Usuarios WHERE UsuarioId = @usuarioId;";
        cmd.Parameters.AddWithValue("@usuarioId", usuarioId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapearUsuario(reader);
        }
        return null;
    }

    public async Task<int> CrearAsync(Usuario usuario)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = (SqliteCommand)connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Usuarios (Username, PasswordHash, Salt, Rol, Activo, IntentosFallidos, FechaCreacion)
            VALUES (@username, @passwordHash, @salt, @rol, @activo, @intentosFallidos, @fechaCreacion);
            SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@username", usuario.Username);
        cmd.Parameters.AddWithValue("@passwordHash", usuario.PasswordHash);
        cmd.Parameters.AddWithValue("@salt", usuario.Salt);
        cmd.Parameters.AddWithValue("@rol", usuario.Rol.ToString());
        cmd.Parameters.AddWithValue("@activo", usuario.Activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@intentosFallidos", usuario.IntentosFallidos);
        cmd.Parameters.AddWithValue("@fechaCreacion", usuario.FechaCreacion.ToString("yyyy-MM-dd HH:mm:ss"));

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task ActualizarAsync(Usuario usuario)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = (SqliteCommand)connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Usuarios 
            SET Activo = @activo, 
                IntentosFallidos = @intentosFallidos, 
                BloqueadoHasta = @bloqueoHasta, 
                UltimoAcceso = @ultimoAcceso
            WHERE UsuarioId = @usuarioId;";

        cmd.Parameters.AddWithValue("@activo", usuario.Activo ? 1 : 0);
        cmd.Parameters.AddWithValue("@intentosFallidos", usuario.IntentosFallidos);
        cmd.Parameters.AddWithValue("@bloqueoHasta", usuario.BloqueoHasta.HasValue ? usuario.BloqueoHasta.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ultimoAcceso", usuario.UltimoAcceso.HasValue ? usuario.UltimoAcceso.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value);
        cmd.Parameters.AddWithValue("@usuarioId", usuario.UsuarioId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<Usuario>> ObtenerPorRolAsync(RolUsuario rol)
    {
        var lista = new List<Usuario>();
        using var connection = _connectionFactory.CreateConnection();
        using var cmd = (SqliteCommand)connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Usuarios WHERE Rol = @rol;";
        cmd.Parameters.AddWithValue("@rol", rol.ToString());

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add(MapearUsuario(reader));
        }
        return lista;
    }

    // Método auxiliar para reconstruir la Entidad Rica desde SQLite
    private Usuario MapearUsuario(SqliteDataReader reader)
    {
        Enum.TryParse<RolUsuario>(reader["Rol"].ToString(), out var rol);

        var usuario = new Usuario(
            reader["Username"].ToString()!,
            reader["PasswordHash"].ToString()!,
            reader["Salt"].ToString()!,
            rol
        );

        // Mapeo por reflexión para respetar el encapsulamiento de la Entidad Rica
        typeof(Usuario).GetProperty("UsuarioId")?.SetValue(usuario, Convert.ToInt32(reader["UsuarioId"]));
        typeof(Usuario).GetProperty("Activo")?.SetValue(usuario, Convert.ToInt32(reader["Activo"]) == 1);
        typeof(Usuario).GetProperty("IntentosFallidos")?.SetValue(usuario, Convert.ToInt32(reader["IntentosFallidos"]));

        if (reader["BloqueadoHasta"] != DBNull.Value)
            typeof(Usuario).GetProperty("BloqueoHasta")?.SetValue(usuario, Convert.ToDateTime(reader["BloqueadoHasta"]));

        typeof(Usuario).GetProperty("FechaCreacion")?.SetValue(usuario, Convert.ToDateTime(reader["FechaCreacion"]));

        if (reader["UltimoAcceso"] != DBNull.Value)
            typeof(Usuario).GetProperty("UltimoAcceso")?.SetValue(usuario, Convert.ToDateTime(reader["UltimoAcceso"]));

        return usuario;
    }
}
using System;
using SigesEdu.Domain.Enums;

namespace SigesEdu.Domain.Entities;

public class Usuario
{
    public int UsuarioId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Salt { get; private set; } = string.Empty;
    public RolUsuario Rol { get; private set; }
    public bool Activo { get; private set; }
    
    // Propiedades Anti-Fuerza Bruta
    public int IntentosFallidos { get; private set; }
    public DateTime? BloqueoHasta { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public DateTime? UltimoAcceso { get; private set; }

    // Constructor vacío para uso del ORM (Dapper/EF)
    protected Usuario() { }

    public Usuario(string username, string passwordHash, string salt, RolUsuario rol)
    {
        if (string.IsNullOrWhiteSpace(username)) 
            throw new ArgumentException("El nombre de usuario es obligatorio.");

        Username = username;
        PasswordHash = passwordHash;
        Salt = salt;
        Rol = rol;
        Activo = true;
        IntentosFallidos = 0;
        FechaCreacion = DateTime.UtcNow;
    }

    // Métodos de Negocio Encapsulados
    public void RegistrarAccesoExitoso()
    {
        IntentosFallidos = 0;
        BloqueoHasta = null;
        UltimoAcceso = DateTime.UtcNow;
    }

    public void RegistrarIntentoFallido(int maxIntentosPermitidos, int minutosDeBloqueo)
    {
        IntentosFallidos++;
        if (IntentosFallidos >= maxIntentosPermitidos)
        {
            BloqueoHasta = DateTime.UtcNow.AddMinutes(minutosDeBloqueo);
        }
    }

    public bool TieneAccesoBloqueado()
    {
        if (!Activo) return true;
        if (BloqueoHasta.HasValue && BloqueoHasta.Value > DateTime.UtcNow) return true;
        
        return false;
    }

    public void RestablecerAccesos()
    {
        IntentosFallidos = 0;
        BloqueoHasta = null;
        Activo = true;
    }

    public void SuspenderCuenta()
    {
        Activo = false;
    }
}
using System;
using System.Threading.Tasks;
using SigesEdu.Application.DTOs;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditoriaService _auditoriaService;

    public AuthService(
        IUsuarioRepository usuarioRepository, 
        IPasswordHasher passwordHasher, 
        IAuditoriaService auditoriaService)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _auditoriaService = auditoriaService;
    }

    public async Task<UserSessionDto?> IniciarSesionAsync(string username, string password, string ipAddress)
    {
        // 1. Buscar usuario
        var usuario = await _usuarioRepository.ObtenerPorUsernameAsync(username);

        if (usuario == null)
        {
            await _auditoriaService.RegistrarEventoIndependienteAsync(null, "LOGIN_FALLIDO", "Seguridad", $"Usuario inexistente: {username}", ipAddress);
            return null;
        }

        // 2. Verificar estado de la cuenta (Bloqueos)
        if (usuario.TieneAccesoBloqueado())
        {
            await _auditoriaService.RegistrarEventoIndependienteAsync(usuario.UsuarioId, "LOGIN_BLOQUEADO", "Seguridad", "Intento en cuenta bloqueada o inactiva", ipAddress);
            throw new UnauthorizedAccessException("La cuenta está bloqueada temporalmente o inactiva. Contacte al administrador.");
        }

        // 3. Verificación Criptográfica
        bool esValido = _passwordHasher.VerificarHash(password, usuario.PasswordHash, usuario.Salt);

        if (!esValido)
        {
            usuario.RegistrarIntentoFallido(maxIntentosPermitidos: 5, minutosDeBloqueo: 15);
            await _usuarioRepository.ActualizarAsync(usuario);
            await _auditoriaService.RegistrarEventoIndependienteAsync(usuario.UsuarioId, "LOGIN_FALLIDO", "Seguridad", "Contraseña incorrecta", ipAddress);
            return null;
        }

        // 4. Acceso Exitoso
        usuario.RegistrarAccesoExitoso();
        await _usuarioRepository.ActualizarAsync(usuario);
        await _auditoriaService.RegistrarEventoIndependienteAsync(usuario.UsuarioId, "LOGIN_EXITOSO", "Seguridad", $"Sesión iniciada con rol {usuario.Rol}", ipAddress);

        return new UserSessionDto
        {
            UsuarioId = usuario.UsuarioId,
            Username = usuario.Username,
            Rol = usuario.Rol,
            Token = Guid.NewGuid().ToString() // Token temporal para el manejo de sesión
        };
    }
}
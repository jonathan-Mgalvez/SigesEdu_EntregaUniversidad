using SigesEdu.Domain.Enums;

namespace SigesEdu.Application.DTOs;

public class UserSessionDto
{
    public int UsuarioId { get; set; }
    public string Username { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public string Token { get; set; } = string.Empty; 
}
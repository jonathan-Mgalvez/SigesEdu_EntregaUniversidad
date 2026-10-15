using SigesEdu.Domain.Entities;
using SigesEdu.Domain.Enums;

namespace SigesEdu.Domain.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorIdAsync(int usuarioId);
    Task<Usuario?> ObtenerPorUsernameAsync(string username);
    Task<int> CrearAsync(Usuario usuario);
    Task ActualizarAsync(Usuario usuario);
    Task<IEnumerable<Usuario>> ObtenerPorRolAsync(RolUsuario rol);
}
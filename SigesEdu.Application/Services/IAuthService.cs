using System.Threading.Tasks;
using SigesEdu.Application.DTOs;

namespace SigesEdu.Application.Services;

public interface IAuthService
{
    Task<UserSessionDto?> IniciarSesionAsync(string username, string password, string ipAddress);
}
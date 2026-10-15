using System.Threading.Tasks;

namespace SigesEdu.Application.Services;

public interface IMatriculaAppService
{
    Task<string> FormalizarMatriculaAsync(int solicitudId, int tesoreroId);
}
using System.Threading.Tasks;

namespace SigesEdu.Application.Services;

public interface IReporteAcademicoAppService
{
    Task<byte[]> ObtenerBoletaEstudianteAsync(string carnet);
    Task<byte[]> ObtenerActaOficialAsync(int actaId);
}
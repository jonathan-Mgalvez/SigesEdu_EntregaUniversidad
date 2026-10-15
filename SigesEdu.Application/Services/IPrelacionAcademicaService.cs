using System.Threading.Tasks;

namespace SigesEdu.Application.Services;

public interface IPrelacionAcademicaService
{
    Task ValidarHistorialAcademicoAsync(int estudianteId, bool tieneExcepcionAutorizada);
}
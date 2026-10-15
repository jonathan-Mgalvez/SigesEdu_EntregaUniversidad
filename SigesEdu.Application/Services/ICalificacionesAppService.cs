using System.Threading.Tasks;

namespace SigesEdu.Application.Services;

public interface ICalificacionesAppService
{
    Task RegistrarNotaDocenteAsync(int cursoId, string periodo, int estudianteId, decimal notaParcial, decimal notaFinal);
    Task<string> CerrarActaOficialAsync(int actaId, int coordinadorId);
}
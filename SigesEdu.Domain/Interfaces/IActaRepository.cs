using SigesEdu.Domain.Entities;

namespace SigesEdu.Domain.Interfaces;

public interface IActaRepository
{
    Task<ActaCalificaciones?> ObtenerPorIdAsync(int actaId);
    Task<ActaCalificaciones?> ObtenerPorCursoYPeriodoAsync(int cursoId, string periodo);
    Task<int> CrearActaAsync(ActaCalificaciones acta);
    Task CerrarActaInmutableAsync(int actaId, int coordinadorId, string firmaHash, DateTime fechaCierre);
    
    // Calificaciones
    Task RegistrarOActualizarNotaAsync(Calificacion calificacion);
    Task<IEnumerable<Calificacion>> ObtenerCalificacionesPorActaAsync(int actaId);
}
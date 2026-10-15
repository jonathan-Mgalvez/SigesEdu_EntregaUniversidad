using SigesEdu.Domain.Entities;
using SigesEdu.Domain.Enums;

namespace SigesEdu.Domain.Interfaces;

public interface ICupoRepository
{
    Task<Cupo?> ObtenerPorIdAsync(int cupoId);
    Task<Cupo?> ObtenerCupoDisponibleParaSeccionAsync(int seccionId);
    Task<int> ContarCuposDisponiblesAsync(int seccionId);
    Task ActualizarEstadoCupoAsync(int cupoId, EstadoCupo nuevoEstado, int? solicitudId, DateTime? bloqueoHasta);
    Task<IEnumerable<Cupo>> ObtenerCuposExpiradosAsync();
}
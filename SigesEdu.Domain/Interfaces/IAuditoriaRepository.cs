using SigesEdu.Domain.Entities;

namespace SigesEdu.Domain.Interfaces;

public interface IAuditoriaRepository
{
    Task RegistrarEventoAsync(AuditoriaEvento evento);
    Task<IEnumerable<AuditoriaEvento>> ObtenerEventosRecientesAsync(int limite = 100);
    Task<IEnumerable<AuditoriaEvento>> ObtenerPorTablaYRegistroAsync(string tabla, int registroId);
}
using SigesEdu.Domain.Enums;

namespace SigesEdu.Domain.Entities;

public class Cupo
{
    public int CupoId { get; set; }
    public int SeccionId { get; set; }
    public int? SolicitudId { get; set; }
    public int NumeroCupo { get; set; }
    public EstadoCupo EstadoCupo { get; set; } = EstadoCupo.Disponible;
    public DateTime? BloqueoHasta { get; set; }

    // Relaciones
    public Seccion? Seccion { get; set; }
    public SolicitudMatricula? Solicitud { get; set; }
}
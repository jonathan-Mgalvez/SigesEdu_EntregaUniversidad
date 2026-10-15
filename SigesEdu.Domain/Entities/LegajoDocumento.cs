namespace SigesEdu.Domain.Entities;

public class LegajoDocumento
{
    public int DocumentoId { get; set; }
    public int SolicitudId { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public string EstadoRevision { get; set; } = "Pendiente";
    public string? MotivoObservacion { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    // Relaciones
    public SolicitudMatricula? Solicitud { get; set; }
}
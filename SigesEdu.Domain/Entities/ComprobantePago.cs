namespace SigesEdu.Domain.Entities;

public class ComprobantePago
{
    public int ComprobanteId { get; set; }
    public int SolicitudId { get; set; }
    public string NumeroBoleta { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public DateTime FechaEmision { get; set; }
    public string RutaComprobante { get; set; } = string.Empty;
    public string EstadoPago { get; set; } = "En Revision";
    public string? MotivoRechazo { get; set; }
    public DateTime? FechaValidacion { get; set; }
    public int? TesoreroId { get; set; }

    // Relaciones
    public SolicitudMatricula? Solicitud { get; set; }
    public Usuario? Tesorero { get; set; }
}
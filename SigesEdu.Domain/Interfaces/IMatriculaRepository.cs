using SigesEdu.Domain.Entities;
using SigesEdu.Domain.Enums;

namespace SigesEdu.Domain.Interfaces;

public interface IMatriculaRepository
{
    Task<SolicitudMatricula?> ObtenerPorIdAsync(int solicitudId);
    Task<SolicitudMatricula?> ObtenerPorCodigoSeguimientoAsync(string codigoSeguimiento);
    Task<int> CrearSolicitudAsync(SolicitudMatricula solicitud);
    Task ActualizarEstadoAsync(int solicitudId, EstadoMatricula nuevoEstado, string? observaciones = null);
    Task<IEnumerable<SolicitudMatricula>> ObtenerPorEstadoAsync(EstadoMatricula estado);
    
    // Legajos y Comprobantes
    Task GuardarLegajoAsync(LegajoDocumento legajo);
    Task ActualizarRevisionLegajoAsync(int documentoId, string estadoRevision, string? observaciones);
    Task RegistrarComprobantePagoAsync(ComprobantePago comprobante);
    Task ActualizarEstadoComprobanteAsync(int comprobanteId, string estadoPago, int tesoreroId, string? motivoRechazo);
}
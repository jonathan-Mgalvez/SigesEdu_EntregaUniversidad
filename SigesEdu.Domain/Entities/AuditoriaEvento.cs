using System;

namespace SigesEdu.Domain.Entities;

public class AuditoriaEvento
{
    public int AuditoriaId { get; private set; }
    public int? UsuarioId { get; private set; } // Puede ser nulo si el ataque viene de un anónimo
    public string Accion { get; private set; } = string.Empty;
    public string Modulo { get; private set; } = string.Empty;
    public string Detalle { get; private set; } = string.Empty;
    public string? DireccionIP { get; private set; }
    public DateTime FechaHora { get; private set; }

    // Relaciones
    public Usuario? Usuario { get; private set; }

    protected AuditoriaEvento() { }

    public AuditoriaEvento(int? usuarioId, string accion, string modulo, string detalle, string? direccionIp)
    {
        if (string.IsNullOrWhiteSpace(accion)) throw new ArgumentNullException(nameof(accion));
        if (string.IsNullOrWhiteSpace(modulo)) throw new ArgumentNullException(nameof(modulo));

        UsuarioId = usuarioId;
        Accion = accion;
        Modulo = modulo;
        Detalle = detalle ?? string.Empty;
        DireccionIP = direccionIp;
        FechaHora = DateTime.UtcNow;
    }
}
namespace SigesEdu.Domain.Entities;

public class AnioLectivo
{
    public int AnioLectivoId { get; set; }
    public int AnioCalendario { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public bool EsActivo { get; set; }

    // Relaciones
    public ICollection<Grado> Grados { get; set; } = new List<Grado>();
}
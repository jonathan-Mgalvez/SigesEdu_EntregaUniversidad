using SigesEdu.Domain.Enums;

namespace SigesEdu.Domain.Entities;

public class Grado
{
    public int GradoId { get; set; }
    public int AnioLectivoId { get; set; }
    public NivelEducativo Nivel { get; set; }
    public string NombreGrado { get; set; } = string.Empty;

    // Relaciones
    public AnioLectivo? AnioLectivo { get; set; }
    public ICollection<Seccion> Secciones { get; set; } = new List<Seccion>();
}
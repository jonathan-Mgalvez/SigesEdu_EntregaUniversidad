namespace SigesEdu.Domain.Entities;

public class Curso
{
    public int CursoId { get; set; }
    public int SeccionId { get; set; }
    public int DocenteId { get; set; }
    public string NombreCurso { get; set; } = string.Empty;
    public decimal PonderacionTotal { get; set; } = 100.0m;

    // Relaciones
    public Seccion? Seccion { get; set; }
    public Usuario? Docente { get; set; }
    public ICollection<ActaCalificaciones> Actas { get; set; } = new List<ActaCalificaciones>();
}
namespace SigesEdu.Domain.Entities;

public class Estudiante
{
    public int EstudianteId { get; set; }
    public int PadreId { get; set; }
    public string? CodigoCarnet { get; set; }
    public string DocumentoIdentidad { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; }
    public string EstadoAcademico { get; set; } = "Aspirante";

    // Relaciones de navegación
    public PadreDeFamilia? Padre { get; set; }
}
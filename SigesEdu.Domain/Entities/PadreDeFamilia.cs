namespace SigesEdu.Domain.Entities;

public class PadreDeFamilia
{
    public int PadreId { get; set; }
    public int UsuarioId { get; set; }
    public string DocumentoIdentidad { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;

    // Relaciones de navegación
    public Usuario? Usuario { get; set; }
    public ICollection<Estudiante> Estudiantes { get; set; } = new List<Estudiante>();
}
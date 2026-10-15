namespace SigesEdu.Domain.Entities;

public class Seccion
{
    public int SeccionId { get; set; }
    public int GradoId { get; set; }
    public string NombreSeccion { get; set; } = string.Empty;
    public int CapacidadMaxima { get; set; }
    public int CuposOcupados { get; set; }

    public int CuposDisponibles => CapacidadMaxima - CuposOcupados;

    // Relación con el Grado
    public Grado? Grado { get; set; }
}
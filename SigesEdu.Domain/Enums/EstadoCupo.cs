namespace SigesEdu.Domain.Enums;

/// <summary>
/// Estado físico y transaccional de un cupo en un aula (RN-01 / RN-04).
/// </summary>
public enum EstadoCupo
{
    Disponible = 1,
    Reservado = 2,
    Ocupado = 3
}
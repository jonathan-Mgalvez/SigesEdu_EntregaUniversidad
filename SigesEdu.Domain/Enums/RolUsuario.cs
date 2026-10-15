namespace SigesEdu.Domain.Enums;

/// <summary>
/// Perfiles de usuario bajo el esquema de control de acceso RBAC.
/// </summary>
public enum RolUsuario
{
    Padre = 1,
    Estudiante = 2,
    Docente = 3,
    Coordinador = 4,
    Tesorero = 5,
    Administrador = 6
}
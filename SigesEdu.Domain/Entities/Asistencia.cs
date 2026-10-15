using System;
using SigesEdu.Domain.Enums;

namespace SigesEdu.Domain.Entities;

public class Asistencia
{
    public int AsistenciaId { get; private set; }
    public int CursoId { get; private set; }
    public int EstudianteId { get; private set; }
    public DateTime Fecha { get; private set; }
    public EstadoAsistencia Estado { get; private set; }
    public string? Observacion { get; private set; }

    // Relaciones de navegación
    public Curso? Curso { get; private set; }
    public Estudiante? Estudiante { get; private set; }

    protected Asistencia() { }

    public Asistencia(int cursoId, int estudianteId, DateTime fecha, EstadoAsistencia estado, string? observacion = null)
    {
        if (cursoId <= 0) throw new ArgumentException("El curso es inválido.");
        if (estudianteId <= 0) throw new ArgumentException("El estudiante es inválido.");
        if (fecha.Date > DateTime.UtcNow.Date) throw new ArgumentException("No se puede registrar asistencia en fechas futuras.");

        CursoId = cursoId;
        EstudianteId = estudianteId;
        Fecha = fecha.Date; // Garantiza que solo se guarde la fecha sin la hora
        Estado = estado;
        Observacion = observacion;
    }

    public void ModificarEstado(EstadoAsistencia nuevoEstado, string? nuevaObservacion = null)
    {
        Estado = nuevoEstado;
        if (nuevaObservacion != null)
        {
            Observacion = nuevaObservacion;
        }
    }
}
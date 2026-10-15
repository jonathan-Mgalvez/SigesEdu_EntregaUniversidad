using System;

namespace SigesEdu.Domain.Entities;

public class Calificacion
{
    public int CalificacionId { get; private set; }
    public int ActaId { get; private set; }
    public int EstudianteId { get; private set; }
    public decimal NotaParcial { get; private set; }
    public decimal NotaFinal { get; private set; }
    public bool EsAprobado { get; private set; }

    public ActaCalificaciones? Acta { get; private set; }
    public Estudiante? Estudiante { get; private set; }

    protected Calificacion() { }

    public Calificacion(int actaId, int estudianteId, decimal notaParcial, decimal notaFinal)
    {
        if (notaParcial < 0m || notaParcial > 100m) throw new ArgumentException("La nota parcial debe estar entre 0 y 100.");
        if (notaFinal < 0m || notaFinal > 100m) throw new ArgumentException("La nota final debe estar entre 0 y 100.");

        ActaId = actaId;
        EstudianteId = estudianteId;
        NotaParcial = notaParcial;
        NotaFinal = notaFinal;
        EsAprobado = notaFinal >= 60.0m; // Aprobación determinista
    }

    public void ActualizarNotas(decimal notaParcial, decimal notaFinal)
    {
        if (notaParcial < 0m || notaParcial > 100m) throw new ArgumentException("La nota parcial debe estar entre 0 y 100.");
        if (notaFinal < 0m || notaFinal > 100m) throw new ArgumentException("La nota final debe estar entre 0 y 100.");

        NotaParcial = notaParcial;
        NotaFinal = notaFinal;
        EsAprobado = notaFinal >= 60.0m;
    }
}
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SigesEdu.Domain.Entities;

public class ActaCalificaciones
{
    public int ActaId { get; private set; }
    public int CursoId { get; private set; }
    public string Periodo { get; private set; } = string.Empty;
    public bool EsCerrada { get; private set; }
    public DateTime? FechaCierre { get; private set; }
    public int? CoordinadorId { get; private set; }
    public string? FirmaHash { get; private set; }

    // Relaciones
    public Curso? Curso { get; private set; }
    public Usuario? Coordinador { get; private set; }
    
    private readonly List<Calificacion> _calificaciones = new();
    public IReadOnlyCollection<Calificacion> Calificaciones => _calificaciones.AsReadOnly();

    protected ActaCalificaciones() { }

    public ActaCalificaciones(int cursoId, string periodo)
    {
        if (string.IsNullOrWhiteSpace(periodo))
            throw new InvalidOperationException("El período académico es obligatorio.");

        CursoId = cursoId;
        Periodo = periodo;
        EsCerrada = false;
    }

    // Métodos de Negocio (RN-05)
    public void EjecutarCierreInmutable(int coordinadorId, int totalEstudiantesMatriculados)
    {
        if (EsCerrada)
            throw new InvalidOperationException("El acta ya ha sido cerrada previamente.");

        if (_calificaciones.Count < totalEstudiantesMatriculados)
            throw new InvalidOperationException(
                $"No se puede cerrar el acta: Calificaciones registradas ({_calificaciones.Count}) no cubren la nómina activa ({totalEstudiantesMatriculados}).");

        EsCerrada = true;
        FechaCierre = DateTime.UtcNow;
        CoordinadorId = coordinadorId;
        FirmaHash = GenerarHuellaSha256();
    }

    private string GenerarHuellaSha256()
    {
        var dataBuilder = new StringBuilder();
        dataBuilder.Append($"{ActaId}|{CursoId}|{Periodo}|{FechaCierre:O}|{CoordinadorId}|");

        foreach (var c in _calificaciones)
        {
            dataBuilder.Append($"{c.EstudianteId}:{c.NotaFinal:F2};");
        }

        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataBuilder.ToString()));
        return Convert.ToHexString(bytes);
    }
    public void RegistrarCalificacion(int estudianteId, decimal notaParcial, decimal notaFinal)
    {
        if (EsCerrada)
            throw new InvalidOperationException("Operación denegada (RN-05): El acta se encuentra cerrada e inmutable.");

        if (notaParcial < 0.0m || notaParcial > 100.0m || notaFinal < 0.0m || notaFinal > 100.0m)
            throw new InvalidOperationException("Las notas deben ubicarse estrictamente en el rango de 0.0 a 100.0.");

        // Para evitar dependencias complejas de LINQ aquí, buscamos manualmente o iteramos
        Calificacion? califExistente = null;
        foreach (var c in _calificaciones)
        {
            if (c.EstudianteId == estudianteId)
            {
                califExistente = c;
                break;
            }
        }

        if (califExistente != null)
        {
            califExistente.ActualizarNotas(notaParcial, notaFinal);
        }
        else
        {
            _calificaciones.Add(new Calificacion(ActaId, estudianteId, notaParcial, notaFinal));
        }
    }
}
using System;
using System.Collections.Generic;
using SigesEdu.Domain.Enums;

namespace SigesEdu.Domain.Entities;

public class SolicitudMatricula
{
    public int SolicitudId { get; private set; }
    public int EstudianteId { get; private set; }
    public int SeccionId { get; private set; }
    public string CodigoSeguimiento { get; private set; } = string.Empty;
    public EstadoMatricula EstadoMatricula { get; private set; }
    public DateTime FechaSolicitud { get; private set; }
    public string? Observaciones { get; private set; }

    // Relaciones de navegación
    public Estudiante? Estudiante { get; private set; }
    public Seccion? Seccion { get; private set; }
    public Cupo? CupoAsignado { get; private set; }
    
    private readonly List<LegajoDocumento> _documentos = new();
    public IReadOnlyCollection<LegajoDocumento> Documentos => _documentos.AsReadOnly();
    
    private readonly List<ComprobantePago> _comprobantes = new();
    public IReadOnlyCollection<ComprobantePago> Comprobantes => _comprobantes.AsReadOnly();

    protected SolicitudMatricula() { }

    // Constructor para creación inicial (Estado: Pendiente de Documentos)
    public SolicitudMatricula(int estudianteId, int seccionId, string codigoSeguimiento)
    {
        if (string.IsNullOrWhiteSpace(codigoSeguimiento))
            throw new InvalidOperationException("El código de seguimiento es obligatorio.");

        EstudianteId = estudianteId;
        SeccionId = seccionId;
        CodigoSeguimiento = codigoSeguimiento;
        EstadoMatricula = EstadoMatricula.PendienteDocumentos;
        FechaSolicitud = DateTime.UtcNow;
    }

    // Métodos de Negocio Encapsulados (Máquina de Estados)
    public void AprobarDocumentacionYPreMatricular()
    {
        if (EstadoMatricula != EstadoMatricula.DocumentosCargados)
            throw new InvalidOperationException("Solo se puede pre-matricular si los documentos han sido cargados.");

        EstadoMatricula = EstadoMatricula.PreMatriculado;
    }

    public void MarcarDocumentosObservados(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Debe especificarse el motivo de la observación.");

        Observaciones = motivo;
        EstadoMatricula = EstadoMatricula.DocumentosObservados;
    }

    public void ActivarMatriculaDefinitiva()
    {
        if (EstadoMatricula != EstadoMatricula.PagoVerificado && EstadoMatricula != EstadoMatricula.PreMatriculado)
            throw new InvalidOperationException("La matrícula solo puede activarse tras verificar el pago correspondiente.");

        EstadoMatricula = EstadoMatricula.MatriculaActiva;
    }

    public void CaducarPorFaltaDePago()
    {
        if (EstadoMatricula != EstadoMatricula.PreMatriculado)
            throw new InvalidOperationException("Solo una solicitud pre-matriculada puede caducar.");

        EstadoMatricula = EstadoMatricula.CaducadaPorFaltaDePago;
    }
}
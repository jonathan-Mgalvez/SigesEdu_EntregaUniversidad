namespace SigesEdu.Domain.Enums;

/// <summary>
/// Representa los estados deterministas del ciclo de vida de la matrícula en la FSM.
/// </summary>
public enum EstadoMatricula
{
    PendienteDocumentos = 1,
    DocumentosCargados = 2,
    DocumentosObservados = 3,
    PreMatriculado = 4,
    CaducadaPorFaltaDePago = 5,
    PagoVerificado = 6,
    MatriculaActiva = 7,
    Cancelada = 8
}
using SigesEdu.Domain.Enums;
using SigesEdu.Domain.Exceptions;

namespace SigesEdu.Application.Services;

public class MotorEstadosMatriculaService : IMotorEstadosMatriculaService
{
    public bool PuedeTransitar(EstadoMatricula estadoActual, EstadoMatricula estadoDestino)
    {
        return (estadoActual, estadoDestino) switch
        {
            (EstadoMatricula.PendienteDocumentos, EstadoMatricula.DocumentosCargados) => true,
            (EstadoMatricula.DocumentosCargados, EstadoMatricula.PreMatriculado) => true,
            (EstadoMatricula.DocumentosCargados, EstadoMatricula.DocumentosObservados) => true,
            (EstadoMatricula.DocumentosObservados, EstadoMatricula.DocumentosCargados) => true,
            (EstadoMatricula.PreMatriculado, EstadoMatricula.PagoVerificado) => true,
            (EstadoMatricula.PreMatriculado, EstadoMatricula.CaducadaPorFaltaDePago) => true,
            (EstadoMatricula.PagoVerificado, EstadoMatricula.MatriculaActiva) => true,
            _ => false
        };
    }

    public void ValidarTransicion(EstadoMatricula actual, EstadoMatricula destino)
    {
        if (!PuedeTransitar(actual, destino))
        {
            throw new TransicionEstadoInvalidaException(
                $"Transición no permitida en la máquina de estados: De '{actual}' hacia '{destino}'.");
        }
    }
}
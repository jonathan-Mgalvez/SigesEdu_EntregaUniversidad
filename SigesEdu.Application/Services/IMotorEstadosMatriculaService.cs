using SigesEdu.Domain.Enums;

namespace SigesEdu.Application.Services;

public interface IMotorEstadosMatriculaService
{
    bool PuedeTransitar(EstadoMatricula estadoActual, EstadoMatricula estadoDestino);
    void ValidarTransicion(EstadoMatricula actual, EstadoMatricula destino);
}
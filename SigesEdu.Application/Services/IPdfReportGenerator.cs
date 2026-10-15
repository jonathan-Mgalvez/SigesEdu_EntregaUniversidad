namespace SigesEdu.Application.Services;

public interface IPdfReportGenerator
{
    byte[] GenerarBoletaCalificacionesPdf(string carnet, string datosEstructurados);
    byte[] GenerarActaMinisterialPdf(int actaId, string datosEstructurados);
}
using System.Text;
using SigesEdu.Application.Services;

namespace SigesEdu.Infrastructure.Reports;

public class PdfReportGenerator : IPdfReportGenerator
{
    public byte[] GenerarBoletaCalificacionesPdf(string carnet, string datosEstructurados)
    {
        string simulacionPdf = $"--- BOLETA OFICIAL DE CALIFICACIONES ---\nCarnet: {carnet}\n{datosEstructurados}\n[SELLO INSTITUCIONAL]";
        return Encoding.UTF8.GetBytes(simulacionPdf);
    }

    public byte[] GenerarActaMinisterialPdf(int actaId, string datosEstructurados)
    {
        string simulacionPdf = $"--- ACTA MINISTERIAL DE CIERRE ---\nActa ID: {actaId}\n{datosEstructurados}\n[FIRMA HASH SHA-256 APLICADA]";
        return Encoding.UTF8.GetBytes(simulacionPdf);
    }
}
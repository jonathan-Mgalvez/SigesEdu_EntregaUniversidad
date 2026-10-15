using System.Threading.Tasks;

namespace SigesEdu.Application.Services;

public interface ITesoreriaAppService
{
    Task<string> ValidarPagoYGenerarReciboAsync(int comprobanteId, int tesoreroId, string observacion);
}
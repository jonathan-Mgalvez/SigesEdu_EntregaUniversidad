using System.Data;
using System.Threading.Tasks;

namespace SigesEdu.Domain.Interfaces;

public interface ICupoTransaccionalService
{
    Task<int> ReservarCupoTemporalAsync(int seccionId, int solicitudId);
    Task ConsolidarCupoDefinitivoAsync(IDbConnection connection, IDbTransaction transaction, int cupoId, int seccionId);
    Task<int> LiberarCuposCaducadosAsync();
}
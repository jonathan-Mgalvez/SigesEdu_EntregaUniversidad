using System.Data;
using System.Threading.Tasks;

namespace SigesEdu.Application.Services;

public interface IAuditoriaService
{
    // Método para adjuntar a una transacción ACID existente (RN-04)
    Task RegistrarEventoAsync(IDbConnection connection, IDbTransaction transaction, int? usuarioId, string accion, string modulo, string detalle, string? direccionIp = null);
    
    // Método para eventos de seguridad directos (como inicios de sesión fallidos)
    Task RegistrarEventoIndependienteAsync(int? usuarioId, string accion, string modulo, string detalle, string? direccionIp = null);
}
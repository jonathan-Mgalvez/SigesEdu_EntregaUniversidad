using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SigesEdu.Domain.Interfaces;

namespace SigesEdu.UI.Workers;

public class LiberacionCuposWorker : BackgroundService
{
    private readonly ILogger<LiberacionCuposWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _intervaloEjecucion = TimeSpan.FromMinutes(5); // Evalúa cada 5 minutos

    public LiberacionCuposWorker(ILogger<LiberacionCuposWorker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio en segundo plano de Liberación de Cupos (RN-01) iniciado.");

        // Bucle continuo mientras la aplicación esté viva
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Crear un "Scope" temporal para resolver el servicio transaccional
                using (var scope = _scopeFactory.CreateScope())
                {
                    var cupoService = scope.ServiceProvider.GetRequiredService<ICupoTransaccionalService>();
                    
                    int cuposLiberados = await cupoService.LiberarCuposCaducadosAsync();

                    if (cuposLiberados > 0)
                    {
                        _logger.LogWarning("RN-01 Ejecutada: Se liberaron {Cantidad} cupos por falta de pago.", cuposLiberados);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intentar liberar cupos caducados en la rutina de segundo plano.");
            }

            // Poner el worker a dormir hasta el siguiente intervalo
            await Task.Delay(_intervaloEjecucion, stoppingToken);
        }
    }
}
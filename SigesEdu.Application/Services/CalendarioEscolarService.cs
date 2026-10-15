using System;

namespace SigesEdu.Application.Services;

public class CalendarioEscolarService : ICalendarioEscolarService
{
    public bool EsVentanaCalificacionesAbierta(string periodo, DateTime fechaActual)
    {
        // RN-03: Aquí se implementa la verificación contra las fechas configuradas en la BD.
        // Para la iteración de construcción actual, permitiremos el acceso para pruebas de integración.
        return true; 
    }
}
using System;

namespace SigesEdu.Application.Services;

public interface ICalendarioEscolarService
{
    bool EsVentanaCalificacionesAbierta(string periodo, DateTime fechaActual);
}
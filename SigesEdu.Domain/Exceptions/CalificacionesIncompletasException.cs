using System;

namespace SigesEdu.Domain.Exceptions;

public class CalificacionesIncompletasException : Exception
{
    public CalificacionesIncompletasException(string message) : base(message) { }
}
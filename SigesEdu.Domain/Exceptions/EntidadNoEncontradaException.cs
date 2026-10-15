using System;

namespace SigesEdu.Domain.Exceptions;

public class EntidadNoEncontradaException : Exception
{
    public EntidadNoEncontradaException(string message) : base(message) { }
}
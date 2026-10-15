using System;

namespace SigesEdu.Domain.Exceptions;

public class ActaCerradaException : Exception
{
    public ActaCerradaException(string message) : base(message) { }
}
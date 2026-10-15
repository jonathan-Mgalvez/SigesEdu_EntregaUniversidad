using System;

namespace SigesEdu.Domain.Exceptions;

public class TransicionEstadoInvalidaException : Exception
{
    public TransicionEstadoInvalidaException(string message) : base(message) { }
}

public class CupoAgotadoException : Exception
{
    public CupoAgotadoException(string message) : base(message) { }
}

public class ConflictoConcurrenciaException : Exception
{
    public ConflictoConcurrenciaException(string message) : base(message) { }
}

public class SobrecupoException : Exception
{
    public SobrecupoException(string message) : base(message) { }
}

public class ErrorTransaccionalException : Exception
{
    public ErrorTransaccionalException(string message, Exception? innerException = null) 
        : base(message, innerException) { }
}
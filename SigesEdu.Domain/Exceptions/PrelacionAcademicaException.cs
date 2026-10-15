using System;

namespace SigesEdu.Domain.Exceptions;

public class PrelacionAcademicaException : Exception
{
    public PrelacionAcademicaException(string message) : base(message) { }
}
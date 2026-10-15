using System;

namespace SigesEdu.Domain.Exceptions;

public class PeriodoEvaluacionCerradoException : Exception
{
    public PeriodoEvaluacionCerradoException(string message) : base(message) { }
}
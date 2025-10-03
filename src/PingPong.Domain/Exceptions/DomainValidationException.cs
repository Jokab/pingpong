using System;

namespace PingPong.Domain.Exceptions;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message)
    {
    }
}

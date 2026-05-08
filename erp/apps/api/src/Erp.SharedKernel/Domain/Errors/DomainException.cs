namespace Erp.SharedKernel.Domain.Errors;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public DomainException(string message, Exception inner) : base(message, inner) { }

    public string? Code { get; }
}

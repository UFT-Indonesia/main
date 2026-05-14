namespace Erp.SharedKernel.Domain.Results;

public abstract class Result<T>
{
    private Result() { }

    public sealed class Success : Result<T>
    {
        public T Value { get; }
        public Success(T value) => Value = value;
    }

    public sealed class NotFound : Result<T>
    {
        public string Message { get; }
        public NotFound(string message) => Message = message;
    }

    public sealed class Error : Result<T>
    {
        public string Code { get; }
        public string Message { get; }
        public Error(string code, string message)
        {
            Code = code;
            Message = message;
        }
    }
}

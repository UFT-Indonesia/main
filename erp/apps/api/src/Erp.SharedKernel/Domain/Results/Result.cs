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

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onNotFound,
        Func<string, string, TResult> onError)
    {
        return this switch
        {
            Success s => onSuccess(s.Value),
            NotFound n => onNotFound(n.Message),
            Error e => onError(e.Code, e.Message),
            _ => throw new InvalidOperationException($"Unexpected result type: {GetType().Name}")
        };
    }

    public void Switch(
        Action<T> onSuccess,
        Action<string> onNotFound,
        Action<string, string> onError)
    {
        switch (this)
        {
            case Success s:
                onSuccess(s.Value);
                break;
            case NotFound n:
                onNotFound(n.Message);
                break;
            case Error e:
                onError(e.Code, e.Message);
                break;
            default:
                throw new InvalidOperationException($"Unexpected result type: {GetType().Name}");
        }
    }
}

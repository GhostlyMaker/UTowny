namespace UTowny.Domain.Common;

public interface IOutcome {bool Success {get;}}
public readonly record struct Result(bool Success, string Code) : IOutcome
{
    public static Result Ok(string code = "ok") => new(true, code);
    public static Result Fail(string code) => new(false, code);
}

public readonly record struct Result<T>(bool Success, string Code, T? Value) : IOutcome
{
    public static Result<T> Ok(T value, string code = "ok") => new(true, code, value);
    public static Result<T> Fail(string code) => new(false, code, default);
}

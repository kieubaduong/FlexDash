namespace FlexDash.Api.Core;

public readonly struct Result<T> {
    private readonly T? _value;
    private readonly string? _error;

    private Result(T value) {
        _value = value;
        _error = null;
        IsOk = true;
    }

    private Result(string error) {
        _value = default;
        _error = error;
        IsOk = false;
    }

    public bool IsOk { get; }
    public bool IsError => !IsOk;

    public T GetData() => _value!;
    public string GetError() => _error!;

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Err(string error) => new(error);
}

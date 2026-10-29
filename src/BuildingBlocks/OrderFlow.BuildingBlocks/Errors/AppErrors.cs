namespace OrderFlow.BuildingBlocks.Errors;

/// <summary>A failure the client can act on. <see cref="Code"/> is stable and machine-readable.</summary>
public abstract class AppException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class NotFoundException(string what) : AppException(404, "not_found", $"{what} was not found.");

public sealed class ConflictException(string code, string message) : AppException(409, code, message);

/// <summary>A business rule was broken (422).</summary>
public sealed class RuleException(string code, string message) : AppException(422, code, message);

public sealed class UpstreamUnavailableException(string message, Exception? inner = null) : AppException(503, "upstream_unavailable", message)
{
    public Exception? Upstream { get; } = inner;
}

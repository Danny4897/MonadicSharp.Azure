using MonadicSharp;

namespace MonadicSharp.Azure.Core;

/// <summary>
/// RFC 9457-compliant problem details record, built from a MonadicSharp <see cref="Error"/>.
/// Serializes cleanly to JSON for HTTP error responses.
/// </summary>
public sealed record MonadicProblemDetails
{
    public string Type    { get; init; }
    public string Title   { get; init; }
    public int    Status  { get; init; }
    public string Detail  { get; init; }
    public string Code    { get; init; }
    public IDictionary<string, object>? Extensions { get; init; }

    public MonadicProblemDetails(Error error)
    {
        var status = error.ToHttpStatusCode();
        Type       = $"https://monadicsharp.dev/errors/{error.Type.ToString().ToLowerInvariant()}";
        Title      = error.Type.ToString();
        Status     = (int)status;
        Detail     = error.Message;
        Code       = error.Code;
        Extensions = error.Metadata.Count > 0
            ? new Dictionary<string, object>(error.Metadata)
            : null;
    }
}

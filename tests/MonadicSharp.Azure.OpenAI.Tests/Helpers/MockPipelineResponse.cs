using System.ClientModel.Primitives;
using System.Collections;

namespace MonadicSharp.Azure.OpenAI.Tests.Helpers;

/// <summary>
/// Minimal <see cref="PipelineResponse"/> implementation for unit testing
/// <see cref="System.ClientModel.ClientResultException"/> with specific HTTP status codes.
/// </summary>
internal sealed class MockPipelineResponse : PipelineResponse
{
    private readonly int _status;

    public MockPipelineResponse(int status) => _status = status;

    public override int Status          => _status;
    public override string ReasonPhrase => string.Empty;
    public override Stream? ContentStream { get => null; set { } }
    public override BinaryData Content  => BinaryData.Empty;

    protected override PipelineResponseHeaders HeadersCore => new MockPipelineResponseHeaders();

    public override BinaryData BufferContent(CancellationToken cancellationToken = default)
        => BinaryData.Empty;

    public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BinaryData.Empty);

    public override void Dispose() { }
}

internal sealed class MockPipelineResponseHeaders : PipelineResponseHeaders
{
    public override bool TryGetValue(string name, out string? value)
    {
        value = null;
        return false;
    }

    public override bool TryGetValues(string name, out IEnumerable<string>? values)
    {
        values = null;
        return false;
    }

    public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        => Enumerable.Empty<KeyValuePair<string, string>>().GetEnumerator();
}

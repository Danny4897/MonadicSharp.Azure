using Azure;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Moq;
using MonadicSharp;
using MonadicSharp.Azure.KeyVault;

namespace MonadicSharp.Azure.KeyVault.Tests;

public class SecretClientExtensionsTests
{
    private readonly Mock<SecretClient> _client = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Mock<Response<KeyVaultSecret>> MakeSecretResponse(string name, string value)
    {
        var secret   = new KeyVaultSecret(name, value);
        var response = new Mock<Response<KeyVaultSecret>>();
        response.Setup(r => r.Value).Returns(secret);
        return response;
    }

    // ── FindSecretAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task FindSecretAsync_returns_some_when_secret_exists()
    {
        _client.Setup(c => c.GetSecretAsync("db-password", null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(MakeSecretResponse("db-password", "s3cr3t!").Object);

        var result = await _client.Object.FindSecretAsync("db-password");

        result.HasValue.Should().BeTrue();
        result.Match(v => v, () => "").Should().Be("s3cr3t!");
    }

    [Fact]
    public async Task FindSecretAsync_returns_none_when_secret_not_found()
    {
        _client.Setup(c => c.GetSecretAsync("missing", null, It.IsAny<CancellationToken>()))
               .ThrowsAsync(new RequestFailedException(404, "SecretNotFound"));

        var result = await _client.Object.FindSecretAsync("missing");

        result.IsNone.Should().BeTrue();
    }

    // ── GetSecretValueAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSecretValueAsync_returns_success_with_value()
    {
        _client.Setup(c => c.GetSecretAsync("api-key", null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(MakeSecretResponse("api-key", "abc123").Object);

        var result = await _client.Object.GetSecretValueAsync("api-key");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("abc123");
    }

    [Fact]
    public async Task GetSecretValueAsync_returns_failure_when_not_found()
    {
        _client.Setup(c => c.GetSecretAsync("missing", null, It.IsAny<CancellationToken>()))
               .ThrowsAsync(new RequestFailedException(404, "SecretNotFound"));

        var result = await _client.Object.GetSecretValueAsync("missing");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("KV_SECRET_NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetSecretValueAsync_returns_failure_on_access_denied()
    {
        _client.Setup(c => c.GetSecretAsync("restricted", null, It.IsAny<CancellationToken>()))
               .ThrowsAsync(new RequestFailedException(403, "Forbidden"));

        var result = await _client.Object.GetSecretValueAsync("restricted");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetSecretValueAsync_retrieves_specific_version()
    {
        _client.Setup(c => c.GetSecretAsync("api-key", "v1", It.IsAny<CancellationToken>()))
               .ReturnsAsync(MakeSecretResponse("api-key", "old-value").Object);

        var result = await _client.Object.GetSecretValueAsync("api-key", version: "v1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("old-value");
    }

    // ── SetSecretValueAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SetSecretValueAsync_returns_success_when_secret_is_set()
    {
        _client.Setup(c => c.SetSecretAsync("conn-string", "Server=localhost", It.IsAny<CancellationToken>()))
               .ReturnsAsync(MakeSecretResponse("conn-string", "Server=localhost").Object);

        var result = await _client.Object.SetSecretValueAsync("conn-string", "Server=localhost");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task SetSecretValueAsync_returns_failure_on_access_denied()
    {
        _client.Setup(c => c.SetSecretAsync("protected", It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new RequestFailedException(403, "Forbidden"));

        var result = await _client.Object.SetSecretValueAsync("protected", "value");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    // ── DeleteSecretAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSecretAsync_returns_success_when_deletion_starts()
    {
        var operation = new Mock<DeleteSecretOperation>();
        _client.Setup(c => c.StartDeleteSecretAsync("old-key", It.IsAny<CancellationToken>()))
               .ReturnsAsync(operation.Object);

        var result = await _client.Object.DeleteSecretAsync("old-key");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteSecretAsync_returns_failure_when_secret_not_found()
    {
        _client.Setup(c => c.StartDeleteSecretAsync("missing", It.IsAny<CancellationToken>()))
               .ThrowsAsync(new RequestFailedException(404, "SecretNotFound"));

        var result = await _client.Object.DeleteSecretAsync("missing");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("KV_SECRET_NOT_FOUND");
    }
}

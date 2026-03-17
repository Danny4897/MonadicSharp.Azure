using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Moq;
using MonadicSharp;
using MonadicSharp.Azure.Storage;

namespace MonadicSharp.Azure.Storage.Tests;

public class BlobContainerClientExtensionsTests
{
    private readonly Mock<BlobContainerClient> _container = new();
    private readonly Mock<BlobClient>          _blobClient = new();
    private readonly Uri                       _blobUri =
        new("https://test.blob.core.windows.net/mycontainer/test.txt");

    public BlobContainerClientExtensionsTests()
    {
        _blobClient.Setup(c => c.Uri).Returns(_blobUri);
        _container.Setup(c => c.GetBlobClient(It.IsAny<string>()))
                  .Returns(_blobClient.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Mock<Response<BlobDownloadResult>> MakeDownloadResponse(BinaryData content)
    {
        var downloadResult = BlobsModelFactory.BlobDownloadResult(content: content);
        var response       = new Mock<Response<BlobDownloadResult>>();
        response.Setup(r => r.Value).Returns(downloadResult);
        return response;
    }

    private Mock<Response<bool>> MakeBoolResponse(bool value)
    {
        var response = new Mock<Response<bool>>();
        response.Setup(r => r.Value).Returns(value);
        return response;
    }

    // ── FindAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindAsync_returns_some_when_blob_exists()
    {
        var response = MakeDownloadResponse(BinaryData.FromString("hello"));
        _blobClient.Setup(c => c.DownloadContentAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(response.Object);

        var result = await _container.Object.FindAsync("test.txt");

        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public async Task FindAsync_returns_none_when_blob_not_found()
    {
        _blobClient.Setup(c => c.DownloadContentAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new RequestFailedException(404, "BlobNotFound"));

        var result = await _container.Object.FindAsync("missing.txt");

        result.IsNone.Should().BeTrue();
    }

    // ── DownloadAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadAsync_returns_success_with_content()
    {
        var response = MakeDownloadResponse(BinaryData.FromString("hello world"));
        _blobClient.Setup(c => c.DownloadContentAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(response.Object);

        var result = await _container.Object.DownloadAsync("test.txt");

        result.IsSuccess.Should().BeTrue();
        result.Value.ToString().Should().Be("hello world");
    }

    [Fact]
    public async Task DownloadAsync_returns_failure_on_sdk_error()
    {
        _blobClient.Setup(c => c.DownloadContentAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new RequestFailedException(404, "BlobNotFound"));

        var result = await _container.Object.DownloadAsync("missing.txt");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BLOB_NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── DownloadJsonAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadJsonAsync_deserializes_content()
    {
        var payload  = new BlobDocument("doc-1", "invoice");
        var response = MakeDownloadResponse(BinaryData.FromObjectAsJson(payload));
        _blobClient.Setup(c => c.DownloadContentAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(response.Object);

        var result = await _container.Object.DownloadJsonAsync<BlobDocument>("doc.json");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("doc-1");
        result.Value.Category.Should().Be("invoice");
    }

    [Fact]
    public async Task DownloadJsonAsync_returns_failure_on_invalid_json()
    {
        var response = MakeDownloadResponse(BinaryData.FromString("not json {{"));
        _blobClient.Setup(c => c.DownloadContentAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(response.Object);

        var result = await _container.Object.DownloadJsonAsync<BlobDocument>("bad.json");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BLOB_DESERIALIZE_ERROR");
    }

    // ── UploadAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_returns_blob_uri_on_success()
    {
        _blobClient.Setup(c => c.UploadAsync(
                It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

        var result = await _container.Object.UploadAsync(
            "test.txt", BinaryData.FromString("content"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_blobUri);
    }

    [Fact]
    public async Task UploadAsync_returns_failure_on_conflict()
    {
        _blobClient.Setup(c => c.UploadAsync(
                It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new RequestFailedException(409, "BlobAlreadyExists"));

        var result = await _container.Object.UploadAsync(
            "test.txt", BinaryData.FromString("content"), overwrite: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("BLOB_CONFLICT");
    }

    [Fact]
    public async Task UploadTextAsync_uploads_correct_string_content()
    {
        BinaryData? captured = null;
        _blobClient.Setup(c => c.UploadAsync(
                It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .Callback<BinaryData, bool, CancellationToken>((data, _, _) => captured = data)
                   .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

        await _container.Object.UploadTextAsync("readme.txt", "hello, world");

        captured.Should().NotBeNull();
        captured!.ToString().Should().Be("hello, world");
    }

    [Fact]
    public async Task UploadJsonAsync_uploads_json_serialized_payload()
    {
        BinaryData? captured = null;
        _blobClient.Setup(c => c.UploadAsync(
                It.IsAny<BinaryData>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .Callback<BinaryData, bool, CancellationToken>((data, _, _) => captured = data)
                   .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

        await _container.Object.UploadJsonAsync("doc.json", new BlobDocument("d1", "report"));

        captured.Should().NotBeNull();
        var json = captured!.ToString();
        json.Should().Contain("d1");
        json.Should().Contain("report");
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_returns_true_when_blob_existed()
    {
        _blobClient.Setup(c => c.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions?>(),
                It.IsAny<CancellationToken>()))
                   .ReturnsAsync(MakeBoolResponse(true).Object);

        var result = await _container.Object.DeleteAsync("test.txt");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_returns_false_when_blob_did_not_exist()
    {
        _blobClient.Setup(c => c.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions?>(),
                It.IsAny<CancellationToken>()))
                   .ReturnsAsync(MakeBoolResponse(false).Object);

        var result = await _container.Object.DeleteAsync("missing.txt");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_returns_failure_on_access_denied()
    {
        _blobClient.Setup(c => c.DeleteIfExistsAsync(
                It.IsAny<DeleteSnapshotsOption>(),
                It.IsAny<BlobRequestConditions?>(),
                It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new RequestFailedException(403, "AuthorizationPermissionMismatch"));

        var result = await _container.Object.DeleteAsync("protected.txt");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    // ── ExistsAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_returns_true_when_blob_exists()
    {
        _blobClient.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(MakeBoolResponse(true).Object);

        var result = await _container.Object.ExistsAsync("test.txt");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_returns_false_when_blob_missing()
    {
        _blobClient.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(MakeBoolResponse(false).Object);

        var result = await _container.Object.ExistsAsync("missing.txt");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_returns_failure_on_service_error()
    {
        _blobClient.Setup(c => c.ExistsAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new RequestFailedException(503, "ServiceUnavailable"));

        var result = await _container.Object.ExistsAsync("test.txt");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Exception);
        result.Error.Code.Should().Be("BLOB_SERVICE_ERROR");
    }
}

// Must be public at namespace level — Azure.Storage.Blobs is strong-named and Moq
// cannot proxy Response<T> with private/nested type arguments.
public record BlobDocument(string Id, string Category);

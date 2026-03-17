using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using MonadicSharp;
using MonadicSharp.Extensions;
using MonadicSharp.Azure.CosmosDb;

namespace MonadicSharp.Azure.CosmosDb.Tests;

// Must be public at namespace level — Cosmos SDK is strong-named and Moq
// cannot proxy FeedResponse<T>/ItemResponse<T> with private/nested types.
public record UserDoc(string Id, string Name);

public class ContainerExtensionsTests
{
    private readonly Mock<Container> _container = new();
    private readonly PartitionKey _pk = new("users");

    private static CosmosException CosmosError(HttpStatusCode status) =>
        new("error", status, 0, "act-1", 1.0);

    private static Mock<ItemResponse<T>> OkResponse<T>(T value)
    {
        var mock = new Mock<ItemResponse<T>>();
        mock.Setup(r => r.Resource).Returns(value);
        return mock;
    }

    // ── FindAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindAsync_returns_Some_when_item_exists()
    {
        var user = new UserDoc("u1", "Alice");
        _container
            .Setup(c => c.ReadItemAsync<UserDoc>("u1", _pk, null, default))
            .ReturnsAsync(OkResponse(user).Object);

        var result = await _container.Object.FindAsync<UserDoc>("u1", _pk);

        result.HasValue.Should().BeTrue();
        result.Match(u => u.Name, () => "").Should().Be("Alice");
    }

    [Fact]
    public async Task FindAsync_returns_None_on_404()
    {
        _container
            .Setup(c => c.ReadItemAsync<UserDoc>("u1", _pk, null, default))
            .ThrowsAsync(CosmosError(HttpStatusCode.NotFound));

        var result = await _container.Object.FindAsync<UserDoc>("u1", _pk);

        result.IsNone.Should().BeTrue();
    }

    // ── ReadAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_returns_success_when_item_exists()
    {
        var user = new UserDoc("u1", "Bob");
        _container
            .Setup(c => c.ReadItemAsync<UserDoc>("u1", _pk, null, default))
            .ReturnsAsync(OkResponse(user).Object);

        var result = await _container.Object.ReadAsync<UserDoc>("u1", _pk);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task ReadAsync_returns_NotFound_error_on_404()
    {
        _container
            .Setup(c => c.ReadItemAsync<UserDoc>("u1", _pk, null, default))
            .ThrowsAsync(CosmosError(HttpStatusCode.NotFound));

        var result = await _container.Object.ReadAsync<UserDoc>("u1", _pk);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("COSMOS_NOT_FOUND");
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_returns_success_when_created()
    {
        var user = new UserDoc("u2", "Carol");
        _container
            .Setup(c => c.CreateItemAsync(user, null, null, default))
            .ReturnsAsync(OkResponse(user).Object);

        var result = await _container.Object.CreateAsync(user);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("u2");
    }

    [Fact]
    public async Task CreateAsync_returns_Conflict_error_on_409()
    {
        var user = new UserDoc("u2", "Carol");
        _container
            .Setup(c => c.CreateItemAsync(user, null, null, default))
            .ThrowsAsync(CosmosError(HttpStatusCode.Conflict));

        var result = await _container.Object.CreateAsync(user);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    // ── UpsertAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_returns_success()
    {
        var user = new UserDoc("u3", "Dan");
        _container
            .Setup(c => c.UpsertItemAsync(user, null, null, default))
            .ReturnsAsync(OkResponse(user).Object);

        var result = await _container.Object.UpsertAsync(user);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_returns_failure_on_cosmos_error()
    {
        var user = new UserDoc("u3", "Dan");
        _container
            .Setup(c => c.UpsertItemAsync(user, null, null, default))
            .ThrowsAsync(CosmosError(HttpStatusCode.Forbidden));

        var result = await _container.Object.UpsertAsync(user);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    // ── ReplaceAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReplaceAsync_returns_success()
    {
        var user = new UserDoc("u4", "Eve");
        _container
            .Setup(c => c.ReplaceItemAsync(user, "u4", null, null, default))
            .ReturnsAsync(OkResponse(user).Object);

        var result = await _container.Object.ReplaceAsync(user, "u4");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Eve");
    }

    [Fact]
    public async Task ReplaceAsync_returns_NotFound_on_404()
    {
        var user = new UserDoc("u4", "Eve");
        _container
            .Setup(c => c.ReplaceItemAsync(user, "u4", null, null, default))
            .ThrowsAsync(CosmosError(HttpStatusCode.NotFound));

        var result = await _container.Object.ReplaceAsync(user, "u4");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_returns_unit_on_success()
    {
        _container
            .Setup(c => c.DeleteItemAsync<object>("u5", _pk, null, default))
            .ReturnsAsync(OkResponse<object>(null!).Object);

        var result = await _container.Object.DeleteAsync("u5", _pk);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task DeleteAsync_returns_NotFound_on_404()
    {
        _container
            .Setup(c => c.DeleteItemAsync<object>("u5", _pk, null, default))
            .ThrowsAsync(CosmosError(HttpStatusCode.NotFound));

        var result = await _container.Object.DeleteAsync("u5", _pk);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── QueryAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_collects_all_pages()
    {
        var users = new List<UserDoc> { new("u1", "Alice"), new("u2", "Bob") };
        var iterator = SetupIterator(users);

        _container
            .Setup(c => c.GetItemQueryIterator<UserDoc>(
                It.IsAny<QueryDefinition>(), null, null))
            .Returns(iterator);

        var result = await _container.Object
            .QueryAsync<UserDoc>(new QueryDefinition("SELECT * FROM c"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(u => u.Name).Should().BeEquivalentTo(["Alice", "Bob"]);
    }

    [Fact]
    public async Task QueryAsync_with_sql_string_collects_results()
    {
        var users = new List<UserDoc> { new("u1", "Alice") };
        var iterator = SetupIterator(users);

        _container
            .Setup(c => c.GetItemQueryIterator<UserDoc>(
                It.IsAny<QueryDefinition>(), null, null))
            .Returns(iterator);

        var result = await _container.Object
            .QueryAsync<UserDoc>("SELECT * FROM c WHERE c.name = 'Alice'");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task QueryAsync_returns_empty_list_when_no_results()
    {
        var iterator = SetupIterator<UserDoc>([]);

        _container
            .Setup(c => c.GetItemQueryIterator<UserDoc>(
                It.IsAny<QueryDefinition>(), null, null))
            .Returns(iterator);

        var result = await _container.Object
            .QueryAsync<UserDoc>(new QueryDefinition("SELECT * FROM c"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── QueryFirstOrNoneAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task QueryFirstOrNoneAsync_returns_Some_when_result_exists()
    {
        var users = new List<UserDoc> { new("u1", "Alice") };
        var iterator = SetupIterator(users);

        _container
            .Setup(c => c.GetItemQueryIterator<UserDoc>(
                It.IsAny<QueryDefinition>(), null, It.IsAny<QueryRequestOptions>()))
            .Returns(iterator);

        var result = await _container.Object
            .QueryFirstOrNoneAsync<UserDoc>(new QueryDefinition("SELECT TOP 1 * FROM c"));

        result.HasValue.Should().BeTrue();
        result.Match(u => u.Name, () => "").Should().Be("Alice");
    }

    [Fact]
    public async Task QueryFirstOrNoneAsync_returns_None_when_no_results()
    {
        var iterator = SetupIterator<UserDoc>([]);

        _container
            .Setup(c => c.GetItemQueryIterator<UserDoc>(
                It.IsAny<QueryDefinition>(), null, It.IsAny<QueryRequestOptions>()))
            .Returns(iterator);

        var result = await _container.Object
            .QueryFirstOrNoneAsync<UserDoc>(new QueryDefinition("SELECT TOP 1 * FROM c"));

        result.IsNone.Should().BeTrue();
    }

    // ── ROP pipeline example ──────────────────────────────────────────────────

    [Fact]
    public async Task Pipeline_read_then_update_flows_correctly()
    {
        var original = new UserDoc("u1", "Alice");
        var updated  = new UserDoc("u1", "Alice Updated");

        _container
            .Setup(c => c.ReadItemAsync<UserDoc>("u1", _pk, null, default))
            .ReturnsAsync(OkResponse(original).Object);

        _container
            .Setup(c => c.ReplaceItemAsync(updated, "u1", null, null, default))
            .ReturnsAsync(OkResponse(updated).Object);

        var result = await _container.Object.ReadAsync<UserDoc>("u1", _pk)
            .BindAsync(user => _container.Object.ReplaceAsync(
                user with { Name = "Alice Updated" }, user.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Alice Updated");
    }

    [Fact]
    public async Task Pipeline_stops_on_first_failure()
    {
        _container
            .Setup(c => c.ReadItemAsync<UserDoc>("u1", _pk, null, default))
            .ThrowsAsync(CosmosError(HttpStatusCode.NotFound));

        var sideEffectCalled = false;

        var result = await _container.Object.ReadAsync<UserDoc>("u1", _pk)
            .BindAsync(user =>
            {
                sideEffectCalled = true;
                return _container.Object.ReplaceAsync(user, user.Id);
            });

        result.IsFailure.Should().BeTrue();
        sideEffectCalled.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FeedIterator<T> SetupIterator<T>(IList<T> items)
    {
        var feedResponse = new Mock<FeedResponse<T>>();
        feedResponse.Setup(r => r.GetEnumerator()).Returns(items.GetEnumerator());

        var iterator = new Mock<FeedIterator<T>>();
        iterator.SetupSequence(i => i.HasMoreResults)
            .Returns(items.Count > 0)
            .Returns(false);
        iterator.Setup(i => i.ReadNextAsync(default))
            .ReturnsAsync(feedResponse.Object);

        return iterator.Object;
    }
}

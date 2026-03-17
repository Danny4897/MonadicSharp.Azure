using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Moq;
using MonadicSharp;
using MonadicSharp.Extensions;
using MonadicSharp.Azure.Messaging;

namespace MonadicSharp.Azure.Messaging.Tests;

public class ServiceBusSenderExtensionsTests
{
    private readonly Mock<ServiceBusSender> _sender = new();

    // ── SendAsync<T> ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_returns_success_when_send_succeeds()
    {
        _sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
               .Returns(Task.CompletedTask);

        var result = await _sender.Object.SendAsync(new OrderMessage("o1", 99.9m));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task SendAsync_serializes_payload_as_json()
    {
        ServiceBusMessage? captured = null;
        _sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
               .Callback<ServiceBusMessage, CancellationToken>((msg, _) => captured = msg)
               .Returns(Task.CompletedTask);

        await _sender.Object.SendAsync(new OrderMessage("o1", 42.0m));

        var json = Encoding.UTF8.GetString(captured!.Body);
        json.Should().Contain("o1");
        json.Should().Contain("42");
    }

    [Fact]
    public async Task SendAsync_applies_configure_action()
    {
        ServiceBusMessage? captured = null;
        _sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
               .Callback<ServiceBusMessage, CancellationToken>((msg, _) => captured = msg)
               .Returns(Task.CompletedTask);

        await _sender.Object.SendAsync(
            new OrderMessage("o2", 10.0m),
            configure: msg => msg.Subject = "order.created");

        captured!.Subject.Should().Be("order.created");
    }

    [Fact]
    public async Task SendAsync_returns_failure_on_service_bus_exception()
    {
        _sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
               .ThrowsAsync(new ServiceBusException("quota exceeded",
                   ServiceBusFailureReason.QuotaExceeded));

        var result = await _sender.Object.SendAsync(new OrderMessage("o3", 5.0m));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SB_QUOTA_EXCEEDED");
    }

    // ── SendRawAsync(ServiceBusMessage) ──────────────────────────────────────

    [Fact]
    public async Task SendRawAsync_raw_message_returns_success()
    {
        _sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
               .Returns(Task.CompletedTask);

        var msg = new ServiceBusMessage("hello");
        var result = await _sender.Object.SendRawAsync(msg);

        result.IsSuccess.Should().BeTrue();
    }

    // ── SendBatchAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SendBatchAsync_returns_success_for_each_message()
    {
        _sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
               .Returns(Task.CompletedTask);

        var orders = new[]
        {
            new OrderMessage("o1", 10m),
            new OrderMessage("o2", 20m),
            new OrderMessage("o3", 30m)
        };

        var results = await _sender.Object.SendBatchAsync(orders);

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

    [Fact]
    public async Task SendBatchAsync_collects_all_results_even_when_some_fail()
    {
        var callCount = 0;
        _sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
               .Returns(() =>
               {
                   callCount++;
                   if (callCount == 2)
                       throw new ServiceBusException("quota", ServiceBusFailureReason.QuotaExceeded);
                   return Task.CompletedTask;
               });

        var orders = new[]
        {
            new OrderMessage("o1", 10m),
            new OrderMessage("o2", 20m),
            new OrderMessage("o3", 30m)
        };

        var results = await _sender.Object.SendBatchAsync(orders);

        results.Should().HaveCount(3);
        results.Count(r => r.IsSuccess).Should().Be(2);
        results.Count(r => r.IsFailure).Should().Be(1);
    }

    [Fact]
    public async Task SendBatchAsync_partition_splits_successes_and_failures()
    {
        var callCount = 0;
        _sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), default))
               .Returns(() =>
               {
                   callCount++;
                   if (callCount % 2 == 0)
                       throw new ServiceBusException("error", ServiceBusFailureReason.ServiceTimeout);
                   return Task.CompletedTask;
               });

        var orders = Enumerable.Range(1, 4)
            .Select(i => new OrderMessage($"o{i}", i * 10m));

        var results = await _sender.Object.SendBatchAsync(orders);
        var successes = results.Where(r => r.IsSuccess).ToList();
        var failures  = results.Where(r => r.IsFailure).ToList();

        successes.Should().HaveCount(2);
        failures.Should().HaveCount(2);
    }

    private record OrderMessage(string Id, decimal Amount);
}

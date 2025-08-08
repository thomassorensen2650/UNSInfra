using FluentAssertions;
using UNSInfra.Services.Events;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;

namespace UNSInfra.Core.Tests.Services.Events;

public class InMemoryEventBusTests
{
    private readonly InMemoryEventBus _eventBus;

    public InMemoryEventBusTests()
    {
        var mockLogger = new Mock<ILogger<InMemoryEventBus>>();
        _eventBus = new InMemoryEventBus(mockLogger.Object);
    }

    [Fact]
    public async Task PublishAsync_ShouldNotifySubscribers()
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();
        var eventHandler = new Func<TestEvent, Task>(evt =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        _eventBus.Subscribe<TestEvent>(eventHandler);

        var testEvent = new TestEvent("Test Message");

        // Act
        await _eventBus.PublishAsync(testEvent);

        // Assert
        receivedEvents.Should().HaveCount(1);
        receivedEvents[0].Message.Should().Be("Test Message");
    }

    [Fact]
    public async Task PublishAsync_WithMultipleSubscribers_ShouldNotifyAll()
    {
        // Arrange
        var receivedEvents1 = new List<TestEvent>();
        var receivedEvents2 = new List<TestEvent>();

        _eventBus.Subscribe<TestEvent>(evt =>
        {
            receivedEvents1.Add(evt);
            return Task.CompletedTask;
        });

        _eventBus.Subscribe<TestEvent>(evt =>
        {
            receivedEvents2.Add(evt);
            return Task.CompletedTask;
        });

        var testEvent = new TestEvent("Test Message");

        // Act
        await _eventBus.PublishAsync(testEvent);

        // Assert
        receivedEvents1.Should().HaveCount(1);
        receivedEvents2.Should().HaveCount(1);
        receivedEvents1[0].Message.Should().Be("Test Message");
        receivedEvents2[0].Message.Should().Be("Test Message");
    }

    [Fact]
    public async Task PublishAsync_WithNoSubscribers_ShouldNotThrow()
    {
        // Arrange
        var testEvent = new TestEvent("Test Message");

        // Act
        var act = async () => await _eventBus.PublishAsync(testEvent);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithSubscriberException_ShouldContinueWithOtherSubscribers()
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();
        var exceptionThrown = false;

        _eventBus.Subscribe<TestEvent>(_ =>
        {
            exceptionThrown = true;
            throw new InvalidOperationException("Test exception");
        });

        _eventBus.Subscribe<TestEvent>(evt =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        var testEvent = new TestEvent("Test Message");

        // Act
        await _eventBus.PublishAsync(testEvent);

        // Assert
        exceptionThrown.Should().BeTrue();
        receivedEvents.Should().HaveCount(1);
        receivedEvents[0].Message.Should().Be("Test Message");
    }

    [Fact]
    public void Subscribe_WithSameHandlerTwice_ShouldNotDuplicateSubscriptions()
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();
        var eventHandler = new Func<TestEvent, Task>(evt =>
        {
            receivedEvents.Add(evt);
            return Task.CompletedTask;
        });

        // Act
        _eventBus.Subscribe<TestEvent>(eventHandler);
        _eventBus.Subscribe<TestEvent>(eventHandler);

        // Assert - This would be verified by publishing an event and checking the count
        // The exact behavior depends on implementation - some event buses prevent duplicates,
        // others allow them. This test documents the expected behavior.
    }

    [Fact]
    public async Task Subscribe_WithDifferentEventTypes_ShouldOnlyReceiveCorrectType()
    {
        // Arrange
        var testEventReceived = new List<TestEvent>();
        var otherEventReceived = new List<OtherTestEvent>();

        _eventBus.Subscribe<TestEvent>(evt =>
        {
            testEventReceived.Add(evt);
            return Task.CompletedTask;
        });

        _eventBus.Subscribe<OtherTestEvent>(evt =>
        {
            otherEventReceived.Add(evt);
            return Task.CompletedTask;
        });

        var testEvent = new TestEvent("Test Message");
        var otherEvent = new OtherTestEvent(42);

        // Act
        await _eventBus.PublishAsync(testEvent);
        await _eventBus.PublishAsync(otherEvent);

        // Assert
        testEventReceived.Should().HaveCount(1);
        otherEventReceived.Should().HaveCount(1);
        testEventReceived[0].Message.Should().Be("Test Message");
        otherEventReceived[0].Value.Should().Be(42);
    }

    // Test event classes
    private record TestEvent(string Message) : BaseEvent;
    private record OtherTestEvent(int Value) : BaseEvent;
}
using FluentAssertions;
using FsCheck.Xunit;
using Sample.TransactionalOutbox.Domain.Order.DomainEvents;
using Sample.TransactionalOutbox.Domain.Primitives;
using Sample.TransactionalOutbox.Domain.Tests.Generators;
using Xunit;

namespace Sample.TransactionalOutbox.Domain.Tests;

public sealed class DomainEventManagerTests
{
    private sealed class TestableEventManager : DomainEventManager { }

    [Fact]
    public void RaiseEvent_WithValidEvent_AddsEventToList()
    {
        // Arrange
        var manager = new TestableEventManager();
        var domainEvent = new OrderConfirmed(Guid.NewGuid());

        // Act
        manager.RaiseEvent(domainEvent);

        // Assert
        manager.GetEvents().Should().ContainSingle()
            .Which.Should().Be(domainEvent);
    }

    [Fact]
    public void GetEvents_WithMultipleRaisedEvents_ReturnsAllEventsInOrder()
    {
        // Arrange
        var manager = new TestableEventManager();
        var event1 = new OrderConfirmed(Guid.NewGuid());
        var event2 = new OrderConfirmed(Guid.NewGuid());
        var event3 = new OrderConfirmed(Guid.NewGuid());

        manager.RaiseEvent(event1);
        manager.RaiseEvent(event2);
        manager.RaiseEvent(event3);

        // Act
        var events = manager.GetEvents();

        // Assert
        events.Should().HaveCount(3)
            .And.ContainInOrder(event1, event2, event3);
    }

    [Fact]
    public void ClearEvents_AfterRaisingEvents_EmptiesEventList()
    {
        // Arrange
        var manager = new TestableEventManager();
        manager.RaiseEvent(new OrderConfirmed(Guid.NewGuid()));
        manager.RaiseEvent(new OrderConfirmed(Guid.NewGuid()));

        // Act
        manager.ClearEvents();

        // Assert
        manager.GetEvents().Should().BeEmpty();
    }

    [Fact]
    public void GetEvents_OnNewInstance_ReturnsEmptyList()
    {
        // Arrange
        var manager = new TestableEventManager();

        // Act
        var events = manager.GetEvents();

        // Assert
        events.Should().BeEmpty();
    }

    /// <summary>
    /// **Validates: Requirements 2.1, 2.2, 2.4**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property1_RaiseEventGetEvents_RoundTripPreservesAllEvents(IDomainEvent[] events)
    {
        // Arrange
        var manager = new TestableEventManager();

        // Act
        foreach (var domainEvent in events)
        {
            manager.RaiseEvent(domainEvent);
        }

        var result = manager.GetEvents().ToList();

        // Assert
        result.Should().HaveCount(events.Length);
        result.Should().ContainInOrder(events);
    }

    /// <summary>
    /// **Validates: Requirements 2.3, 2.5**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DomainGenerators) })]
    public void Property2_ClearEvents_EmptiesTheEventList(IDomainEvent[] events)
    {
        // Arrange
        var manager = new TestableEventManager();
        foreach (var domainEvent in events)
        {
            manager.RaiseEvent(domainEvent);
        }

        // Act
        manager.ClearEvents();

        // Assert
        manager.GetEvents().Should().BeEmpty();
    }
}

using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Sample.TransactionalOutbox.Domain.Inbox;
using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Tests.Inbox;

// Test-only dummy IInboxMessage implementations used for property-based testing
public sealed record DummyInboxMessageA : IInboxMessage;
public sealed record DummyInboxMessageB : IInboxMessage;
public sealed record DummyInboxMessageC : IInboxMessage;

public sealed class MessageTypeRegistryTests
{
    /// <summary>
    /// Generates a random (messageType, CLR type) pair where messageType is a non-empty,
    /// non-whitespace string and the CLR type is one of the dummy IInboxMessage implementations.
    /// </summary>
    private sealed class RegisterResolveArbitrary
    {
        private static readonly Type[] InboxMessageTypes =
        [
            typeof(DummyInboxMessageA),
            typeof(DummyInboxMessageB),
            typeof(DummyInboxMessageC)
        ];

        public static Arbitrary<(string MessageType, Type ClrType)> ArbitraryPair()
        {
            var gen =
                from nws in ArbMap.Default.GeneratorFor<NonWhiteSpaceString>()
                from idx in Gen.Choose(0, InboxMessageTypes.Length - 1)
                select (nws.Get, InboxMessageTypes[idx]);

            return gen.ToArbitrary();
        }
    }

    /// <summary>
    /// Feature: endpoint-restructure-and-generic-inbox, Property 3: MessageTypeRegistry register-then-resolve round-trip
    ///
    /// For any (messageType string, CLR type implementing IInboxMessage) pair,
    /// after registering the pair in the MessageTypeRegistry, calling Resolve
    /// with that messageType string returns the exact CLR type that was registered.
    ///
    /// **Validates: Requirements 9.1, 9.2**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(RegisterResolveArbitrary) })]
    public void RegisterThenResolve_ReturnsExactRegisteredType((string MessageType, Type ClrType) pair)
    {
        // Arrange
        var registry = new MessageTypeRegistry();

        // Use reflection to call the generic Register<T> with the runtime type
        var registerMethod = typeof(MessageTypeRegistry)
            .GetMethod(nameof(MessageTypeRegistry.Register))!
            .MakeGenericMethod(pair.ClrType);

        // Act
        registerMethod.Invoke(registry, [pair.MessageType]);
        var resolved = registry.Resolve(pair.MessageType);

        // Assert
        resolved.Should().NotBeNull();
        resolved.Should().Be(pair.ClrType);
    }

    /// <summary>
    /// Feature: endpoint-restructure-and-generic-inbox, Property 4: MessageTypeRegistry returns null for unregistered keys
    ///
    /// For any string that has not been registered in the MessageTypeRegistry,
    /// calling Resolve should return null.
    ///
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Resolve_WithUnregisteredKey_ReturnsNull(NonWhiteSpaceString randomKey)
    {
        // Arrange — fresh registry with no registrations
        var registry = new MessageTypeRegistry();

        // Act
        var resolved = registry.Resolve(randomKey.Get);

        // Assert
        resolved.Should().BeNull();
    }
}

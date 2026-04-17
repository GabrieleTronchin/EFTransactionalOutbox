using FsCheck;
using FsCheck.Fluent;
using Sample.TransactionalOutbox.Domain.Order.DomainEvents;
using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Tests.Generators;

public sealed class DomainGenerators
{
    /// <summary>
    /// Generates valid non-empty, non-whitespace description strings
    /// suitable for OrderEntity.Create.
    /// </summary>
    public static Arbitrary<string> ArbitraryDescription()
    {
        var gen = ArbMap.Default.GeneratorFor<NonWhiteSpaceString>()
            .Select(nws => nws.Get);

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates valid positive integers suitable for ProductEntity.Create.
    /// </summary>
    public static Arbitrary<PositiveInt> ArbitraryQuantity()
    {
        return ArbMap.Default.ArbFor<PositiveInt>();
    }

    /// <summary>
    /// Generates OrderConfirmed events with random Guids.
    /// </summary>
    public static Arbitrary<OrderConfirmed> ArbitraryOrderConfirmed()
    {
        var gen = Gen.Fresh(() => new OrderConfirmed(Guid.NewGuid()));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates IDomainEvent instances for DomainEventManager tests.
    /// Currently produces OrderConfirmed events (the only concrete IDomainEvent in the domain).
    /// </summary>
    public static Arbitrary<IDomainEvent> ArbitraryDomainEvent()
    {
        var gen = Gen.Fresh(() => (IDomainEvent)new OrderConfirmed(Guid.NewGuid()));

        return gen.ToArbitrary();
    }
}

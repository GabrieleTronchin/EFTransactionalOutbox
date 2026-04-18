using FsCheck;
using FsCheck.Fluent;
using Sample.TransactionalOutbox.Domain.Order.DomainEvents;
using Sample.TransactionalOutbox.Domain.Primitives;

namespace Sample.TransactionalOutbox.Domain.Tests.Generators;

public sealed class DomainGenerators
{
    /// <summary>
    /// Generates valid non-empty, non-whitespace strings
    /// suitable for OrderEntity.Create customerName and similar fields.
    /// </summary>
    public static Arbitrary<string> ArbitraryDescription()
    {
        var gen = ArbMap.Default.GeneratorFor<NonWhiteSpaceString>()
            .Select(nws => nws.Get);

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates valid positive integers suitable for ProductEntity.Create and OrderEntity.Create quantity.
    /// </summary>
    public static Arbitrary<PositiveInt> ArbitraryQuantity()
    {
        return ArbMap.Default.ArbFor<PositiveInt>();
    }

    /// <summary>
    /// Generates valid positive decimals suitable for ProductEntity price and OrderEntity totalAmount.
    /// </summary>
    public static Arbitrary<decimal> ArbitraryDecimal()
    {
        var gen = Gen.Choose(0, 100_000)
            .Select(i => i / 100m);

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates OrderConfirmed events with random Guids.
    /// </summary>
    public static Arbitrary<OrderConfirmed> ArbitraryOrderConfirmed()
    {
        var gen = Gen.Fresh(() => new OrderConfirmed(Guid.NewGuid(), Guid.NewGuid()));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates IDomainEvent instances for DomainEventManager tests.
    /// Currently produces OrderConfirmed and OrderCancelled events.
    /// </summary>
    public static Arbitrary<IDomainEvent> ArbitraryDomainEvent()
    {
        var gen = Gen.OneOf(
            Gen.Fresh(() => (IDomainEvent)new OrderConfirmed(Guid.NewGuid(), Guid.NewGuid())),
            Gen.Fresh(() => (IDomainEvent)new OrderCancelled(Guid.NewGuid(), Guid.NewGuid())));

        return gen.ToArbitrary();
    }
}

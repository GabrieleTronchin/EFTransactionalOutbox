# Product Summary

This is a sample application demonstrating the **Transactional Outbox Pattern** using Entity Framework Core.

## Domain

An e-commerce order management system with two core entities:

- **Product** — has a quantity that decrements when an order is confirmed
- **Order** — can be confirmed via a purchase endpoint, which triggers domain events

## How It Works

1. A user confirms an order via the `POST /PurchaseOrder/{id}` endpoint.
2. The `OrderEntity` raises an `OrderConfirmed` domain event.
3. An EF Core `SaveChangesInterceptor` serializes pending domain events into an `OutboxMessages` table within the same transaction as the order update.
4. A Quartz.NET background job polls the outbox table every 10 seconds, deserializes events, and publishes them via MediatR.
5. The `OrderConfirmedEventHandler` receives the event and decrements the product quantity.

## API Endpoints

- `GET /Products` — list products with quantities
- `GET /Orders` — list orders
- `POST /PurchaseOrder/{id}` — confirm an order by ID

## Key Concept

The outbox pattern guarantees that domain events are persisted atomically with the business data change, avoiding distributed transaction issues in microservices architectures.

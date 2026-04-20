using Sample.TransactionalOutbox.Contracts;
using Sample.TransactionalOutbox.Domain.Inbox;

namespace Sample.TransactionalOutbox.Endpoints;

public class InboxEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/Inbox/Receive",
                async (IInboxMessageRepository inboxMessageRepository, InboxReceiveRequest request) =>
                {
                    await inboxMessageRepository.ReceiveAsync(
                        request.Id,
                        request.MessageType,
                        request.Payload,
                        CancellationToken.None);

                    return Results.Ok();
                }
            )
            .WithName("ReceiveInboxMessage")
            .WithSummary("Receive an external message")
            .WithDescription("Accepts an external message payload and persists it as an InboxMessageEntity for asynchronous processing by the Inbox Processor job. Idempotent — if a message with the same ID already exists, returns 200 without creating a duplicate.")
            .Produces(200)
            .ProducesProblem(400);
    }
}

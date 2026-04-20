using Sample.TransactionalOutbox.Domain.Product;

namespace Sample.TransactionalOutbox.Endpoints;

public class ProductsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/Products").WithTags("Products");

        group.MapGet(
                "/",
                async (IProductRepository productRepository) =>
                {
                    return await productRepository.GetAsync(CancellationToken.None);
                }
            )
            .WithName("GetProducts")
            .WithSummary("Get all products")
            .WithDescription("Returns a list of all marketplace products with their catalog details, current quantities, and active status.")
            .Produces<IEnumerable<ProductEntity>>();

        group.MapGet(
                "/{id}",
                async (IProductRepository productRepository, Guid id) =>
                {
                    try
                    {
                        var product = await productRepository.GetAsync(id, CancellationToken.None);
                        return Results.Ok(product);
                    }
                    catch (InvalidOperationException)
                    {
                        return Results.Problem(statusCode: 404, title: "Product not found", detail: $"No product found with ID {id}.");
                    }
                }
            )
            .WithName("GetProductById")
            .WithSummary("Get a product by ID")
            .WithDescription("Returns a single product by its unique identifier. Returns 404 if the product does not exist.")
            .Produces<ProductEntity>()
            .ProducesProblem(404);
    }
}

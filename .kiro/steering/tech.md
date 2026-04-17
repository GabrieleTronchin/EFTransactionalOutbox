# Tech Stack

## Runtime & Language

- .NET 9.0 (C#)
- Nullable reference types enabled
- Implicit usings enabled

## Frameworks & Libraries

| Library | Version | Purpose |
|---|---|---|
| ASP.NET Core Minimal APIs | 9.0 | HTTP endpoints (no controllers) |
| Entity Framework Core | 9.0.2 | ORM and data access |
| EF Core InMemory Provider | 9.0.2 | In-memory database for development/testing |
| MediatR | 12.4.1 | In-process messaging and domain event dispatch |
| Quartz.NET | 3.13.1 | Background job scheduling (outbox processor) |
| Newtonsoft.Json | 13.0.3 | Domain event serialization with `TypeNameHandling` |
| Swashbuckle | 7.2.0 | Swagger/OpenAPI documentation |

## Build & Run

Solution file is at `src/Sample.TransactionalOutbox.sln`.

```bash
# Restore dependencies
dotnet restore src/Sample.TransactionalOutbox.sln

# Build
dotnet build src/Sample.TransactionalOutbox.sln

# Run the API
dotnet run --project src/Sample.TransactionalOutbox

# Run in Development mode (enables Swagger UI)
dotnet run --project src/Sample.TransactionalOutbox --environment Development
```

## Notes

- There is no test project in the solution currently.
- The database is in-memory and seeded on startup via `SeedDb.Initialize`. Data does not persist across restarts.
- Newtonsoft.Json is used (not System.Text.Json) because domain event serialization relies on `TypeNameHandling` for polymorphic deserialization.

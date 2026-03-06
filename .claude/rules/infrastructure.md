---
paths:
  - "src/**/Infrastructure/**/*.cs"
---

# Infrastructure Layer Rules

## Repository Implementation
- Implement both command and query repository interfaces from Domain
- A single class can implement both: `DeviceRepository : IDeviceRepository, IDeviceQueryRepository`
- Use EF Core for writes, Dapper for read-optimized queries where needed
- Pagination returns `(IReadOnlyCollection<T> Items, int TotalCount)` tuples

## EF Core Configuration
- Entity configurations in `Persistence/Configurations/` folder using `IEntityTypeConfiguration<T>`
- Auto-apply via `modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())`
- Each service has its own schema: `modelBuilder.HasDefaultSchema("device_manager")`
- Ignore domain events: `modelBuilder.Ignore<DomainEvent>()`
- Value object conversions defined in entity configurations

## DbContext
- One DbContext per microservice, inherits from `DbContext`
- DbSets for aggregate roots only
- Protected parameterless constructor not needed (DI handles instantiation)

## EF Core Migrations (MANDATORY)
- **Always create a migration** when adding or modifying entities, configurations, or DbSets
- Run `dotnet ef migrations add {Name} --project {Infrastructure} --startup-project {Host}` immediately after model changes
- Never commit entity/configuration changes without the corresponding migration
- Review generated migrations before committing — verify only expected changes are included
- Skipping migrations causes `PendingModelChangesWarning` runtime errors

## Zero-Downtime Migration Patterns
When modifying existing schemas in production, use the **expand/contract** pattern to avoid downtime:

**Adding a column:** Safe — add as nullable or with a default value. Backfill data in a separate migration if needed.

**Renaming a column:** Unsafe as a single step. Instead:
1. **Expand:** Add new column, deploy code that writes to both old and new
2. **Migrate:** Backfill new column from old column
3. **Contract:** Remove reads from old column, then drop it in a later migration

**Dropping a column:** Unsafe — remove all code references first, deploy, then drop the column in a subsequent migration.

**Adding a non-nullable column to existing table:** Add as nullable first with a default, backfill, then alter to non-nullable.

**Index changes:** Adding indexes is safe (may be slow on large tables — consider `CREATE INDEX CONCURRENTLY` via raw SQL migration). Dropping unused indexes is safe.

For simple additive changes (new tables, new nullable columns, new indexes), a single migration is fine. Use expand/contract only when modifying or removing existing schema that production code depends on.

## DI Registration Pattern
- Expose a single `AddInfrastructure(this IServiceCollection services, IConfiguration configuration)` extension method
- Register DbContext with Npgsql: `services.AddDbContext<TContext>(options => options.UseNpgsql(...))`
- Register repositories as scoped
- Register external service clients (NATS, Blob Storage, Redis)

## External Service Clients
- NATS: `NatsMessagePublisher` implementing `IMessagePublisher`, JSON serialization with camelCase
- Azure Blob Storage: wrapper around `BlobServiceClient` implementing `IBlobStorageClient`
- Valkey/Redis: `IConnectionMultiplexer` from StackExchange.Redis
- All external calls have explicit timeouts

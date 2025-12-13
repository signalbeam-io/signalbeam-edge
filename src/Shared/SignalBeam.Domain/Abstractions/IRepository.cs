namespace SignalBeam.Domain.Abstractions;

/// <summary>
/// Generic repository interface for aggregate roots.
/// Repositories are responsible for persistence of aggregates.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type</typeparam>
/// <typeparam name="TId">The identifier type</typeparam>
public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Finds an aggregate by its identifier.
    /// </summary>
    Task<TAggregate?> FindByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new aggregate to the repository.
    /// </summary>
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing aggregate.
    /// </summary>
    Task UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an aggregate from the repository.
    /// </summary>
    Task RemoveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}

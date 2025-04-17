namespace CacheStorage;

/// <summary>
/// Represents an event raised by an cache storage.
/// </summary>
/// <typeparam name="TValue">The type of the cached object.</typeparam>
/// <param name="sender">The cache storage which emitted this event.</param>
/// <param name="item">The event target.</param>
public delegate void CachedItemHandler<TValue> ( object? sender, TValue item );

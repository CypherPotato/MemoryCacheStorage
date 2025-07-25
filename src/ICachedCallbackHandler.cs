namespace CacheStorage;

/// <summary>
/// Provides callback methods for cache storages.
/// </summary>
public interface ICachedCallbackHandler<TValue>
{
    /// <summary>
    /// Event called when an item is being added in the cache storage.
    /// </summary>
    public event CachedItemHandler<TValue>? AddItemCallback;

    /// <summary>
    /// Event called when an item is being removed from the cache storage.
    /// </summary>
    public event CachedItemHandler<TValue>? RemoveItemCallback;
}

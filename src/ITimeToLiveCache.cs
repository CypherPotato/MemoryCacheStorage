namespace CacheStorage;

/// <summary>
/// Provides an interface for collecting expired time-to-live based values.
/// </summary>
public interface ITimeToLiveCache {
    /// <summary>
    /// Removes all expired entities from this storage.
    /// </summary>
    /// <returns>The number of removed entities.</returns>
    public int RemoveExpiredEntities ();
}

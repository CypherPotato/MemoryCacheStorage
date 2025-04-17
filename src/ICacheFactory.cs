namespace CacheStorage;

/// <summary>
/// Provides an interface for building an <see cref="ICacheStorage{TKey, TValue}"/> factory.
/// </summary>
/// <typeparam name="TFactoryKey">Represents the factory key type, used to find and separate cache instances.</typeparam>
/// <typeparam name="TFactoryCache">Represents the type which represents the cache type.</typeparam>
/// <typeparam name="TCacheKey">Represents the cache storage key type.</typeparam>
/// <typeparam name="TCacheValue">Represents the cache storage key value.</typeparam>
public interface ICacheFactory<TFactoryKey, TFactoryCache, TCacheKey, TCacheValue>
    where TFactoryKey : notnull
    where TCacheKey : notnull
    where TFactoryCache : ICacheStorage<TCacheKey, TCacheValue> {
    /// <summary>
    /// Gets a <see cref="ICacheStorage{TKey, TValue}"/> instance from the specified key or creates a new instance with the specified key.
    /// </summary>
    /// <param name="key">The storage key.</param>
    public ICacheStorage<TCacheKey, TCacheValue> GetStorage ( TFactoryKey key );

    /// <summary>
    /// Clears and removes all created <see cref="ICacheStorage{TKey, TValue}"/> instances from this
    /// factory.
    /// </summary>
    public void Clear ();
}
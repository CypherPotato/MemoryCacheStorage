using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheStorage;

/// <summary>
/// Represents a factory that hosts multiple memory caches instances.
/// </summary>
/// <typeparam name="TStorageKey">The primary type for separating <see cref="MemoryCacheStorage{TKey, TValue}"/> instances.</typeparam>
/// <typeparam name="TKey">The <see cref="MemoryCacheStorage{TKey, TValue}"/> key type.</typeparam>
/// <typeparam name="TValue">The <see cref="MemoryCacheStorage{TKey, TValue}"/> value type.</typeparam>
public class MemoryCacheFactory<TStorageKey, TKey, TValue> : ICacheFactory<TStorageKey, MemoryCacheStorage<TKey, TValue>, TKey, TValue>
    where TStorageKey : notnull
    where TKey : notnull
{
    Dictionary<TStorageKey, ICacheStorage<TKey, TValue>> storages;

    /// <summary>
    /// Gets or sets the <see cref="IEqualityComparer{T}"/> comparer for comparing <typeparamref name="TStorageKey"/> in this
    /// factory.
    /// </summary>
    public IEqualityComparer<TKey>? StorageComparer { get; set; }

    /// <summary>
    /// Gets or sets the default <see cref="CachePoolingContext"/> for this factory.
    /// All new cache stores created in this factory that contain the specified <see cref="CachePoolingContext"/> will start
    /// being collected by it.
    /// </summary>
    public CachePoolingContext? PoolingContext { get; set; }

    /// <summary>
    /// Gets or sets the default expiration time for newly created cache storages.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Creates an new instance of the <see cref="MemoryCacheFactory{TStorageKey, TKey, TValue}"/> class.
    /// </summary>
    public MemoryCacheFactory()
    {
        storages = new();
    }

    /// <summary>
    /// Creates an new instance of the <see cref="MemoryCacheFactory{TStorageKey, TKey, TValue}"/> class with the specified comparers.
    /// </summary>
    /// <param name="factoryComparer">Defines the comparer of the storage instances.</param>
    /// <param name="storageComparer">Defines the comparer of the storage keys.</param>
    public MemoryCacheFactory(IEqualityComparer<TStorageKey> factoryComparer, IEqualityComparer<TKey> storageComparer)
    {
        storages = new(factoryComparer);
        StorageComparer = storageComparer;
    }

    /// <summary>
    /// Gets an <see cref="MemoryCacheStorage{TKey, TValue}"/> instance from the specified key or creates
    /// an new one if it doens't exists.
    /// </summary>
    /// <param name="key">The storage key.</param>
    public MemoryCacheStorage<TKey, TValue> GetMemoryStorage(TStorageKey key)
        => (MemoryCacheStorage<TKey, TValue>)GetStorage(key);

    /// <inheritdoc/> 
    public ICacheStorage<TKey, TValue> GetStorage(TStorageKey key)
    {
        lock (storages)
        {
            if (storages.TryGetValue(key, out var cacheStorage))
            {
                return cacheStorage;
            }
            else
            {
                MemoryCacheStorage<TKey, TValue> memCache;
                if (StorageComparer is null)
                {
                    memCache = new MemoryCacheStorage<TKey, TValue>();
                }
                else
                {
                    memCache = new MemoryCacheStorage<TKey, TValue>(StorageComparer);
                }

                memCache.DefaultExpiration = DefaultExpiration;
                storages.Add(key, memCache);
                PoolingContext?.CollectingCaches.Add(memCache);

                return memCache;
            }
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        storages.Clear();
    }
}

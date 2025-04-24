using System.Collections;

namespace CacheStorage;

/// <summary>
/// Represents a TTL memory key/value cache storage implementation.
/// </summary>
public sealed class MemoryCacheStorage : MemoryCacheStorage<object, object?> { }

/// <summary>
/// Represents a TTL memory key/value cache storage implementation.
/// </summary>
/// <typeparam name="TKey">The type of the cache keys.</typeparam>
/// <typeparam name="TValue">The type of the cache values.</typeparam>
public class MemoryCacheStorage<TKey, TValue> :
    ICacheStorage<TKey, TValue>,
    ITimeToLiveCache,
    IEnumerable<TValue>,
    ICachedCallbackHandler<TValue>
    where TKey : notnull
{

    internal IDictionary<TKey, CacheItem<TValue>> items;
    private static MemoryCacheStorage<TKey, TValue>? shared;

    /// <summary>
    /// Gets the shared instance of <see cref="MemoryCacheStorage{TKey, TValue}"/>, which is linked to the shared
    /// <see cref="CachePoolingContext"/>.
    /// </summary>
    public static MemoryCacheStorage<TKey, TValue> Shared
    {
        get
        {
            shared ??= CachePoolingContext.Shared.Collect(new MemoryCacheStorage<TKey, TValue>());
            return shared;
        }
    }

    /// <summary>
    /// Gets or sets the default expiration time for newly added items.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(10);

    internal enum ReplaceItemAction
    {
        Dispose,
        Renew
    }

    internal bool UnsafeRemoveKey(TKey key)
    {
        var value = items[key];

        RemoveItemCallback?.Invoke(this, value.Value);

        return items.Remove(key);
    }

    internal bool SafeRemoveKey(TKey key)
    {
        lock (items)
        {
            return UnsafeRemoveKey(key);
        }
    }

    internal bool SetCachedItem(TKey key, TValue item, TimeSpan expiration, ReplaceItemAction replaceAction)
    {
        lock (items)
        {
            if (items.TryGetValue(key, out var removingEntity))
            {
                if (replaceAction == ReplaceItemAction.Dispose)
                {
                    RemoveItemCallback?.Invoke(this, removingEntity.Value);
                }
                else if (replaceAction == ReplaceItemAction.Renew)
                {
                    removingEntity.ExpiresAt = DateTime.Now.Add(expiration);
                    return true;
                }
            }

            AddItemCallback?.Invoke(this, item);
            items[key] = new CacheItem<TValue>(item, DateTime.Now.Add(expiration));
        }
        return true;
    }

    internal bool TryGetCachedItem(TKey key, out TValue value)
    {
        lock (items)
        {
            if (items.TryGetValue(key, out var cachedItem))
            {
                if (!cachedItem.IsExpired())
                {
                    value = cachedItem.Value;
                    return true;
                }
                else
                {
                    SafeRemoveKey(key);
                }
            }
            value = default!;
            return false;
        }
    }

    /// <summary>
    /// Creates an new <see cref="MemoryCacheStorage{TKey, TValue}"/> instance.
    /// </summary>
    public MemoryCacheStorage()
    {
        items = new Dictionary<TKey, CacheItem<TValue>>();
    }

    /// <summary>
    /// Creates an new <see cref="MemoryCacheStorage{TKey, TValue}"/> instance with given
    /// <see cref="IEqualityComparer{T}"/> comparer.
    /// </summary>
    /// <param name="comparer">The comparer used to compare keys inside this cache storage.</param>
    public MemoryCacheStorage(IEqualityComparer<TKey> comparer)
    {
        items = new Dictionary<TKey, CacheItem<TValue>>(comparer);
    }

    /// <summary>
    /// Gets or sets an item from their key.
    /// </summary>
    /// <param name="key">The key to set or get the item.</param>
    /// <returns></returns>
    public TValue this[TKey key]
    {
        get
        {
            if (TryGetCachedItem(key, out var cachedItem))
            {
                return cachedItem!;
            }

            throw new KeyNotFoundException();
        }
        set
        {
            SetCachedItem(key, value, DefaultExpiration, ReplaceItemAction.Dispose);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<TKey> Keys
    {
        get
        {
            lock (items)
            {
                foreach (KeyValuePair<TKey, CacheItem<TValue>> kvp in items)
                {
                    if (!kvp.Value.IsExpired())
                        yield return kvp.Key;
                }
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<TValue> Values
    {
        get
        {
            lock (items)
            {
                foreach (KeyValuePair<TKey, CacheItem<TValue>> kvp in items)
                {
                    if (!kvp.Value.IsExpired())
                        yield return kvp.Value.Value;
                }
            }
        }
    }

    /// <summary>
    /// Gets the number of cached items in this <see cref="MemoryCacheStorage{TKey, TValue}"/>.
    /// </summary>
    public int Count
    {
        get
        {
            int count = 0;
            using (var enumerator = GetEnumerator())
            {
                while (enumerator.MoveNext())
                    count++;
            }
            return count;
        }
    }

    /// <inheritdoc/>
    public event CachedItemHandler<TValue>? AddItemCallback;

    /// <inheritdoc/>
    public event CachedItemHandler<TValue>? RemoveItemCallback;

    /// <summary>
    /// Adds or sets a cached item to this cache storage using the default expiration time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="value">The object value.</param>
    /// <returns>An boolean indicating if the value was added or not.</returns>
    public bool Add(TKey key, TValue value)
    {
        return SetCachedItem(key, value, DefaultExpiration, ReplaceItemAction.Dispose);
    }

    /// <summary>
    /// Adds or sets a cached item to this cache storage with the specified expiration time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="value">The object value.</param>
    /// <param name="expiration">The expiration time for this object.</param>
    /// <returns>An boolean indicating if the value was added or not.</returns>
    public bool Add(TKey key, TValue value, TimeSpan expiration)
    {
        return SetCachedItem(key, value, expiration, ReplaceItemAction.Dispose);
    }

    /// <summary>
    /// Adds or renews an defined cached item to this cache storage with the specified
    /// expiration time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="value">The object value.</param>
    /// <param name="expiration">The expiration time for this object.</param>
    /// <returns>An boolean indicating if the value was renewed or not.</returns>
    public bool AddOrRenew(TKey key, TValue value, TimeSpan expiration)
    {
        return SetCachedItem(key, value, expiration, ReplaceItemAction.Renew);
    }

    /// <summary>
    /// Adds or renews an defined cached item to this cache storage with the default
    /// expiration time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="value">The object value.</param>
    /// <returns>An boolean indicating if the value was renewed or not.</returns>
    public bool AddOrRenew(TKey key, TValue value)
    {
        return SetCachedItem(key, value, DefaultExpiration, ReplaceItemAction.Renew);
    }

    /// <summary>
    /// Gets the cached object from the specified key or invokes the expression and caches its result for the specified time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="expiration">The object expiration time.</param>
    /// <param name="getHandler">The expression which results the <typeparamref name="TValue"/> to get.</param>
    /// <returns>Returns the cached object or the value of the expression if the object is not already cached.</returns>
    public TValue GetOrAdd(TKey key, TimeSpan expiration, Func<TValue> getHandler)
    {
        if (TryGetCachedItem(key, out var cachedItem))
        {
            return cachedItem;
        }
        else
        {
            cachedItem = getHandler();
            SetCachedItem(key, cachedItem, expiration, ReplaceItemAction.Dispose);
            return cachedItem;
        }
    }

    /// <summary>
    /// Gets the cached object from the specified key or invokes the expression and caches its result.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="getHandler">The expression which results the <typeparamref name="TValue"/> to get.</param>
    /// <returns>Returns the cached object or the value of the expression if the object is not already cached.</returns>
    public TValue GetOrAdd(TKey key, Func<TValue> getHandler)
    {
        return GetOrAdd(key, DefaultExpiration, getHandler);
    }

    /// <summary>
    /// Gets the cached object from the specified key or invokes the expression asynchronously and caches its result for the specified time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="expiration">The object expiration time.</param>
    /// <param name="getHandler">The async expression which results the <typeparamref name="TValue"/> to get.</param>
    /// <returns>Returns the cached object or the value of the expression if the object is not already cached.</returns>
    public async Task<TValue> GetOrAddAsync(TKey key, TimeSpan expiration, Func<Task<TValue>> getHandler)
    {
        if (TryGetCachedItem(key, out var cachedItem))
        {
            return cachedItem;
        }
        else
        {
            cachedItem = await getHandler();
            SetCachedItem(key, cachedItem, expiration, ReplaceItemAction.Dispose);
            return cachedItem;
        }
    }

    /// <summary>
    /// Gets the cached object from the specified key or invokes the expression asynchronously and caches its result for the specified time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="getHandler">The async expression which results the <typeparamref name="TValue"/> to get.</param>
    /// <returns>Returns the cached object or the value of the expression if the object is not already cached.</returns>
    public Task<TValue> GetOrAddAsync(TKey key, Func<Task<TValue>> getHandler)
    {
        return GetOrAddAsync(key, DefaultExpiration, getHandler);
    }

    /// <summary>
    /// Gets an boolean indicating if the specified key is present and it's associated object is not expired.
    /// </summary>
    /// <param name="key">The object key.</param>
    public bool ContainsKey(TKey key)
    {
        lock (items)
        {
            if (items.TryGetValue(key, out var cachedItem))
            {
                return !cachedItem.IsExpired();
            }
            return false;
        }
    }


    /// <summary>
    /// Tries to remove an cached object from the specified key.
    /// </summary>
    /// <param name="key">The object key to be removed.</param>
    /// <returns>An boolean indicating if the object was removed or not.</returns>
    public bool Remove(TKey key)
    {
        return SafeRemoveKey(key);
    }

    /// <summary>
    /// Tries to remove an cached object only if it is expired.
    /// </summary>
    /// <param name="key">The object key to be removed.</param>
    /// <returns>An boolean indicating if the object was removed or not.</returns>
    public bool RemoveIfExpired(TKey key)
    {
        lock (items)
        {
            if (items.TryGetValue(key, out var cachedItem))
            {
                if (cachedItem.IsExpired())
                {
                    return UnsafeRemoveKey(key);
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Gets an <typeparamref name="TValue"/> associated with the specified key.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="value">When this method returns, outputs the found object associated with the specified key.</param>
    /// <returns>An boolean indicating if the object was found or not.</returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        return TryGetCachedItem(key, out value);
    }

    /// <inheritdoc/>
    public int RemoveExpiredEntities()
    {
        lock (items)
        {
            List<TKey> toRemove = new List<TKey>(items.Count);
            foreach (KeyValuePair<TKey, CacheItem<TValue>> kvp in items)
            {
                if (kvp.Value.IsExpired())
                    toRemove.Add(kvp.Key);
            }

            foreach (TKey key in toRemove)
                UnsafeRemoveKey(key);

            return toRemove.Count;
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (items)
        {
            if (RemoveItemCallback is not null)
            {
                foreach (var item in items)
                {
                    RemoveItemCallback(this, item.Value.Value);
                }
            }

            items.Clear();
        }
    }

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator()
    {
        lock (items)
        {
            foreach (var item in items.Values)
            {
                if (!item.IsExpired())
                {
                    yield return item.Value;
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

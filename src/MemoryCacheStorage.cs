using System.Collections;
using System.Collections.Concurrent;

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

    internal ConcurrentDictionary<TKey, CacheItem<TValue>> items;
    private static Lazy<MemoryCacheStorage<TKey, TValue>> shared = new Lazy<MemoryCacheStorage<TKey, TValue>>(() => CachePoolingContext.Shared.Collect(new MemoryCacheStorage<TKey, TValue>()));

    /// <summary>
    /// Gets the shared instance of <see cref="MemoryCacheStorage{TKey, TValue}"/>, which is linked to the shared
    /// <see cref="CachePoolingContext"/>.
    /// </summary>
    public static MemoryCacheStorage<TKey, TValue> Shared => shared.Value;

    /// <summary>
    /// Gets or sets the default expiration time for newly added items.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(10);

    internal enum ReplaceItemAction
    {
        Dispose,
        Renew
    }

    internal bool SetCachedItem(TKey key, TValue item, TimeSpan expiration, ReplaceItemAction replaceAction)
    {
        var newCacheItem = new CacheItem<TValue>(item, DateTime.Now.Add(expiration));

        if (replaceAction == ReplaceItemAction.Renew)
        {
            items.AddOrUpdate(key, newCacheItem, (k, old) =>
            {
                old.ExpiresAt = newCacheItem.ExpiresAt;
                return old;
            });
            return true;
        }
        else // ReplaceItemAction.Dispose
        {
            TValue? oldItemValue = default;
            items.AddOrUpdate(key, newCacheItem, (k, old) =>
            {
                oldItemValue = old.Value;
                return newCacheItem;
            });
            if (oldItemValue is not null)
            {
                RemoveItemCallback?.Invoke(this, oldItemValue);
            }
            AddItemCallback?.Invoke(this, item);
            return true;
        }
    }

    internal bool TryGetCachedItem(TKey key, out TValue value)
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
                items.TryRemove(key, out _);
            }
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// Creates an new <see cref="MemoryCacheStorage{TKey, TValue}"/> instance.
    /// </summary>
    public MemoryCacheStorage()
    {
        items = new ConcurrentDictionary<TKey, CacheItem<TValue>>();
    }

    /// <summary>
    /// Creates an new <see cref="MemoryCacheStorage{TKey, TValue}"/> instance with given
    /// <see cref="IEqualityComparer{T}"/> comparer.
    /// </summary>
    /// <param name="comparer">The comparer used to compare keys inside this cache storage.</param>
    public MemoryCacheStorage(IEqualityComparer<TKey> comparer)
    {
        items = new ConcurrentDictionary<TKey, CacheItem<TValue>>(comparer);
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
            foreach (KeyValuePair<TKey, CacheItem<TValue>> kvp in items)
            {
                if (!kvp.Value.IsExpired())
                    yield return kvp.Key;
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<TValue> Values
    {
        get
        {
            foreach (KeyValuePair<TKey, CacheItem<TValue>> kvp in items)
            {
                if (!kvp.Value.IsExpired())
                    yield return kvp.Value.Value;
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
            return items.Count;
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
        if (items.TryGetValue(key, out var cachedItem) && !cachedItem.IsExpired())
        {
            return cachedItem.Value;
        }

        var newItem = new CacheItem<TValue>(getHandler(), DateTime.Now.Add(expiration));
        items.AddOrUpdate(key, newItem, (k, old) => newItem);
        AddItemCallback?.Invoke(this, newItem.Value);
        return newItem.Value;
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

    private readonly ConcurrentDictionary<TKey, TaskCompletionSource<TValue>> _asyncOperations = new ConcurrentDictionary<TKey, TaskCompletionSource<TValue>>();

    /// <summary>
    /// Gets the cached object from the specified key or invokes the expression asynchronously and caches its result for the specified time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="expiration">The cache expiration time.</param>
    /// <param name="getHandler">The async expression which results the <typeparamref name="TValue"/> to get.</param>
    /// <returns>Returns the cached object or the value of the expression if the object is not already cached.</returns>
    public async Task<TValue> GetOrAddAsync(TKey key, TimeSpan expiration, Func<Task<TValue>> getHandler)
    {
        // Try to get the item synchronously first
        if (TryGetCachedItem(key, out var cachedValue))
        {
            return cachedValue;
        }

        // If not found, try to add a TaskCompletionSource for this key
        var tcs = new TaskCompletionSource<TValue>();
        var existingTcs = _asyncOperations.GetOrAdd(key, tcs);

        if (existingTcs == tcs)
        {
            // This thread is responsible for executing the factory and setting the result
            try
            {
                var value = await getHandler().ConfigureAwait(false);
                SetCachedItem(key, value, expiration, ReplaceItemAction.Dispose);
                tcs.SetResult(value);
                return value;
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
                throw;
            }
            finally
            {
                _asyncOperations.TryRemove(key, out _);
            }
        }
        else
        {
            // Another thread is already handling this key, wait for its result
            return await existingTcs.Task;
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
        if (items.TryGetValue(key, out var cachedItem))
        {
            return !cachedItem.IsExpired();
        }
        return false;
    }

    /// <summary>
    /// Tries to remove an cached object from the specified key.
    /// </summary>
    /// <param name="key">The object key to be removed.</param>
    /// <returns>An boolean indicating if the object was removed or not.</returns>
    public bool Remove(TKey key)
    {
        if (items.TryRemove(key, out var removedItem))
        {
            RemoveItemCallback?.Invoke(this, removedItem.Value);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to remove an cached object only if it is expired.
    /// </summary>
    /// <param name="key">The object key to be removed.</param>
    /// <returns>An boolean indicating if the object was removed or not.</returns>
    public bool RemoveIfExpired(TKey key)
    {
        if (items.TryGetValue(key, out var cachedItem) && cachedItem.IsExpired())
        {
            if (items.TryRemove(key, out var removedItem))
            {
                RemoveItemCallback?.Invoke(this, removedItem.Value);
                return true;
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
        var expiredKeys = new List<TKey>();
        foreach (var kvp in items)
        {
            if (kvp.Value.IsExpired())
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        int removedCount = 0;
        foreach (var key in expiredKeys)
        {
            if (items.TryRemove(key, out var removedItem))
            {
                RemoveItemCallback?.Invoke(this, removedItem.Value);
                removedCount++;
            }
        }
        return removedCount;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        if (RemoveItemCallback is not null)
        {
            foreach (var item in items.Values)
            {
                RemoveItemCallback(this, item.Value);
            }
        }

        items.Clear();
    }

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator()
    {
        foreach (var item in items.Values)
        {
            if (!item.IsExpired())
            {
                yield return item.Value;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
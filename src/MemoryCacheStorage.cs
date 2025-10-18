using System.Collections;
using System.Collections.Concurrent;

namespace CacheStorage;

/// <summary>
/// Represents a TTL memory key/value cache storage implementation.
/// </summary>
/// <typeparam name="TKey">The type of the cache keys.</typeparam>
/// <typeparam name="TValue">The type of the cache values.</typeparam>
public sealed class MemoryCacheStorage<TKey, TValue> :
    ICacheStorage<TKey, TValue>,
    ITimeToLiveCache,
    IEnumerable<TValue>,
    ICachedCallbackHandler<TValue>
    where TKey : notnull
{
    internal ConcurrentDictionary<TKey, CacheItem<TValue>> items;
    private static readonly Lazy<MemoryCacheStorage<TKey, TValue>> shared = new(() => CachePoolingContext.Shared.Collect(new MemoryCacheStorage<TKey, TValue>()));

    private readonly ConcurrentDictionary<TKey, Lazy<TValue>> _operations = new();
    private readonly ConcurrentDictionary<TKey, TaskCompletionSource<TValue>> _asyncOperations = new();

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

        while (true)
        {
            if (items.TryGetValue(key, out var oldCacheItem))
            {
                if (replaceAction == ReplaceItemAction.Renew)
                {
                    // In-place update for renewal to avoid creating new CacheItem objects unnecessarily
                    oldCacheItem.Value = newCacheItem.Value;
                    oldCacheItem.ExpiresAt = newCacheItem.ExpiresAt;
                    return true;
                }

                if (items.TryUpdate(key, newCacheItem, oldCacheItem))
                {
                    RemoveItemCallback?.Invoke(this, oldCacheItem.Value);
                    AddItemCallback?.Invoke(this, item);
                    return true;
                }
            }
            else
            {
                if (items.TryAdd(key, newCacheItem))
                {
                    AddItemCallback?.Invoke(this, item);
                    return true;
                }
            }
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
                items.TryRemove(new KeyValuePair<TKey, CacheItem<TValue>>(key, cachedItem));
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
    public IEnumerable<TKey> Keys => items.Where(kvp => !kvp.Value.IsExpired()).Select(kvp => kvp.Key);

    /// <inheritdoc/>
    public IEnumerable<TValue> Values => items.Values.Where(c => !c.IsExpired()).Select(c => c.Value);

    /// <summary>
    /// Gets the number of cached items in this <see cref="MemoryCacheStorage{TKey, TValue}"/>.
    /// This might include expired items that are not yet collected.
    /// </summary>
    public int Count => items.Count;

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
        if (TryGetCachedItem(key, out var cachedValue))
        {
            return cachedValue;
        }

        var lazy = new Lazy<TValue>(getHandler, LazyThreadSafetyMode.ExecutionAndPublication);
        var existingLazy = _operations.GetOrAdd(key, lazy);

        try
        {
            var value = existingLazy.Value;
            if (existingLazy == lazy) // This thread created the value
            {
                // Check again if another thread has added the item in the meantime
                if (!items.ContainsKey(key))
                {
                    SetCachedItem(key, value, expiration, ReplaceItemAction.Dispose);
                }
            }
            return value;
        }
        catch (Exception)
        {
            _operations.TryRemove(new KeyValuePair<TKey, Lazy<TValue>>(key, existingLazy));
            throw;
        }
        finally
        {
            if (existingLazy == lazy)
            {
                _operations.TryRemove(new KeyValuePair<TKey, Lazy<TValue>>(key, lazy));
            }
        }
    }

    /// <summary>
    /// Gets the cached object from the specified key or invokes the expression and caches its result for the specified time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="expiration">The object expiration time.</param>
    /// <param name="getHandler">The expression which results the <typeparamref name="TValue"/> to get.</param>
    /// <param name="arg">The argument to be passed to the expression.</param>
    /// <returns>Returns the cached object or the value of the expression if the object is not already cached.</returns>
    public TValue GetOrAdd<TArgument>(TKey key, TimeSpan expiration, Func<TArgument, TValue> getHandler, TArgument arg)
    {
        if (TryGetCachedItem(key, out var cachedValue))
        {
            return cachedValue;
        }

        var lazy = new Lazy<TValue>(() => getHandler(arg), LazyThreadSafetyMode.ExecutionAndPublication);
        var existingLazy = _operations.GetOrAdd(key, lazy);

        try
        {
            var value = existingLazy.Value;
            if (existingLazy == lazy) // This thread created the value
            {
                // Check again if another thread has added the item in the meantime
                if (!items.ContainsKey(key))
                {
                    SetCachedItem(key, value, expiration, ReplaceItemAction.Dispose);
                }
            }
            return value;
        }
        catch (Exception)
        {
            _operations.TryRemove(new KeyValuePair<TKey, Lazy<TValue>>(key, existingLazy));
            throw;
        }
        finally
        {
            if (existingLazy == lazy)
            {
                _operations.TryRemove(new KeyValuePair<TKey, Lazy<TValue>>(key, lazy));
            }
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
    /// <param name="expiration">The cache expiration time.</param>
    /// <param name="getHandler">The async expression which results the <typeparamref name="TValue"/> to get.</param>
    /// <param name="arg">The argument to be passed to the async expression.</param>
    /// <returns>Returns the cached object or the value of the expression if the object is not already cached.</returns>
    public async Task<TValue> GetOrAddAsync<TArgument>(TKey key, TimeSpan expiration, Func<TArgument, Task<TValue>> getHandler, TArgument arg)
    {
        if (TryGetCachedItem(key, out var cachedValue))
        {
            return cachedValue;
        }

        var tcs = new TaskCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        var existingTcs = _asyncOperations.GetOrAdd(key, tcs);

        if (existingTcs == tcs)
        {
            try
            {
                var value = await getHandler(arg).ConfigureAwait(false);
                if (!items.ContainsKey(key))
                {
                    SetCachedItem(key, value, expiration, ReplaceItemAction.Dispose);
                }
                tcs.SetResult(value);
                return value;
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
                _asyncOperations.TryRemove(new KeyValuePair<TKey, TaskCompletionSource<TValue>>(key, tcs));
                throw;
            }
            finally
            {
                _asyncOperations.TryRemove(new KeyValuePair<TKey, TaskCompletionSource<TValue>>(key, tcs));
            }
        }

        return await existingTcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the cached object from the specified key or invokes the expression asynchronously and caches its result for the specified time.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="expiration">The cache expiration time.</param>
    /// <param name="getHandler">The async expression which results the <typeparamref name="TValue"/> to get.</param>
    /// <returns>Returns the cached object or the value of the expression if the object is not already cached.</returns>
    public async Task<TValue> GetOrAddAsync(TKey key, TimeSpan expiration, Func<Task<TValue>> getHandler)
    {
        if (TryGetCachedItem(key, out var cachedValue))
        {
            return cachedValue;
        }

        var tcs = new TaskCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously);
        var existingTcs = _asyncOperations.GetOrAdd(key, tcs);

        if (existingTcs == tcs)
        {
            try
            {
                var value = await getHandler().ConfigureAwait(false);
                if (!items.ContainsKey(key))
                {
                    SetCachedItem(key, value, expiration, ReplaceItemAction.Dispose);
                }
                tcs.SetResult(value);
                return value;
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
                _asyncOperations.TryRemove(new KeyValuePair<TKey, TaskCompletionSource<TValue>>(key, tcs));
                throw;
            }
            finally
            {
                _asyncOperations.TryRemove(new KeyValuePair<TKey, TaskCompletionSource<TValue>>(key, tcs));
            }
        }

        return await existingTcs.Task.ConfigureAwait(false);
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
        return TryGetCachedItem(key, out _);
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
            if (items.TryRemove(new KeyValuePair<TKey, CacheItem<TValue>>(key, cachedItem)))
            {
                RemoveItemCallback?.Invoke(this, cachedItem.Value);
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
        var expiredKeys = items.Where(kvp => kvp.Value.IsExpired()).Select(kvp => kvp.Key).ToList();

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
        var currentItems = items.ToList();
        items.Clear();

        if (RemoveItemCallback is not null)
        {
            foreach (var item in currentItems)
            {
                RemoveItemCallback(this, item.Value.Value);
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator()
    {
        return Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
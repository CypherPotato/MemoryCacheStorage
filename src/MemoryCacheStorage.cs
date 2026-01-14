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
    private ConcurrentDictionary<TKey, CacheItem<TValue>> items;
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

    /// <summary>
    /// Gets or sets whether to dispose values that implement <see cref="IDisposable"/> when they are removed from the cache.
    /// </summary>
    public bool DisposeValuesOnRemove { get; set; } = false;

    internal enum ReplaceItemAction
    {
        Dispose,
        Renew
    }

    private void SafeInvoke(CachedItemHandler<TValue>? handler, TValue value)
    {
        if (handler is null) return;
        try { handler.Invoke(this, value); }
        catch { }
    }

    private void TryDisposeValue(TValue? value)
    {
        if (DisposeValuesOnRemove && value is IDisposable d)
        {
            try { d.Dispose(); }
            catch { }
        }
    }

    private bool TryRemoveInternal(TKey key, CacheItem<TValue>? expectedItem)
    {
        bool removed;
        CacheItem<TValue>? removedItem = null;

        if (expectedItem is not null)
            removed = items.TryRemove(new KeyValuePair<TKey, CacheItem<TValue>>(key, expectedItem));
        else
            removed = items.TryRemove(key, out removedItem);

        if (removed)
        {
            var value = (expectedItem ?? removedItem)!.Value;
            SafeInvoke(RemoveItemCallback, value);
            TryDisposeValue(value);
        }
        return removed;
    }

    internal bool SetCachedItem(TKey key, TValue value, TimeSpan expiration, ReplaceItemAction replaceAction)
    {
        var newItem = new CacheItem<TValue>(value, DateTime.UtcNow.Add(expiration));
        CacheItem<TValue>? oldItem = null;
        bool added = false;

        items.AddOrUpdate(
            key,
            addValueFactory: _ => { added = true; return newItem; },
            updateValueFactory: (_, existing) => { oldItem = existing; return newItem; });

        if (added)
        {
            SafeInvoke(AddItemCallback, value);
        }
        else if (oldItem is not null)
        {
            if (replaceAction == ReplaceItemAction.Dispose)
            {
                SafeInvoke(RemoveItemCallback, oldItem.Value);
                SafeInvoke(AddItemCallback, value);
                TryDisposeValue(oldItem.Value);
            }
        }
        return true;
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
                TryRemoveInternal(key, cachedItem);
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
            foreach (var kvp in items)
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
            foreach (var kvp in items)
            {
                if (!kvp.Value.IsExpired())
                    yield return kvp.Value.Value;
            }
        }
    }

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

        var existingLazy = _operations.GetOrAdd(key, _ => new Lazy<TValue>(getHandler, LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var value = existingLazy.Value;

            var newItem = new CacheItem<TValue>(value, DateTime.UtcNow.Add(expiration));
            if (items.TryAdd(key, newItem))
            {
                SafeInvoke(AddItemCallback, value);
            }

            return value;
        }
        catch
        {
            _operations.TryRemove(new KeyValuePair<TKey, Lazy<TValue>>(key, existingLazy));
            throw;
        }
        finally
        {
            _operations.TryRemove(new KeyValuePair<TKey, Lazy<TValue>>(key, existingLazy));
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

        var existingLazy = _operations.GetOrAdd(key, _ => new Lazy<TValue>(() => getHandler(arg), LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var value = existingLazy.Value;

            var newItem = new CacheItem<TValue>(value, DateTime.UtcNow.Add(expiration));
            if (items.TryAdd(key, newItem))
            {
                SafeInvoke(AddItemCallback, value);
            }

            return value;
        }
        catch
        {
            _operations.TryRemove(new KeyValuePair<TKey, Lazy<TValue>>(key, existingLazy));
            throw;
        }
        finally
        {
            _operations.TryRemove(new KeyValuePair<TKey, Lazy<TValue>>(key, existingLazy));
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
        var existingTcs = _asyncOperations.GetOrAdd(key, _ => tcs);

        if (existingTcs == tcs)
        {
            try
            {
                var value = await getHandler(arg).ConfigureAwait(false);

                var newItem = new CacheItem<TValue>(value, DateTime.UtcNow.Add(expiration));
                if (items.TryAdd(key, newItem))
                {
                    SafeInvoke(AddItemCallback, value);
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
        var existingTcs = _asyncOperations.GetOrAdd(key, _ => tcs);

        if (existingTcs == tcs)
        {
            try
            {
                var value = await getHandler().ConfigureAwait(false);

                var newItem = new CacheItem<TValue>(value, DateTime.UtcNow.Add(expiration));
                if (items.TryAdd(key, newItem))
                {
                    SafeInvoke(AddItemCallback, value);
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
        return TryRemoveInternal(key, null);
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
            return TryRemoveInternal(key, cachedItem);
        }
        return false;
    }

    /// <summary>
    /// Removes all cached objects whose keys satisfy the specified predicate.
    /// </summary>
    /// <param name="predicate">A function to test each key for removal.</param>
    public void RemoveAll(Func<TKey, bool> predicate)
    {
        foreach (var kvp in items)
        {
            if (predicate(kvp.Key))
            {
                TryRemoveInternal(kvp.Key, kvp.Value);
            }
        }
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
        int removed = 0;
        foreach (var kvp in items)
        {
            if (kvp.Value.IsExpired())
            {
                if (TryRemoveInternal(kvp.Key, kvp.Value))
                {
                    removed++;
                }
            }
        }
        return removed;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        var old = Interlocked.Exchange(ref items, new ConcurrentDictionary<TKey, CacheItem<TValue>>());
        foreach (var kvp in old)
        {
            SafeInvoke(RemoveItemCallback, kvp.Value.Value);
            TryDisposeValue(kvp.Value.Value);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator()
    {
        foreach (var kvp in items)
        {
            if (!kvp.Value.IsExpired())
                yield return kvp.Value.Value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
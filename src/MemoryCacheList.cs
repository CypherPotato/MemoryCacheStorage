using System.Collections;
using System.Collections.Concurrent;

namespace CacheStorage;

/// <summary>
/// Represents an TTL list implementation, where it's items expires after some time.
/// </summary>
/// <typeparam name="TValue">The object type of the list.</typeparam>
public sealed class MemoryCacheList<TValue> : IList<TValue>, ITimeToLiveCache, ICachedCallbackHandler<TValue>, IReadOnlyList<TValue>, ICollection
{
    private ConcurrentBag<CacheItem<TValue>> items;
    private static Lazy<MemoryCacheList<TValue>> shared = new Lazy<MemoryCacheList<TValue>>(() => CachePoolingContext.Shared.Collect(new MemoryCacheList<TValue>()));

    /// <summary>
    /// Gets the shared instance of <see cref="MemoryCacheList{TValue}"/>, which is linked to the shared
    /// <see cref="CachePoolingContext"/>.
    /// </summary>
    public static MemoryCacheList<TValue> Shared => shared.Value;

    /// <summary>
    /// Creates an new <see cref="MemoryCacheList{TValue}"/> from the specified parameters.
    /// </summary>
    public MemoryCacheList()
    {
        items = [];
    }

    /// <summary>
    /// Gets or sets the default expiration time for newly added items.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(10);

    internal bool SetCachedItem(TValue item, TimeSpan expiration)
    {
        AddItemCallback?.Invoke(this, item);

        items.Add(new CacheItem<TValue>(item, DateTime.UtcNow.Add(expiration)));
        return true;
    }

    internal TValue GetByIndex(int index)
    {
        var item = items.ElementAt(index);
        if (item.IsExpired())
        {
            throw new IndexOutOfRangeException("The specified index was not defined or the item at the specified index has expired.");
        }
        return item.Value;
    }

    internal void SetByIndex(int index, TValue item)
    {
        var oldItem = items.ElementAt(index);
        RemoveItemCallback?.Invoke(this, oldItem.Value);

        AddItemCallback?.Invoke(this, item);
        // ConcurrentBag does not support direct replacement by index, so we add it to the end
        // instead setting the index
        items.Add(new CacheItem<TValue>(item, DateTime.UtcNow.Add(DefaultExpiration)));
    }

    /// <inheritdoc/>
    public TValue this[int index] { get => GetByIndex(index); set => SetByIndex(index, value); }

    /// <summary>
    /// Gets the count of non-expired entities in this list.
    /// </summary>
    public int Count
    {
        get
        {
            return items.Count(i => !i.IsExpired());
        }
    }

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public bool IsSynchronized => true;

    /// <inheritdoc/>
    public object SyncRoot => ((ICollection)items).SyncRoot;

    /// <inheritdoc/>
    public event CachedItemHandler<TValue>? AddItemCallback;

    /// <inheritdoc/>
    public event CachedItemHandler<TValue>? RemoveItemCallback;

    /// <summary>
    /// Caches an object and adds it to the end of this collection.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(TValue item)
    {
        SetCachedItem(item, DefaultExpiration);
    }

    /// <summary>
    /// Caches an object and adds it to the end of this collection with the specified expiration time.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="duration">The expiration time.</param>
    public void Add(TValue item, TimeSpan duration)
    {
        SetCachedItem(item, duration);
    }

    /// <summary>
    /// Searches and renews an existing and cached <typeparamref name="TValue"/>. If the value is already expired, an new
    /// entry is added in the cache store.
    /// </summary>
    /// <param name="item">The item to add or renew.</param>
    /// <param name="duration">The expiration time.</param>
    public void AddOrRenew(TValue item, TimeSpan duration)
    {
        var existingItem = items.FirstOrDefault(i => i.Value?.Equals(item) == true);
        if (existingItem != null)
        {
            var newItem = new CacheItem<TValue>(item, DateTime.UtcNow.Add(duration));
            items = new ConcurrentBag<CacheItem<TValue>>(items.Where(i => i != existingItem).Append(newItem));
            return;
        }

        AddItemCallback?.Invoke(this, item);
        items.Add(new CacheItem<TValue>(item, DateTime.UtcNow.Add(duration)));
    }

    /// <summary>
    /// Searches and renews an existing and cached <typeparamref name="TValue"/> using the default expiration time. If
    /// the value is already expired, an newentry is added in the cache store.
    /// </summary>
    /// <param name="item">The item to add or renew.</param>
    public void AddOrRenew(TValue item) => AddOrRenew(item, DefaultExpiration);

    /// <inheritdoc/>
    public void Clear()
    {
        if (RemoveItemCallback is not null)
        {
            foreach (var i in items)
            {
                RemoveItemCallback(this, i.Value);
            }
        }

        items = [];
    }

    /// <summary>
    /// Gets an boolean indicating this list contains any non-expired entity that is equals to the
    /// specified object.
    /// </summary>
    /// <param name="item">The object to compare to.</param>
    /// <returns></returns>
    public bool Contains(TValue item)
    {
        foreach (var i in items)
            if (!i.IsExpired() && i.Value?.Equals(item) == true)
                return true;
        return false;
    }

    /// <summary>
    /// Copies all the non-expired elements to another array.
    /// </summary>
    public void CopyTo(TValue[] array, int arrayIndex)
    {
        var nonExpiredItems = items.Where(item => !item.IsExpired()).Select(item => item.Value).ToArray();
        nonExpiredItems.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator()
    {
        foreach (var item in items)
        {
            if (!item.IsExpired())
            {
                yield return item.Value;
            }
        }
    }

    /// <inheritdoc/>
    public int IndexOf(TValue item)
    {
        int nonExpiredIndex = 0;
        foreach (var current in items)
        {
            if (!current.IsExpired())
            {
                if (current.Value?.Equals(item) == true)
                {
                    return nonExpiredIndex;
                }
                nonExpiredIndex++;
            }
        }
        return -1;
    }

    /// <inheritdoc/>
    public void Insert(int index, TValue item) => Insert(index, item, DefaultExpiration);

    /// <summary>
    /// Inserts an item into the <see cref="MemoryCacheList{TValue}"/> at the specified index with a specified expiration.
    /// </summary>
    /// <param name="index">The index at which the item should be inserted.</param>
    /// <param name="item">The item to insert.</param>
    /// <param name="expiresAt">The timespan after which the item expires.</param>
    public void Insert(int index, TValue item, TimeSpan expiresAt)
    {
        CacheItem<TValue> c = new CacheItem<TValue>(item, DateTime.UtcNow.Add(expiresAt));

        AddItemCallback?.Invoke(this, item);
        var tempList = items.ToList();
        tempList.Insert(index, c);
        items = [.. tempList];
    }

    /// <inheritdoc/>
    public bool Remove(TValue item)
    {
        CacheItem<TValue>? itemToRemove = null;
        foreach (var i in items)
        {
            if (i.Value?.Equals(item) == true)
            {
                itemToRemove = i;
                break;
            }
        }

        if (itemToRemove != null)
        {
            if (RemoveItemCallback is not null)
            {
                RemoveItemCallback(this, itemToRemove.Value);
            }
            // ConcurrentBag does not have a direct Remove method for a specific item.
            // We create a new bag excluding the item to remove.
            items = [.. items.Where(i => i != itemToRemove)];
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        var itemToRemove = items.ElementAt(index);
        RemoveItemCallback?.Invoke(this, itemToRemove.Value);
        items = [.. items.Where((x, i) => i != index)];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc/>
    public int RemoveExpiredEntities()
    {
        int removedCount = 0;
        var newBag = new ConcurrentBag<CacheItem<TValue>>();
        foreach (var item in items)
        {
            if (item.IsExpired())
            {
                RemoveItemCallback?.Invoke(this, item.Value);
                removedCount++;
            }
            else
            {
                newBag.Add(item);
            }
        }
        items = newBag;
        return removedCount;
    }

    /// <inheritdoc/>
    public void CopyTo(Array array, int index)
    {
        var nonExpiredItems = items.Where(item => !item.IsExpired()).Select(item => item.Value).ToArray();
        Array.Copy(nonExpiredItems, 0, array, index, nonExpiredItems.Length);
    }
}

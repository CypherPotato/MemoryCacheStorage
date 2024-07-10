using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CacheStorage;

/// <summary>
/// Represents an TTL list implementation, where it's items expires after some time.
/// </summary>
/// <typeparam name="TValue">The object type of the list.</typeparam>
public class MemoryCacheList<TValue> : IList<TValue>, ITimeToLiveCache
{
    internal List<CacheItem<TValue>> items;

    /// <summary>
    /// Creates an new <see cref="MemoryCacheList{TValue}"/> from the specified parameters.
    /// </summary>
    public MemoryCacheList()
    {
        items = new List<CacheItem<TValue>>();
    }

    /// <summary>
    /// Gets or sets the default expiration time for newly added items.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(10);

    internal bool SetCachedItem(TValue item, TimeSpan expiration)
    {
        lock (items)
        {
            items.Add(new CacheItem<TValue>()
            {
                ExpiresAt = DateTime.Now.Add(expiration),
                Value = item
            });
        }
        return true;
    }

    internal TValue GetByIndex(int index)
    {
        lock (items)
        {
            var item = items[index];
            if (item.IsExpired())
            {
                throw new IndexOutOfRangeException("The specified index was not defined or the item at the specified index has expired.");
            }
            return item.Value;
        }
    }

    internal void SetByIndex(int index, TValue item)
    {
        lock (items)
        {
            items[index] = new CacheItem<TValue>() { Value = item, ExpiresAt = DateTime.Now.Add(DefaultExpiration) };
        }
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
    public bool IsReadOnly => false;

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
    /// <param name="expiresAt">The expiration time.</param>
    public void Add(TValue item, TimeSpan expiresAt)
    {
        SetCachedItem(item, expiresAt);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (items)
        {
            items.Clear();
        }
    }

    /// <summary>
    /// Gets an boolean indicating this list contains any non-expired entity that is equals to the
    /// specified object.
    /// </summary>
    /// <param name="item">The object to compare to.</param>
    /// <returns></returns>
    public bool Contains(TValue item)
    {
        lock (items)
        {
            foreach (var i in items)
                if (!i.IsExpired() && i.Value?.Equals(item) == true)
                    return true;
        }
        return false;
    }

    /// <summary>
    /// Copies all the non-expired elements to another array.
    /// </summary>
    public void CopyTo(TValue[] array, int arrayIndex)
    {
        lock (items)
        {
            this.ToArray().CopyTo(array, arrayIndex);
        }
    }

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator()
    {
        lock (items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                CacheItem<TValue> item = items[i];
                if (!item.IsExpired())
                {
                    yield return item.Value;
                }
            }
        }
    }

    /// <inheritdoc/>
    public int IndexOf(TValue item)
    {
        lock (items)
        {
            return this.ToList().IndexOf(item);
        }
    }

    /// <inheritdoc/>
    public void Insert(int index, TValue item)
    {
        lock (items)
        {
            CacheItem<TValue> c = new CacheItem<TValue>()
            {
                Value = item,
                ExpiresAt = DateTime.Now.Add(DefaultExpiration)
            };
            items.Insert(index, c);
        }
    }

    /// <inheritdoc/>
    public void Insert(int index, TValue item, TimeSpan expiresAt)
    {
        lock (items)
        {
            CacheItem<TValue> c = new CacheItem<TValue>()
            {
                Value = item,
                ExpiresAt = DateTime.Now.Add(expiresAt)
            };
            items.Insert(index, c);
        }
    }

    /// <inheritdoc/>
    public bool Remove(TValue item)
    {
        lock (items)
        {
            int toRemove = -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Value?.Equals(item) == true)
                {
                    toRemove = i;
                    break;
                }
            }

            if (toRemove >= 0)
            {
                items.RemoveAt(toRemove);
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        lock (items)
        {
            items.RemoveAt(index);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc/>
    public int RemoveExpiredEntities()
    {
        lock (items)
        {
            List<int> toRemove = new List<int>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].IsExpired())
                {
                    toRemove.Add(i);
                }
            }

            foreach (int key in toRemove)
                items.RemoveAt(key);

            return toRemove.Count;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CypherPotato;

internal class ConcurrentCacheStack<TKey, TValue> : IDisposable where TKey : notnull
{
    private bool _disposed = false;
    private List<CacheItem<TKey, TValue>> items = new();
    internal IComparer<TKey>? kcomparer = null;
    internal Timer timer;
    internal readonly TimeSpan timerInterval;

    public ConcurrentCacheStack(TimeSpan poolingInterval, IComparer<TKey>? comparer)
    {
        timerInterval = poolingInterval;
        timer = new Timer(new TimerCallback(TimerCollectCallback), null, timerInterval, timerInterval);
        this.kcomparer = comparer;
    }

    private IList<CacheItem<TKey, TValue>> GetBag() => new List<CacheItem<TKey, TValue>>();

    void CheckIfDisposed()
    {
        if (_disposed) throw new InvalidOperationException("Cannot access, modify or delete an disposed concurrent cache stack.");
    }

    private int UnsafeCollectExpired()
    {
        CheckIfDisposed();
        var bag = GetBag();
        var span = CollectionsMarshal.AsSpan(items);
        ref var pointer = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < span.Length; i++)
        {
            var item = Unsafe.Add(ref pointer, i);
            if (item.IsExpired())
            {
                bag.Add(item);
            }
        }

        UnsafeCollect(bag);

        return bag.Count;
    }

    private void UnsafeCollect(IList<CacheItem<TKey, TValue>> toRemove)
    {
        CheckIfDisposed();
        for (int i = 0; i < toRemove.Count; i++)
        {
            items.Remove(toRemove[i]);
        }
    }

    private ref CacheItem<TKey, TValue> UnsafeGetItem(TKey key)
    {
        CheckIfDisposed();
        var span = CollectionsMarshal.AsSpan(items);
        ref var pointer = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < span.Length; i++)
        {
            ref var item = ref Unsafe.Add(ref pointer, i);
            if (!item.IsExpired() && item.IsDefined(key))
            {
                return ref item;
            }
        }

        return ref CacheItem<TKey, TValue>.Default;
    }

    private IEnumerable<CacheItem<TKey, TValue>> UnsafeGetItems(TKey key)
    {
        CheckIfDisposed();
        var bag = GetBag();
        var span = CollectionsMarshal.AsSpan(items);
        ref var pointer = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < span.Length; i++)
        {
            ref var item = ref Unsafe.Add(ref pointer, i);
            if (!item.IsExpired() && item.IsDefined(key))
            {
                bag.Add(item);
            }
        }

        return bag;
    }

    private void UnsafeAdd(CacheItem<TKey, TValue> cacheItem)
    {
        CheckIfDisposed();
        if (cacheItem.Keys.Length == 0) throw new ArgumentException("A cache item must have at least one id.");
        items.Add(cacheItem);
    }

    public int UnsafeRenewSingle(TKey identifier, DateTime value)
    {
        CheckIfDisposed();

        int n = 0;
        var span = CollectionsMarshal.AsSpan(items);
        ref var pointer = ref MemoryMarshal.GetReference(span);
        for (int i = 0; i < span.Length; i++)
        {
            ref var item = ref Unsafe.Add(ref pointer, i);
            if (item.IsDefined(identifier))
            {
                item.Renew(value);
                n++;
            }
        }

        return n;
    }

    public int SafeCount()
    {
        lock (items)
        {
            int count = items.Count;
            int rem = UnsafeCollectExpired();

            return count - rem;
        }
    }

    public void SafeAdd(TKey[] ids, TValue value, DateTime expiresAt)
    {
        CheckIfDisposed();
        ArgumentNullException.ThrowIfNull(value);

        var cItem = new CacheItem<TKey, TValue>(ids, value, expiresAt, kcomparer);
        lock (items) UnsafeAdd(cItem);
    }

    public int SafeCollect()
    {
        lock (items)
        {
            if (items.Count == 0) return 0;
            return UnsafeCollectExpired();
        }
    }

    public void SafeClear()
    {
        lock (items)
        {
            items.Clear();
        }
    }

    public void SafeInvoke(Action action)
    {
        lock (items)
        {
            action();
        }
    }

    public CacheItem<TKey, TValue> SafeFirstMatch(TKey identifier)
    {
        lock (items)
        {
            return UnsafeGetItem(identifier);
        }
    }

    public IEnumerable<CacheItem<TKey, TValue>> SafeAllMatch(TKey identifier)
    {
        lock (items)
        {
            return UnsafeGetItems(identifier);
        }
    }

    public IEnumerable<CacheItem<TKey, TValue>> SafeMatch(Func<TKey[], bool> predicate)
    {
        lock (items)
        {
            var bag = GetBag();
            var itemsSpan = CollectionsMarshal.AsSpan(items);
            for (int i = 0; i < itemsSpan.Length; i++)
            {
                if (predicate(itemsSpan[i].Keys))
                {
                    bag.Add(itemsSpan[i]);
                }
            }

            return bag;
        }
    }

    public int SafeRemove(TKey identifier)
    {
        lock (items)
        {
            var bag = GetBag();
            var itemsSpan = CollectionsMarshal.AsSpan(items);
            for (int i = 0; i < itemsSpan.Length; i++)
            {
                if (itemsSpan[i].IsDefined(identifier))
                {
                    bag.Add(itemsSpan[i]);
                }
            }

            UnsafeCollect(bag);

            return bag.Count;
        }
    }

    public int SafeRemove(Func<TKey[], bool> predicate)
    {
        lock (items)
        {
            var bag = GetBag();
            var itemsSpan = CollectionsMarshal.AsSpan(items);
            for (int i = 0; i < itemsSpan.Length; i++)
            {
                if (predicate(itemsSpan[i].Keys))
                {
                    bag.Add(itemsSpan[i]);
                }
            }

            UnsafeCollect(bag);

            return bag.Count;
        }
    }

    private void TimerCollectCallback(object? state)
    {
        if (_disposed) return;
        SafeCollect();
    }

    public void Dispose()
    {
        _disposed = true;
        timer.Dispose();
        lock (items) items.Clear();
    }
}

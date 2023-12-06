using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CypherPotato;

internal class ConcurrentCacheStack<TKey, TValue> : IDisposable where TKey : notnull
{
    private List<CacheItem<TKey, TValue>> items = new();
    private SemaphoreSlim asyncSemaphore;
    internal Timer timer;
    internal readonly TimeSpan timerInterval;

    public ConcurrentCacheStack(TimeSpan _t)
    {
        timerInterval = _t;
        timer = new Timer(new TimerCallback(CollectInternal), null, TimeSpan.Zero, timerInterval);
        asyncSemaphore = new SemaphoreSlim(1);
    }

    public ConcurrentCacheStack() : this(TimeSpan.FromSeconds(3))
    {
    }

    public async Task AcquireAsyncVoid(Action function)
    {
        await asyncSemaphore.WaitAsync();
        try
        {
            function();
        }
        finally
        {
            asyncSemaphore.Release();
        }
    }

    private ICollection<CacheItem<TKey, TValue>> GetBag() => new List<CacheItem<TKey, TValue>>();

    private CacheItem<TKey, TValue> UnsafeGetItem(TKey key)
    {
        foreach (var cacheItem in items)
        {
            int search = Array.BinarySearch(cacheItem.Keys, key);
            if (search >= 0) return cacheItem;
        }
        return CacheItem<TKey, TValue>.Default;
    }

    private IEnumerable<CacheItem<TKey, TValue>> UnsafeGetItems(TKey key)
    {
        foreach (var cacheItem in items)
        {
            int search = Array.BinarySearch(cacheItem.Keys, key);
            if (search >= 0) yield return cacheItem;
        }
    }

    private void UnsafeAdd(CacheItem<TKey, TValue> cacheItem)
    {
        if (cacheItem.Keys.Length == 0) throw new ArgumentException("A cache item must have at least one id.");
        items.Add(cacheItem);
    }

    public async Task<TAsyncResult> AcquireAsyncFunction<TAsyncResult>(Func<TAsyncResult> function)
    {
        TAsyncResult result;
        await asyncSemaphore.WaitAsync();
        try
        {
            result = function();
        }
        finally
        {
            asyncSemaphore.Release();
        }
        return result;
    }

    public int Count()
    {
        asyncSemaphore.Wait();
        try
        {
            return items.Count;
        }
        finally
        {
            asyncSemaphore.Release();
        }
    }

    public void Add(TKey[] ids, TValue value, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(nameof(value));

        var cItem = new CacheItem<TKey, TValue>(ids, value, expiresAt);

        asyncSemaphore.Wait();
        try
        {
            UnsafeAdd(cItem);
        }
        finally
        {
            asyncSemaphore.Release();
        }
    }

    private void UnsafeCollect(IEnumerable<CacheItem<TKey, TValue>> toRemove)
    {
        foreach(var item in toRemove)
        {
            items.Remove(item);
        }
    }

    private void CollectInternal(object? state)
    {
        _ = Collect();
    }

    public int Collect()
    {
        asyncSemaphore.Wait();
        try
        {
            List<CacheItem<TKey, TValue>> toRemove = new();
            foreach (var item in items)
            {
                if (DateTime.Now > item.ExpiresAt)
                {
                    toRemove.Add(item);
                }
            }

            UnsafeCollect(toRemove);

            return toRemove.Count;
        }
        finally
        {
            asyncSemaphore.Release();
        }
    }

    public void Clear()
    {
        asyncSemaphore.Wait();
        try
        {
            items.Clear();
        }
        finally
        {
            asyncSemaphore.Release();
        }
    }

    public CacheItem<TKey, TValue> FirstMatch(TKey identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        asyncSemaphore.Wait();
        var removeBag = GetBag();
        try
        {
            var item = UnsafeGetItem(identifier);
            if (DateTime.Now > item.ExpiresAt)
            {
                removeBag.Add(item);
                return CacheItem<TKey, TValue>.Default;
            }
            else return item;
        }
        finally
        {
            UnsafeCollect(removeBag);
            asyncSemaphore.Release();
        }
    }

    public IEnumerable<CacheItem<TKey, TValue>> AllMatch(TKey identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        asyncSemaphore.Wait();
        var removeBag = GetBag();
        try
        {
            var values = UnsafeGetItems(identifier);
            foreach (var item in values)
            {
                if (DateTime.Now > item.ExpiresAt)
                {
                    removeBag.Add(item);
                }
                yield return CacheItem<TKey, TValue>.Default;
            }
        }
        finally
        {
            UnsafeCollect(removeBag);
            asyncSemaphore.Release();
        }
    }

    public IEnumerable<CacheItem<TKey, TValue>> Match(Func<TKey[], bool> predicate)
    {
        asyncSemaphore.Wait();
        var removeBag = GetBag();
        try
        {
            foreach (var item in items)
            {
                if (predicate(item.Keys))
                {
                    if (DateTime.Now > item.ExpiresAt)
                    {
                        removeBag.Add(item);
                    }
                    else yield return item;
                }
            }
        }
        finally
        {
            UnsafeCollect(removeBag);
            asyncSemaphore.Release();
        }
    }

    public int Remove(TKey identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        asyncSemaphore.Wait();
        var removeBag = GetBag();
        try
        {
            var item = UnsafeGetItem(identifier);
            if(item.Keys.Length == 0)
            {
                return 0;
            } else
            {
                removeBag.Add(item);
                return 1;
            }
        }
        finally
        {
            UnsafeCollect(removeBag);
            asyncSemaphore.Release();
        }
    }

    public int Remove(Func<TKey[], bool> predicate)
    {
        int r = 0;
        asyncSemaphore.Wait();
        var removeBag = GetBag();
        try
        {
            foreach (var item in items)
            {
                if (predicate(item.Keys))
                {
                    removeBag.Add(item);
                    r++;
                }
            }
        }
        finally
        {
            UnsafeCollect(removeBag);
            asyncSemaphore.Release();
        }
        return r;
    }

    public void Dispose()
    {
        timer.Dispose();
        asyncSemaphore.Wait();
        try
        {
            items.Clear();
        }
        finally
        {
            asyncSemaphore.Dispose();
        }
    }
}

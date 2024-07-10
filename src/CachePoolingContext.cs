using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheStorage;

/// <summary>
/// Provides a pooling context and thread to periodically remove all expired items from cache storages.
/// </summary>
public sealed class CachePoolingContext
{
    internal Thread collectionThread;

    /// <summary>
    /// Gets or sets the pooling interval.
    /// </summary>
    public TimeSpan CollectInterval { get; set; }

    /// <summary>
    /// Gets an boolean indicating if the current pool is running.
    /// </summary>
    public bool IsCollecting { get; private set; } = false;

    /// <summary>
    /// Gets or sets an list of <see cref="ITimeToLiveCache"/> items which will be collected
    /// from the current pool.
    /// </summary>
    public IList<ITimeToLiveCache> CollectingCaches { get; set; } = new List<ITimeToLiveCache>();

    /// <summary>
    /// Creates an new instance of the <see cref="CachePoolingContext"/> class.
    /// </summary>
    /// <param name="interval">The pooling interval.</param>
    public CachePoolingContext(TimeSpan interval)
    {
        CollectInterval = interval;

        collectionThread = new Thread(new ThreadStart(CollectionThreadJob));
        collectionThread.IsBackground = true;
    }

    /// <summary>
    /// Creates an new instance of <see cref="CachePoolingContext"/> with the specified interval
    /// and immediately starts it.
    /// </summary>
    /// <param name="collectInterval">The pooling interval time.</param>
    public static CachePoolingContext StartNew(TimeSpan collectInterval)
    {
        var c = new CachePoolingContext(collectInterval);
        c.StartCollecting();
        return c;
    }

    /// <summary>
    /// Starts the pooling collection.
    /// </summary>
    public void StartCollecting()
    {
        if (collectionThread.ThreadState != ThreadState.Running)
        {
            IsCollecting = true;
            collectionThread.Start();
        }
    }

    /// <summary>
    /// Stops the pooling collection.
    /// </summary>
    public void StopCollecting()
    {
        IsCollecting = false;
    }

    /// <summary>
    /// Collects all expired entities from monitored cache storages.
    /// </summary>
    /// <returns>
    /// the amount of removed entities from all cache storages.
    /// </returns>
    public int CollectAll()
    {
        lock (CollectingCaches)
        {
            int count = 0;
            for (int i = 0; i < CollectingCaches.Count; i++)
            {
                var cache = CollectingCaches[i];
                count += cache.RemoveExpiredEntities();
            }
            return count;
        }
    }

    void CollectionThreadJob()
    {
        while (IsCollecting)
        {
            try
            {
                CollectAll();
            }
            finally
            {
                Thread.Sleep(CollectInterval);
            }
        }
    }
}

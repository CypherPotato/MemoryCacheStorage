using System.Collections;

namespace CacheStorage;

/// <summary>
/// Provides a pooling context and thread to periodically remove all expired items from cache storages.
/// </summary>
public sealed class CachePoolingContext
{
    internal System.Timers.Timer collectionTimer;
    private static CachePoolingContext? shared;

    /// <summary>
    /// Gets the shared instance of <see cref="CachePoolingContext"/>.
    /// </summary>
    public static CachePoolingContext Shared
    {
        get
        {
            shared ??= StartNew(TimeSpan.FromMinutes(10));
            return shared;
        }
    }

    /// <summary>
    /// Gets or sets the pooling interval.
    /// </summary>
    public TimeSpan CollectInterval
    {
        get => TimeSpan.FromMilliseconds(collectionTimer.Interval);
        set => collectionTimer.Interval = value.TotalMilliseconds;
    }

    /// <summary>
    /// Gets an boolean indicating if the current pool is running.
    /// </summary>
    public bool IsCollecting
    {
        get => collectionTimer.Enabled;
        set => collectionTimer.Enabled = value;
    }

    /// <summary>
    /// Gets or sets an list of <see cref="ITimeToLiveCache"/> items which will be collected
    /// from the current pool.
    /// </summary>
    public IList<ITimeToLiveCache> CollectingCaches { get; set; } = [];

    /// <summary>
    /// Creates an new instance of the <see cref="CachePoolingContext"/> class.
    /// </summary>
    /// <param name="interval">The pooling interval.</param>
    public CachePoolingContext(TimeSpan interval)
    {
        collectionTimer = new System.Timers.Timer(interval.TotalMilliseconds);
        collectionTimer.Elapsed += CollectionTimer_Elapsed;
        CollectInterval = interval;
    }

    private void CollectionTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        CollectAll();
    }

    /// <summary>
    /// Creates an new instance of <see cref="CachePoolingContext"/> with the specified interval
    /// and immediately starts it.
    /// </summary>
    /// <param name="collectInterval">The pooling interval time.</param>
    /// <param name="collectingCaches">An optional array of <see cref="ITimeToLiveCache"/> to start collecting.</param>
    public static CachePoolingContext StartNew(TimeSpan collectInterval, params ITimeToLiveCache[] collectingCaches)
    {
        var c = new CachePoolingContext(collectInterval);

        for (int i = 0; i < collectingCaches.Length; i++)
            c.CollectingCaches.Add(collectingCaches[i]);

        c.StartCollecting();
        return c;
    }

    /// <summary>
    /// Starts the pooling collection.
    /// </summary>
    public void StartCollecting()
    {
        collectionTimer.Start();
    }

    /// <summary>
    /// Stops the pooling collection.
    /// </summary>
    public void StopCollecting()
    {
        collectionTimer.Stop();
    }

    /// <summary>
    /// Collects all expired entities from monitored cache storages.
    /// </summary>
    /// <returns>
    /// the amount of removed entities from all cache storages.
    /// </returns>
    public int CollectAll()
    {
        lock (((ICollection)CollectingCaches).SyncRoot)
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

    /// <summary>
    /// Adds a <see cref="ITimeToLiveCache"/> instance to the collection and returns the instance.
    /// </summary>
    /// <typeparam name="TCache">The type of the cache instance, which must implement <see cref="ITimeToLiveCache"/>.</typeparam>
    /// <param name="instance">The cache instance to add to the collection.</param>
    /// <returns>The added cache instance.</returns>
    /// <remarks>
    /// If the instance is already in the collection, it is not added again.
    /// </remarks>
    public TCache Collect<TCache>(TCache instance) where TCache : ITimeToLiveCache
    {
        if (!CollectingCaches.Contains(instance))
            CollectingCaches.Add(instance);
        return instance;
    }
}

namespace CacheStorage;

/// <summary>
/// Provides a pooling context and thread to periodically remove all expired items from cache storages.
/// </summary>
public sealed class CachePoolingContext
{
    internal System.Timers.Timer collectionTimer;

    /// <summary>
    /// Gets or sets the pooling interval.
    /// </summary>
    public TimeSpan CollectInterval
    {
        get => TimeSpan.FromMilliseconds(this.collectionTimer.Interval);
        set => this.collectionTimer.Interval = value.TotalMilliseconds;
    }

    /// <summary>
    /// Gets an boolean indicating if the current pool is running.
    /// </summary>
    public bool IsCollecting
    {
        get => this.collectionTimer.Enabled;
        set => this.collectionTimer.Enabled = value;
    }

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
        this.collectionTimer = new System.Timers.Timer(interval.TotalMilliseconds);
        this.collectionTimer.Elapsed += this.CollectionTimer_Elapsed;
        this.CollectInterval = interval;
    }

    private void CollectionTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        this.CollectAll();
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
        this.collectionTimer.Start();
    }

    /// <summary>
    /// Stops the pooling collection.
    /// </summary>
    public void StopCollecting()
    {
        this.collectionTimer.Stop();
    }

    /// <summary>
    /// Collects all expired entities from monitored cache storages.
    /// </summary>
    /// <returns>
    /// the amount of removed entities from all cache storages.
    /// </returns>
    public int CollectAll()
    {
        lock (this.CollectingCaches)
        {
            int count = 0;
            for (int i = 0; i < this.CollectingCaches.Count; i++)
            {
                var cache = this.CollectingCaches[i];
                count += cache.RemoveExpiredEntities();
            }
            return count;
        }
    }
}

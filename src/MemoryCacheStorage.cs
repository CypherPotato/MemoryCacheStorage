using System.Collections.Concurrent;

namespace CypherPotato
{
    /// <summary>
    /// Represents a specialized instance of the <see cref="MemoryCacheStorage{TKey, TValue}"/> class
    /// with <see cref="string"/> as the key type and <see cref="object"/> as the value type.
    /// </summary>
    public class MemoryCacheStorage : MemoryCacheStorage<string, object>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheStorage"/> class with default settings.
        /// </summary>
        public MemoryCacheStorage() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheStorage"/> class with the specified parameters.
        /// </summary>
        /// <param name="collectPoolInterval">The interval for collecting expired items from the cache pool.</param>
        /// <param name="defaultExpiration">The default expiration time for items in the cache.</param>
        public MemoryCacheStorage(TimeSpan collectPoolInterval, TimeSpan defaultExpiration)
            : base(collectPoolInterval, defaultExpiration) { }
    }

    /// <summary>
    /// Represents a TTL memory cache storage implementation.
    /// </summary>
    /// <typeparam name="TKey">The type of the cache keys.</typeparam>
    /// <typeparam name="TValue">The type of the cache values.</typeparam>
    public class MemoryCacheStorage<TKey, TValue> : IDisposable where TKey : notnull
    {
        private readonly ConcurrentCacheStack<TKey, TValue> cacheStack;

        /// <summary>
        /// Gets or sets the default expiration time for items in the cache.
        /// </summary>
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Gets or sets the cache collection interval.
        /// </summary>
        public TimeSpan CachePoolInterval { get => cacheStack.timerInterval; }

        /// <summary>
        /// Gets the count of items in the cache.
        /// </summary>
        public int Count { get => cacheStack.SafeCount(); }

        /// <summary>
        /// Gets the <see cref="IComparer{T}"/> for the key comparer.
        /// </summary>
        public IComparer<TKey>? KeyComparer { get => cacheStack.kcomparer; }

        #region CONSTRUCTORS
        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheStorage{TKey, TValue}"/> class with default settings.
        /// </summary>
        public MemoryCacheStorage()
        {
            cacheStack = new ConcurrentCacheStack<TKey, TValue>(TimeSpan.FromSeconds(30), null);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheStorage{TKey, TValue}"/> class with default settings.
        /// </summary>
        /// <param name="keyComparer">The <see cref="IComparer{T}"/> to comparing key values.</param>
        public MemoryCacheStorage(IComparer<TKey> keyComparer)
        {
            cacheStack = new ConcurrentCacheStack<TKey, TValue>(TimeSpan.FromSeconds(30), keyComparer);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheStorage{TKey, TValue}"/> class with the specified parameters.
        /// </summary>
        /// <param name="collectPoolInterval">The interval for collecting expired items from the cache pool.</param>
        /// <param name="defaultExpiration">The default expiration time for items in the cache.</param>
        public MemoryCacheStorage(TimeSpan collectPoolInterval, TimeSpan defaultExpiration)
        {
            DefaultExpiration = defaultExpiration;
            cacheStack = new ConcurrentCacheStack<TKey, TValue>(collectPoolInterval, null);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheStorage{TKey, TValue}"/> class with the specified parameters.
        /// </summary>
        /// <param name="keyComparer">The <see cref="IComparer{T}"/> to comparing key values.</param>
        /// <param name="collectPoolInterval">The interval for collecting expired items from the cache pool.</param>
        /// <param name="defaultExpiration">The default expiration time for items in the cache.</param>
        public MemoryCacheStorage(IComparer<TKey> keyComparer, TimeSpan collectPoolInterval, TimeSpan defaultExpiration)
        {
            DefaultExpiration = defaultExpiration;
            cacheStack = new ConcurrentCacheStack<TKey, TValue>(collectPoolInterval, keyComparer);
        }
        #endregion

        #region SETTERS
        /// <summary>
        /// Renews the expiration time of all items that contain one or more of the entered IDs.
        /// </summary>
        /// <param name="ids">The list of IDs to look for on items to renew.</param>
        /// <param name="newExpiration">The new expiration date and time.</param>
        /// <returns>The count of renewed cached items.</returns>
        public int Renew(TKey[] ids, DateTime newExpiration)
        {
            int n = 0;
            cacheStack.SafeInvoke(() =>
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    var id = ids[i];
                    n += cacheStack.UnsafeRenewSingle(id, newExpiration);
                }
            });
            return n;
        }

        /// <summary>
        /// Renews the expiration time of all items that contains the specified id.
        /// </summary>
        /// <param name="id">The ID which the items has to renew.</param>
        /// <param name="newExpiration">The new expiration date and time.</param>
        /// <returns>The count of renewed cached items.</returns>
        public int Renew(TKey id, DateTime newExpiration)
        {
            int n = 0;
            cacheStack.SafeInvoke(() =>
            {
                n += cacheStack.UnsafeRenewSingle(id, newExpiration);
            });
            return n;
        }

        /// <summary>
        /// Renews the expiration time of all items that contains the specified id with the default expiration time.
        /// </summary>
        /// <param name="id">The ID which the items has to renew.</param>
        /// <returns>The count of renewed cached items.</returns>
        public int Renew(TKey id)
        {
            int n = 0;
            cacheStack.SafeInvoke(() =>
            {
                n += cacheStack.UnsafeRenewSingle(id, DateTime.Now + DefaultExpiration);
            });
            return n;
        }

        /// <summary>
        /// Adds an item to the cache with the specified IDs, value, and expiration time.
        /// </summary>
        /// <param name="ids">The array of cache keys.</param>
        /// <param name="value">The value to be cached.</param>
        /// <param name="expiresAt">The expiration time for the cached item.</param>
        public void Add(TKey[] ids, TValue value, DateTime expiresAt)
        {
            cacheStack.SafeAdd(ids, value, expiresAt);
        }

        /// <summary>
        /// Adds an item to the cache with the specified ID, value, and time to live.
        /// </summary>
        /// <param name="ids">The array of cache keys.</param>
        /// <param name="value">The value to be cached.</param>
        /// <param name="timeToLive">The time to live for the cached item.</param>
        public void Add(TKey[] ids, TValue value, TimeSpan timeToLive)
        {
            Add(ids, value, DateTime.Now.Add(timeToLive));
        }

        /// <summary>
        /// Adds an item to the cache with the specified ID, value, and expiration time.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="value">The value to be cached.</param>
        /// <param name="expiresAt">The expiration time for the cached item.</param>
        public void Add(TKey id, TValue value, DateTime expiresAt)
        {
            Add(new TKey[] { id }, value, expiresAt);
        }

        /// <summary>
        /// Adds an item to the cache with the specified ID, value, and time to live.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="value">The value to be cached.</param>
        /// <param name="timeToLive">The time to live for the cached item.</param>
        public void Add(TKey id, TValue value, TimeSpan timeToLive)
        {
            Add(new TKey[] { id }, value, timeToLive);
        }

        /// <summary>
        /// Adds an item to the cache with the specified ID, value, and default expiration time.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="value">The value to be cached.</param>
        public void Add(TKey id, TValue value)
        {
            Add(new TKey[] { id }, value, DateTime.Now.Add(DefaultExpiration));
        }

        /// <summary>
        /// Asynchronously adds an item to the cache with the specified IDs, value, and expiration time.
        /// </summary>
        /// <param name="ids">The array of cache keys.</param>
        /// <param name="value">The value to be cached.</param>
        /// <param name="expiresAt">The expiration time for the cached item.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddAsync(TKey[] ids, TValue value, DateTime expiresAt)
            => await Task.Run(() => Add(ids, value, expiresAt));

        /// <summary>
        /// Asynchronously adds an item to the cache with the specified IDs, value, and time to live.
        /// </summary>
        /// <param name="ids">The array of cache keys.</param>
        /// <param name="value">The value to be cached.</param>
        /// <param name="timeToLive">The time to live for the cached item.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddAsync(TKey[] ids, TValue value, TimeSpan timeToLive)
            => await Task.Run(() => Add(ids, value, timeToLive));

        /// <summary>
        /// Asynchronously adds an item to the cache with the specified ID, value, and expiration time.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="value">The value to be cached.</param>
        /// <param name="expiresAt">The expiration time for the cached item.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddAsync(TKey id, TValue value, DateTime expiresAt)
            => await Task.Run(() => Add(id, value, expiresAt));

        /// <summary>
        /// Asynchronously adds an item to the cache with the specified ID, value, and time to live.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="value">The value to be cached.</param>
        /// <param name="timeToLive">The time to live for the cached item.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddAsync(TKey id, TValue value, TimeSpan timeToLive)
            => await Task.Run(() => Add(id, value, timeToLive));

        /// <summary>
        /// Asynchronously adds an item to the cache with the specified ID, value, and default expiration time.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="value">The value to be cached.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddAsync(TKey id, TValue value)
            => await Task.Run(() => Add(id, value, DefaultExpiration));
        #endregion

        #region GETTERS
        /// <summary>
        /// Gets the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="matchId">The cache key to match.</param>
        /// <returns>The value associated with the key, or <c>null</c> if the key is not found.</returns>
        public TValue? Get(TKey matchId)
        {
            var match = cacheStack.SafeFirstMatch(matchId);
            return match.Value;
        }

        /// <summary>
        /// Gets all values associated with the specified key from the cache.
        /// </summary>
        /// <param name="matchId">The cache key to match.</param>
        /// <returns>An enumerable collection of values associated with the key.</returns>
        public IEnumerable<TValue> GetAll(TKey matchId)
        {
            return cacheStack.SafeAllMatch(matchId).Select(s => s.Value!);
        }

        /// <summary>
        /// Gets all values that satisfy the specified predicate from the cache.
        /// </summary>
        /// <param name="predicate">The predicate function to filter keys.</param>
        /// <returns>An enumerable collection of values that satisfy the predicate.</returns>
        public IEnumerable<TValue> GetAll(Func<TKey[], bool> predicate)
        {
            return cacheStack.SafeMatch(predicate).Select(s => s.Value!);
        }

        /// <summary>
        /// Tries to get the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="matchId">The cache key to match.</param>
        /// <param name="value">When this method returns, contains the value associated with the key, if found; otherwise, the default value.</param>
        /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
        public bool TryGet(TKey matchId, out TValue? value)
        {
            var match = cacheStack.SafeFirstMatch(matchId);
            value = match.Value;
            return match.Keys.Length > 0;
        }

        /// <summary>
        /// Asynchronously gets the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="matchId">The cache key to match.</param>
        /// <returns>A task representing the asynchronous operation, containing the value associated with the key, or <c>null</c> if the key is not found.</returns>
        public async Task<TValue?> GetAsync(TKey matchId)
            => await Task.Run(() => Get(matchId));

        /// <summary>
        /// Asynchronously gets all values associated with the specified key from the cache.
        /// </summary>
        /// <param name="matchId">The cache key to match.</param>
        /// <returns>A task representing the asynchronous operation, containing an enumerable collection of values associated with the key.</returns>
        public async Task<IEnumerable<TValue>> GetAllAsync(TKey matchId)
            => await Task.Run(() => GetAll(matchId));

        /// <summary>
        /// Asynchronously gets all values that satisfy the specified predicate from the cache.
        /// </summary>
        /// <param name="predicate">The predicate function to filter keys.</param>
        /// <returns>A task representing the asynchronous operation, containing an enumerable collection of values that satisfy the predicate.</returns>
        public async Task<IEnumerable<TValue>> GetAllAsync(Func<TKey[], bool> predicate)
            => await Task.Run(() => GetAll(predicate));
        #endregion

        #region REMOVERS
        /// <summary>
        /// Removes the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="matchId">The cache key to match.</param>
        /// <returns>The number of items removed from the cache.</returns>
        public int Remove(TKey matchId)
        {
            return cacheStack.SafeRemove(matchId);
        }

        /// <summary>
        /// Removes all values that satisfy the specified predicate from the cache.
        /// </summary>
        /// <param name="predicate">The predicate function to filter keys.</param>
        /// <returns>The number of items removed from the cache.</returns>
        public int Remove(Func<TKey[], bool> predicate)
        {
            return cacheStack.SafeRemove(predicate);
        }

        /// <summary>
        /// Removes all expired items from the cache.
        /// </summary>
        /// <returns>The number of items removed from the cache.</returns>
        public int Collect()
        {
            return cacheStack.SafeCollect();
        }

        /// <summary>
        /// Removes all items from the cache, including the not expired ones.
        /// </summary>
        public void Clear()
        {
            cacheStack.SafeClear();
        }

        /// <summary>
        /// Asynchronously removes the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="matchId">The cache key to match.</param>
        /// <returns>A task representing the asynchronous operation, containing the number of items removed from the cache (0 or 1).</returns>
        public async Task<int> RemoveAsync(TKey matchId)
            => await Task.Run(() => Remove(matchId));

        /// <summary>
        /// Asynchronously removes all values that satisfy the specified predicate from the cache.
        /// </summary>
        /// <param name="predicate">The predicate function to filter keys.</param>
        /// <returns>A task representing the asynchronous operation, containing the number of items removed from the cache.</returns>
        public async Task<int> RemoveAsync(Func<TKey[], bool> predicate)
            => await Task.Run(() => Remove(predicate));

        /// <summary>
        /// Asynchronously removes all expired items from the cache.
        /// </summary>
        /// <returns>A task representing the asynchronous operation, containing the number of items removed from the cache.</returns>
        public async Task<int> CollectAsync()
            => await Task.Run(() => Collect());
        #endregion

        #region FUNCTION HELPERS

        /// <summary>
        /// Invokes the specified function to obtain a value and caches it using the specified key.
        /// If the key is already in the cache, returns the cached value; otherwise, adds the value to the cache with the specified expiration time.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="expiresAfter">The expiration time for the cached item.</param>
        /// <param name="function">The function to obtain the value if not already cached.</param>
        /// <returns>The cached value.</returns>
        public TValue GetOrAdd(TKey id, TimeSpan expiresAfter, Func<TValue> function)
        {
            if (TryGet(id, out var cachedValue))
            {
                return cachedValue!;
            }
            else
            {
                TValue value = function();
                Add(id, value, DateTime.Now.Add(expiresAfter));
                return value;
            }
        }

        /// <summary>
        /// Invokes the specified function to obtain a value and caches it using the specified key with the default expiration time.
        /// If the key is already in the cache, returns the cached value; otherwise, adds the value to the cache with the default expiration time.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="function">The function to obtain the value if not already cached.</param>
        /// <returns>The cached value.</returns>
        public TValue GetOrAdd(TKey id, Func<TValue> function)
        {
            return GetOrAdd(id, DefaultExpiration, function);
        }

        /// <summary>
        /// Asynchronously invokes the specified function to obtain a value and caches it using the specified key.
        /// If the key is already in the cache, returns the cached value; otherwise, adds the value to the cache with the specified expiration time.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="expiresAfter">The expiration time for the cached item.</param>
        /// <param name="function">The asynchronous function to obtain the value if not already cached.</param>
        /// <returns>A task representing the asynchronous operation, containing the cached value.</returns>
        public async Task<TValue> GetOrAddAsync(TKey id, TimeSpan expiresAfter, Func<Task<TValue>> function)
        {
            if (TryGet(id, out var cachedValue))
            {
                return cachedValue!;
            }
            else
            {
                TValue value = await function();
                await AddAsync(id, value, DateTime.Now.Add(expiresAfter));
                return value;
            }
        }

        /// <summary>
        /// Asynchronously invokes the specified function to obtain a value and caches it using the specified key.
        /// If the key is already in the cache, returns the cached value; otherwise, adds the value to the cache with the default expiration time.
        /// </summary>
        /// <param name="id">The cache key.</param>
        /// <param name="function">The asynchronous function to obtain the value if not already cached.</param>
        /// <returns>A task representing the asynchronous operation, containing the cached value.</returns>
        public async Task<TValue> GetOrAddAsync(TKey id, Func<Task<TValue>> function)
        {
            return await GetOrAddAsync(id, DefaultExpiration, function);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="MemoryCacheStorage{TKey, TValue}"/>.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            cacheStack.Dispose();
        }
        #endregion
    }
}
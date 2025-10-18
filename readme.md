<div align="center" style="display:grid;place-items:center;">
  <p>
      <img src="./.assets/icon.png">
  </p>
  <h1>Memory Cache Storage</h1>
</div>

This is a tiny implementation of a TTL (time-to-live) in-memory cache, where each stored object has a lifetime to be retrieved.

It is an lightweight alternative to [System.Runtime.Caching](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.caching.memorycache?view=dotnet-plat-ext-8.0), which in turn offers:

- thread-safe add, remove or query cache items.
- async API.
- almost 2x faster than System.Runtime.Caching.
- supports enumerating and querying cached items, using their IDs.
- hybrid caching strategy of lazy caching + pooling.

The "lazy" strategy consists of validating whether the cached object is still fresh at the time you obtain it. The other strategy, long-pooling, regularly runs a check on all cached objects in order to validate whether they are expired or not, allowing the GC to do its work for objects that should no longer be kept in the cache.

The storage mechanism of this implementation allows an object to be stored with multiple keys, and multiple objects can share the same key. This is possible because it makes grouping of cache items possible, making it easy to get the objects with the `GetAll()` and `RemoveAll()` query functions.

### Creating the MemoryCacheStorage

```cs
// defines an in-memory cache where each cached item
// is fresh for 10 minutes
MemoryCacheStorage<int, string> cache = new MemoryCacheStorage<int, string> () {
    DefaultExpiration = TimeSpan.FromMinutes ( 10 )
};

// or create an list-powered cache with the same behavior
MemoryCacheList<string> listCache = new MemoryCacheList<string> ();
```

### Adding items to cache

```cs
// add an item to the cache
cache.Add ( 10, "hello" );

// add or renew an item in the cache. if an item with the same key
// already exists, it won't be replaced, but have its expiration renewed.
// if the item does not exist, it will be added to the cache.
cache.AddOrRenew ( 10, "hey" );

// get an item from the cache using the following expression.
// if the item does not exist, the lambda expression will be executed
string result = cache.GetOrAdd ( 10, () => "hello" );
```

### Querying items from cache

```cs
// gets an cached item by their key. throws an exception if the item
// doenst exists or is expired
string item = cache [ 10 ];

// attempts to get an item by their key. if the item is expired or
// not found, the result will be false and no result will be outputted
if (cache.TryGetValue ( 10, out string result )) {
    ;
}

// checks if an item is present and not expired
if (cache.ContainsKey ( 10 )) {
    ;
}

// IEnumerable over the cache store
if (cache.Where ( s => s.StartsWith ( "hello" ) ).Any ()) {
    ;
}
```

### Removing items from cache

```cs
 // attempts to remove an cached item by their id
 cache.Remove ( 10 );
 
 // or removes only if expired
 cache.RemoveIfExpired ( 10 );

 // or removes all expired items
 cache.RemoveExpiredEntities ();
 
 // or just remove everything
 cache.Clear();
```

### Cache events

```cs
cache.AddItemCallback += ( sender, addedItem ) => {
    // do something
};

cache.RemoveItemCallback += ( sender, removedItem ) => {
    // dispose the removed item
    (removedItem as IDisposable)?.Dispose ();
};
```

### Cache-collect pool

```cs
// creates an cache pool which collects multiple expired items from the specified 
// cache storages every 30 minutes.
CachePoolingContext pool = new CachePoolingContext ( TimeSpan.FromMinutes ( 30 ) );
pool.CollectingCaches.Add ( cache1 );
pool.CollectingCaches.Add ( cache2 );
pool.CollectingCaches.Add ( cache3 );
pool.StartCollecting ();

// or
CachePoolingContext pool = CachePoolingContext.StartNew ( 
    TimeSpan.FromMinutes ( 30 ), cache1, cache2, cache3...);
```

# Benchmark

Last benchmark run:

```
BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.6899)
11th Gen Intel(R) Core(TM) i5-11400F @ 2.60GHz (2.59 GHz)
.NET SDK 10.0.100-rc.2.25502.107
  [Host]   : .NET 9.0.10 (9.0.1025.47515), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI [AttachedDebugger]
  .NET 9.0 : .NET 9.0.10 (9.0.1025.47515), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
```

| Method                             | Categories | Operations | Mean        | Error     | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|----------------------------------- |----------- |----------- |------------:|----------:|-----------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| Add_MemoryCacheStorage             | Add        | 1000       |   116.36 us |  1.174 us |   1.099 us |  1.00 |    0.00 |  41.6260 |  13.7939 |        - |  262000 B |        1.00 |
| Add_SystemRuntimeMemoryCache       | Add        | 1000       |   444.77 us |  5.300 us |   4.958 us |  3.82 |    0.05 |  67.3828 |  25.3906 |        - |  425367 B |        1.62 |
| Add_MicrosoftExtensionsMemoryCache | Add        | 1000       |   125.02 us |  1.580 us |   1.478 us |  1.07 |    0.02 |  52.9785 |  17.5781 |        - |  333287 B |        1.27 |
|                                    |            |            |             |           |            |       |         |          |          |          |           |             |
| Add_MemoryCacheStorage             | Add        | 10000      | 3,607.52 us | 72.036 us | 176.704 us |  1.00 |    0.00 | 328.1250 | 242.1875 |  85.9375 | 1959366 B |        1.00 |
| Add_SystemRuntimeMemoryCache       | Add        | 10000      | 4,792.71 us | 25.943 us |  24.267 us |  1.40 |    0.05 | 539.0625 | 453.1250 |        - | 3401090 B |        1.74 |
| Add_MicrosoftExtensionsMemoryCache | Add        | 10000      | 4,827.84 us | 95.833 us | 281.060 us |  1.32 |    0.08 | 453.1250 | 304.6875 | 101.5625 | 2678770 B |        1.37 |
|                                    |            |            |             |           |            |       |         |          |          |          |           |             |
| Get_MemoryCacheStorage             | Get        | 1000       |    34.52 us |  0.073 us |   0.065 us |  1.00 |    0.00 |        - |        - |        - |         - |          NA |
| Get_SystemRuntimeMemoryCache       | Get        | 1000       |    93.35 us |  0.130 us |   0.102 us |  2.70 |    0.01 |   5.0049 |        - |        - |   32000 B |          NA |
| Get_MicrosoftExtensionsMemoryCache | Get        | 1000       |    33.78 us |  0.637 us |   0.626 us |  0.98 |    0.02 |        - |        - |        - |         - |          NA |
|                                    |            |            |             |           |            |       |         |          |          |          |           |             |
| Get_MemoryCacheStorage             | Get        | 10000      |   413.37 us |  2.545 us |   2.256 us |  1.00 |    0.00 |        - |        - |        - |         - |          NA |
| Get_SystemRuntimeMemoryCache       | Get        | 10000      | 1,044.81 us |  3.775 us |   3.531 us |  2.53 |    0.02 |  50.7813 |        - |        - |  320000 B |          NA |
| Get_MicrosoftExtensionsMemoryCache | Get        | 10000      |   431.64 us |  2.465 us |   2.059 us |  1.04 |    0.01 |        - |        - |        - |         - |          NA |

# Contributing

We welcome contributions! If you have ideas for improvements or find any issues, please open an issue or submit a pull request.

# License

This project is licensed under the MIT License - see the LICENSE.md file for details.
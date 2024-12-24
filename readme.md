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
- almost 10x faster than System.Runtime.Caching. - [benchmark](https://gist.github.com/CypherPotato/1b0e1bdecd8b20d72a05cf7ad88c5b80)
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

# Contributing

We welcome contributions! If you have ideas for improvements or find any issues, please open an issue or submit a pull request.

# License

This project is licensed under the MIT License - see the LICENSE.md file for details.
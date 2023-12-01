<div align="center" style="display:grid;place-items:center;">
  <p>
      <img src="./.assets/icon.png">
  </p>
  <h1>Memory Cache Storage</h1>
</div>

This is a tiny implementation of a TTL (time-to-live) in-memory cache, where each stored object has a lifetime to be retrieved.

It is an lightweight alternative to [System.Runtime.Caching](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.caching.memorycache?view=dotnet-plat-ext-8.0), which in turn offers:

- thread-safe add, remove or query cache items.
- supports adding items to cache with multiple IDs.
- supports enumerating and querying cached items, using their IDs.
- hybrid caching strategy of lazy caching + pooling.

The "lazy" strategy consists of validating whether the cached object is still valid at the time you obtain it. The other strategy, long-pooling, regularly runs a check on all cached objects in order to validate whether they are expired or not, allowing the GC to do its work for objects that should no longer be kept in the cache.

The storage mechanism of this implementation allows an object to be stored with multiple keys, and multiple objects can share the same key. This is possible because it makes grouping of cache items possible, making it easy to get the objects with the `GetAll()` and `RemoveAll()` query functions.

### Creating the MemoryCacheStorage

```cs
// create an cache which will store strings and use ints as their keys
MemoryCacheStorage<int, string> cache = new MemoryCacheStorage<int, string>();

// create an cache which will collect expired items each 5 minutes
// and expires items after 10 minutes
MemoryCacheStorage<int, string> cache = new MemoryCacheStorage<int, string>(
    collectPoolInterval: TimeSpan.FromMinutes(5),
    defaultExpiration: TimeSpan.FromMinutes(10)
);

// create an generic MemoryCache which is the same as
// MemoryCacheStorage<string, object>
MemoryCacheStorage cache = new MemoryCacheStorage();
```

### Adding items to cache

```cs
// add "hello, world" item with id 1
cache.Add(1, "hello, world");

// add an cache item with pre-determined expiration
// date and time
cache.Add(2, "hello, world", DateTime.Now.AddDays(10));

// add an cache item with multiple keys at the same
// time
cache.Add([1, 4, 6], "hello", TimeSpan.FromSeconds(60));

// invokes the expression and caches their value in the specified id.
// if the value is already cached, returns the cached item
// instead running the expression again.
string item = cache.GetOrAdd(10, () => "hello");
```

### Querying items from cache

```cs
// gets the first cache item that matches the given key. it should
// return "null" when using with an reference type if the cache
// item was not found in the storage, or "default" when using with an value-type.
string? hello = cache.Get(1);

// gets all cache items which contains "4" in their key
IEnumerable<string> items = cache.GetAll(4);

// gets all cache items which matches the specified predicate
IEnumerable<string> pattern = cache.GetAll(keys => keys.Contains(4) && keys.Contains(5));

// tries to get an cache item by their id and return an boolean
// indicating if the id is cached or not.
if (cache.TryGet(5, out string value))
{
    // do something with value here
}
```

### Removing items from cache

```cs
// invalidates the first cache item which has '5' in their
// key collection
cache.Remove(5);

// invalidates all cache items which matches the specified
// predicate
cache.Remove(keys => keys.Contains(5) && keys.Contains(6));

// force an full cache collect, which removes all expired items
// from their storage
cache.Collect();

// removes all items from the cache, including the not expired ones.
cache.Clear();

// disposes the cache interface
cache.Dispose();
```

# Contributing

We welcome contributions! If you have ideas for improvements or find any issues, please open an issue or submit a pull request.

# License

This project is licensed under the MIT License - see the LICENSE.md file for details.
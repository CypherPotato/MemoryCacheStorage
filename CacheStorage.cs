using System.Numerics;

namespace CacheStorage
{
    /// <summary>
    /// Provides a thread-safe memory cache that stores objects for a certain period of time.
    /// </summary>
    public class MemoryCacheStorage
    {
        private List<CacheStorageItem> _collection = new List<CacheStorageItem>();
        private long incrementingId = 0;

        /// <summary>
        /// Gets or sets the default expirity time for newly cached objects.
        /// </summary>
        public TimeSpan DefaultExpirity { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Clears the cache memory and restarts the ID generator to zero.
        /// </summary>
        public void Clear()
        {
            lock (_collection)
            {
                Interlocked.Exchange(ref incrementingId, 0);
                _collection.Clear();
            }
        }

        /// <summary>
        /// Attempts to gets a cached value through its ID, and if successful, a true boolean value is returned.
        /// </summary>
        /// <param name="id">The ID of the cached object.</param>
        /// <param name="value">The cached object, returned by reference.</param>
        /// <returns>A Boolean indicating whether the object was found or not.</returns>
        public bool TryGetValue(long id, out object? value)
        {
            lock (_collection)
            {
                foreach (var item in _collection)
                {
                    if (item.Identifier == id)
                    {
                        value = item.Value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Attempts to gets a cached value through their tag part, and if successful, a true boolean value is returned.
        /// </summary>
        /// <param name="tag">One of the tags that must be present on the object.</param>
        /// <param name="value">The cached object, returned by reference.</param>
        /// <returns>A Boolean indicating whether the object was found or not.</returns>
        public bool TryGetValue(string tag, out object? value)
        {
            lock (_collection)
            {
                foreach (var item in _collection)
                {
                    if (item.Tags?.Contains(tag) == true)
                    {
                        value = item.Value;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Returns an array of all cached objects that share the referenced tag.
        /// </summary>
        /// <param name="tag">The tag present on all stored objects that will return in this query.</param>
        /// <returns>An enumerable of objects with the values found in those tags.</returns>
        public IEnumerable<object?> GetValuesByTag(string tag)
        {
            lock (_collection)
            {
                foreach (var item in _collection)
                {
                    if (item.Tags?.Contains(tag) == true)
                    {
                        yield return item;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to anticipate the invalidation of an cached object by its ID and returns a boolean indicating the success of the operation.
        /// </summary>
        /// <param name="id">The ID of the cached object.</param>
        /// <returns>An boolean indicating if the object was invalidated or not.</returns>
        public bool Invalidate(long id)
        {
            lock (_collection)
            {
                foreach (var item in _collection)
                {
                    if (item.Identifier == id)
                    {
                        item.Invalidate();
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Attempts to invalidate all cached objects that contains the specified tag.
        /// </summary>
        /// <param name="tag">One of the tags that must be present on the cached objects.</param>
        /// <returns>An boolean indicating if the object was invalidated or not.</returns>
        public bool Invalidate(string tag)
        {
            bool removedAny = false;
            lock (_collection)
            {
                foreach (var item in _collection)
                {
                    if (item.Tags?.Contains(tag) == true)
                    {
                        item.Invalidate();
                        removedAny = true;
                    }
                }
            }
            return removedAny;
        }

        /// <summary>
        /// Stores a value in cache.
        /// </summary>
        /// <param name="value">The value which will be cached.</param>
        /// <param name="expirity">The time for the object to be invalidated in the cache.</param>
        /// <param name="tags">Tags to be able to fetch that object later.</param>
        /// <returns>Returns the ID of the cached object.</returns>
        public long Set(object? value, TimeSpan expirity, string[]? tags)
        {
            Console.WriteLine("object added: " + string.Join(", ", tags ?? Array.Empty<string>()));

            // verify if the incrementing Id is larger than long.maxvalue
            Interlocked.CompareExchange(ref incrementingId, 0, long.MaxValue - 1);

            CacheStorageItem i = new CacheStorageItem(ref value, _collection, expirity)
            {
                Identifier = Interlocked.Increment(ref incrementingId),
                Tags = tags
            };
            return i.Identifier;
        }

        /// <summary>
        /// Stores a value in cache.
        /// </summary>
        /// <param name="value">The value which will be cached.</param>
        /// <param name="expirity">The time for the object to be invalidated in the cache.</param>
        /// <param name="tag">Tag to be able to fetch that object later.</param>
        /// <returns>Returns the ID of the cached object.</returns>
        public long Set(object? value, TimeSpan expirity, string tag)
        {
            return Set(value, expirity, new string[] { tag });
        }

        /// <summary>
        /// Stores a value in cache using the default expirity time.
        /// </summary>
        /// <param name="value">The value which will be cached.</param>
        /// <param name="tag">Tag to be able to fetch that object later.</param>
        /// <returns>Returns the ID of the cached object.</returns>
        public long Set(object? value, string tag)
        {
            return Set(value, DefaultExpirity, new string[] { tag });
        }

        /// <summary>
        /// Stores a value in cache using the default expirity time.
        /// </summary>
        /// <param name="value">The value which will be cached.</param>
        /// <returns>Returns the ID of the cached object.</returns>
        public long Set(object? value)
        {
            return Set(value, DefaultExpirity, Array.Empty<string>());
        }

        private class CacheStorageItem
        {
            public object? Value { get; private set; }
            public long Identifier { get; set; }
            public string[]? Tags { get; set; }
            public TimeSpan Expires { get; private set; }
            private List<CacheStorageItem> parentCollection;
            private bool isInvalidated = false;

            private async void CollectionThread()
            {
                await Task.Delay(Expires);
                Invalidate();
            }

            internal void Invalidate()
            {
                if (isInvalidated) return;
                Value = null;
                lock (parentCollection)
                {
                    isInvalidated = true;
                    parentCollection.Remove(this);
                }
            }

            public CacheStorageItem(ref object? value, List<CacheStorageItem> parent, TimeSpan expirity)
            {
                this.Value = value;
                this.parentCollection = parent;
                this.Expires = expirity;

                lock (parent)
                {
                    parentCollection.Add(this);
                }

                CollectionThread();
            }

            public override bool Equals(object? obj)
            {
                CacheStorageItem? other = obj as CacheStorageItem;
                if (other == null) return false;
                if (other.Identifier == this.Identifier) return true;
                return false;
            }

            public override int GetHashCode()
            {
                return (int)this.Identifier;
            }
        }
    }
}
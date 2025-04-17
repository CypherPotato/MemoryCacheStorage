using System.Collections;

namespace CacheStorage;

/// <summary>
/// Represents an TTL list implementation, where it's items expires after some time.
/// </summary>
/// <typeparam name="TValue">The object type of the list.</typeparam>
public class MemoryCacheList<TValue> : IList<TValue>, ITimeToLiveCache, ICachedCallbackHandler<TValue> {
    internal List<CacheItem<TValue>> items;

    /// <summary>
    /// Creates an new <see cref="MemoryCacheList{TValue}"/> from the specified parameters.
    /// </summary>
    public MemoryCacheList () {
        items = new List<CacheItem<TValue>> ();
    }

    /// <summary>
    /// Gets or sets the default expiration time for newly added items.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes ( 10 );

    internal bool SetCachedItem ( TValue item, TimeSpan expiration ) {
        lock (items) {
            if (AddItemCallback is not null)
                AddItemCallback ( this, item );

            items.Add ( new CacheItem<TValue> ( item, DateTime.Now.Add ( expiration ) ) );
        }
        return true;
    }

    internal TValue GetByIndex ( int index ) {
        lock (items) {
            var item = items [ index ];
            if (item.IsExpired ()) {
                throw new IndexOutOfRangeException ( "The specified index was not defined or the item at the specified index has expired." );
            }
            return item.Value;
        }
    }

    internal void SetByIndex ( int index, TValue item ) {
        lock (items) {
            if (items.Count > index && RemoveItemCallback is not null)
                RemoveItemCallback ( this, items [ index ].Value );

            if (AddItemCallback is not null)
                AddItemCallback ( this, item );

            items [ index ] = new CacheItem<TValue> ( item, DateTime.Now.Add ( DefaultExpiration ) );
        }
    }

    /// <inheritdoc/>
    public TValue this [ int index ] { get => GetByIndex ( index ); set => SetByIndex ( index, value ); }

    /// <summary>
    /// Gets the count of non-expired entities in this list.
    /// </summary>
    public int Count {
        get {
            int count = 0;
            using (var enumerator = GetEnumerator ()) {
                while (enumerator.MoveNext ())
                    count++;
            }
            return count;
        }
    }

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public event CachedItemHandler<TValue>? AddItemCallback;

    /// <inheritdoc/>
    public event CachedItemHandler<TValue>? RemoveItemCallback;

    /// <summary>
    /// Caches an object and adds it to the end of this collection.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add ( TValue item ) {
        SetCachedItem ( item, DefaultExpiration );
    }

    /// <summary>
    /// Caches an object and adds it to the end of this collection with the specified expiration time.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="expiresAt">The expiration time.</param>
    public void Add ( TValue item, TimeSpan expiresAt ) {
        SetCachedItem ( item, expiresAt );
    }

    /// <summary>
    /// Searches and renews an existing and cached <typeparamref name="TValue"/>. If the value is already expired, an new
    /// entry is added in the cache store.
    /// </summary>
    /// <param name="item">The item to add or renew.</param>
    /// <param name="expiresAt">The expiration time.</param>
    public void AddOrRenew ( TValue item, TimeSpan expiresAt ) {
        lock (items) {
            for (int i1 = 0; i1 < items.Count; i1++) {
                CacheItem<TValue> i = items [ i1 ];
                if (i.Value?.Equals ( item ) == true) {
                    i.ExpiresAt = DateTime.Now + expiresAt;
                    return;
                }
            }

            // if the item was not found, add it
            if (AddItemCallback is not null)
                AddItemCallback ( this, item );

            items.Add ( new CacheItem<TValue> ( item, DateTime.Now.Add ( expiresAt ) ) );
        }
    }

    /// <summary>
    /// Searches and renews an existing and cached <typeparamref name="TValue"/> using the default expiration time. If
    /// the value is already expired, an newentry is added in the cache store.
    /// </summary>
    /// <param name="item">The item to add or renew.</param>
    public void AddOrRenew ( TValue item ) => AddOrRenew ( item, DefaultExpiration );

    /// <inheritdoc/>
    public void Clear () {
        lock (items) {
            if (RemoveItemCallback is not null) {
                for (int i1 = 0; i1 < items.Count; i1++) {
                    CacheItem<TValue>? i = items [ i1 ];
                    RemoveItemCallback ( this, i.Value );
                }
            }

            items.Clear ();
        }
    }

    /// <summary>
    /// Gets an boolean indicating this list contains any non-expired entity that is equals to the
    /// specified object.
    /// </summary>
    /// <param name="item">The object to compare to.</param>
    /// <returns></returns>
    public bool Contains ( TValue item ) {
        lock (items) {
            foreach (var i in items)
                if (!i.IsExpired () && i.Value?.Equals ( item ) == true)
                    return true;
        }
        return false;
    }

    /// <summary>
    /// Copies all the non-expired elements to another array.
    /// </summary>
    public void CopyTo ( TValue [] array, int arrayIndex ) {
        lock (items) {
            this.ToArray ().CopyTo ( array, arrayIndex );
        }
    }

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator () {
        lock (items) {
            for (int i = 0; i < items.Count; i++) {
                CacheItem<TValue> item = items [ i ];
                if (!item.IsExpired ()) {
                    yield return item.Value;
                }
            }
        }
    }

    /// <inheritdoc/>
    public int IndexOf ( TValue item ) {
        lock (items) {
            return this.ToList ().IndexOf ( item );
        }
    }

    /// <inheritdoc/>
    public void Insert ( int index, TValue item ) {
        lock (items) {
            CacheItem<TValue> c = new CacheItem<TValue> ( item, DateTime.Now.Add ( DefaultExpiration ) );

            if (AddItemCallback is not null)
                AddItemCallback ( this, item );

            items.Insert ( index, c );
        }
    }

    /// <inheritdoc/>
    public void Insert ( int index, TValue item, TimeSpan expiresAt ) {
        lock (items) {
            CacheItem<TValue> c = new CacheItem<TValue> ( item, DateTime.Now.Add ( expiresAt ) );

            if (AddItemCallback is not null)
                AddItemCallback ( this, item );

            items.Insert ( index, c );
        }
    }

    /// <inheritdoc/>
    public bool Remove ( TValue item ) {
        lock (items) {
            for (int i = 0; i < items.Count; i++) {
                var value = items [ i ].Value;
                if (value?.Equals ( item ) == true) {
                    if (RemoveItemCallback is not null)
                        RemoveItemCallback ( this, value );

                    items.RemoveAt ( i );
                    break;
                }
            }
        }
        return false;
    }

    /// <inheritdoc/>
    public void RemoveAt ( int index ) {
        lock (items) {
            var value = items [ index ].Value;

            if (RemoveItemCallback is not null)
                RemoveItemCallback ( this, value );

            items.RemoveAt ( index );
        }
    }

    IEnumerator IEnumerable.GetEnumerator () {
        return GetEnumerator ();
    }

    /// <inheritdoc/>
    public int RemoveExpiredEntities () {
        lock (items) {
            List<int> toRemove = new List<int> ( items.Count );
            for (int i = 0; i < items.Count; i++) {
                if (items [ i ].IsExpired ()) {
                    toRemove.Add ( i );
                }
            }

            toRemove.Reverse ();
            foreach (int key in toRemove) {
                var value = items [ key ].Value;

                if (RemoveItemCallback is not null)
                    RemoveItemCallback ( this, value );

                items.RemoveAt ( key );
            }

            return toRemove.Count;
        }
    }
}

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
        this.items = new List<CacheItem<TValue>> ();
    }

    /// <summary>
    /// Gets or sets the default expiration time for newly added items.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes ( 10 );

    internal bool SetCachedItem ( TValue item, TimeSpan expiration ) {
        lock (this.items) {
            if (this.AddItemCallback is not null)
                this.AddItemCallback ( this, item );

            this.items.Add ( new CacheItem<TValue> ( item, DateTime.Now.Add ( expiration ) ) );
        }
        return true;
    }

    internal TValue GetByIndex ( int index ) {
        lock (this.items) {
            var item = this.items [ index ];
            if (item.IsExpired ()) {
                throw new IndexOutOfRangeException ( "The specified index was not defined or the item at the specified index has expired." );
            }
            return item.Value;
        }
    }

    internal void SetByIndex ( int index, TValue item ) {
        lock (this.items) {
            if (this.items.Count > index && this.RemoveItemCallback is not null)
                this.RemoveItemCallback ( this, this.items [ index ].Value );

            if (this.AddItemCallback is not null)
                this.AddItemCallback ( this, item );

            this.items [ index ] = new CacheItem<TValue> ( item, DateTime.Now.Add ( this.DefaultExpiration ) );
        }
    }

    /// <inheritdoc/>
    public TValue this [ int index ] { get => this.GetByIndex ( index ); set => this.SetByIndex ( index, value ); }

    /// <summary>
    /// Gets the count of non-expired entities in this list.
    /// </summary>
    public int Count {
        get {
            int count = 0;
            using (var enumerator = this.GetEnumerator ()) {
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
        this.SetCachedItem ( item, this.DefaultExpiration );
    }

    /// <summary>
    /// Caches an object and adds it to the end of this collection with the specified expiration time.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="expiresAt">The expiration time.</param>
    public void Add ( TValue item, TimeSpan expiresAt ) {
        this.SetCachedItem ( item, expiresAt );
    }

    /// <summary>
    /// Searches and renews an existing and cached <typeparamref name="TValue"/>. If the value is already expired, an new
    /// entry is added in the cache store.
    /// </summary>
    /// <param name="item">The item to add or renew.</param>
    /// <param name="expiresAt">The expiration time.</param>
    public void AddOrRenew ( TValue item, TimeSpan expiresAt ) {
        lock (this.items) {
            for (int i1 = 0; i1 < this.items.Count; i1++) {
                CacheItem<TValue> i = this.items [ i1 ];
                if (i.Value?.Equals ( item ) == true) {
                    i.ExpiresAt = DateTime.Now + expiresAt;
                    return;
                }
            }

            // if the item was not found, add it
            if (this.AddItemCallback is not null)
                this.AddItemCallback ( this, item );

            this.items.Add ( new CacheItem<TValue> ( item, DateTime.Now.Add ( expiresAt ) ) );
        }
    }

    /// <summary>
    /// Searches and renews an existing and cached <typeparamref name="TValue"/> using the default expiration time. If
    /// the value is already expired, an newentry is added in the cache store.
    /// </summary>
    /// <param name="item">The item to add or renew.</param>
    public void AddOrRenew ( TValue item ) => this.AddOrRenew ( item, this.DefaultExpiration );

    /// <inheritdoc/>
    public void Clear () {
        lock (this.items) {
            if (this.RemoveItemCallback is not null) {
                for (int i1 = 0; i1 < this.items.Count; i1++) {
                    CacheItem<TValue>? i = this.items [ i1 ];
                    this.RemoveItemCallback ( this, i.Value );
                }
            }

            this.items.Clear ();
        }
    }

    /// <summary>
    /// Gets an boolean indicating this list contains any non-expired entity that is equals to the
    /// specified object.
    /// </summary>
    /// <param name="item">The object to compare to.</param>
    /// <returns></returns>
    public bool Contains ( TValue item ) {
        lock (this.items) {
            foreach (var i in this.items)
                if (!i.IsExpired () && i.Value?.Equals ( item ) == true)
                    return true;
        }
        return false;
    }

    /// <summary>
    /// Copies all the non-expired elements to another array.
    /// </summary>
    public void CopyTo ( TValue [] array, int arrayIndex ) {
        lock (this.items) {
            this.ToArray ().CopyTo ( array, arrayIndex );
        }
    }

    /// <inheritdoc/>
    public IEnumerator<TValue> GetEnumerator () {
        lock (this.items) {
            for (int i = 0; i < this.items.Count; i++) {
                CacheItem<TValue> item = this.items [ i ];
                if (!item.IsExpired ()) {
                    yield return item.Value;
                }
            }
        }
    }

    /// <inheritdoc/>
    public int IndexOf ( TValue item ) {
        lock (this.items) {
            return this.ToList ().IndexOf ( item );
        }
    }

    /// <inheritdoc/>
    public void Insert ( int index, TValue item ) {
        lock (this.items) {
            CacheItem<TValue> c = new CacheItem<TValue> ( item, DateTime.Now.Add ( this.DefaultExpiration ) );

            if (this.AddItemCallback is not null)
                this.AddItemCallback ( this, item );

            this.items.Insert ( index, c );
        }
    }

    /// <inheritdoc/>
    public void Insert ( int index, TValue item, TimeSpan expiresAt ) {
        lock (this.items) {
            CacheItem<TValue> c = new CacheItem<TValue> ( item, DateTime.Now.Add ( expiresAt ) );

            if (this.AddItemCallback is not null)
                this.AddItemCallback ( this, item );

            this.items.Insert ( index, c );
        }
    }

    /// <inheritdoc/>
    public bool Remove ( TValue item ) {
        lock (this.items) {
            for (int i = 0; i < this.items.Count; i++) {
                var value = this.items [ i ].Value;
                if (value?.Equals ( item ) == true) {
                    if (this.RemoveItemCallback is not null)
                        this.RemoveItemCallback ( this, value );

                    this.items.RemoveAt ( i );
                    break;
                }
            }
        }
        return false;
    }

    /// <inheritdoc/>
    public void RemoveAt ( int index ) {
        lock (this.items) {
            var value = this.items [ index ].Value;

            if (this.RemoveItemCallback is not null)
                this.RemoveItemCallback ( this, value );

            this.items.RemoveAt ( index );
        }
    }

    IEnumerator IEnumerable.GetEnumerator () {
        return this.GetEnumerator ();
    }

    /// <inheritdoc/>
    public int RemoveExpiredEntities () {
        lock (this.items) {
            List<int> toRemove = new List<int> ( this.items.Count );
            for (int i = 0; i < this.items.Count; i++) {
                if (this.items [ i ].IsExpired ()) {
                    toRemove.Add ( i );
                }
            }

            toRemove.Reverse ();
            foreach (int key in toRemove) {
                var value = this.items [ key ].Value;

                if (this.RemoveItemCallback is not null)
                    this.RemoveItemCallback ( this, value );

                this.items.RemoveAt ( key );
            }

            return toRemove.Count;
        }
    }
}

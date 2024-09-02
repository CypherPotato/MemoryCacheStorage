using System.Diagnostics.CodeAnalysis;

namespace CacheStorage;

/// <summary>
/// Represents an structure which caches an specific object.
/// </summary>
/// <typeparam name="TValue">The type of the object to cache.</typeparam>
public struct CachedObject<TValue> : ITimeToLiveCache, IEquatable<TValue>, IEquatable<CachedObject<TValue>>
{
    private CacheItem<TValue> _item;

    /// <summary>
    /// Specifies the default amount of time to cache the object.
    /// </summary>
    public TimeSpan Expiration { get; set; }

    /// <summary>
    /// Gets or sets the cached object. If the object expiration time is expired,
    /// the default value is returned.
    /// </summary>
    public TValue? Value
    {
        get
        {
            if (this._item.IsExpired())
            {
                this.Clear();
                return default;
            }
            else
            {
                return this._item.Value;
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.Clear();
            this._item = new CacheItem<TValue>()
            {
                ExpiresAt = DateTime.Now.Add(this.Expiration),
                Value = value
            };
        }
    }

    /// <summary>
    /// Gets an boolean indicating if this <see cref="CachedObject{TValue}"/> has an valid, non-expired
    /// value.
    /// </summary>
    public bool HasValue
    {
        get
        {
            if (this._item.IsExpired())
            {
                this.Clear();
                return false;
            }
            else return true;
        }
    }

    /// <summary>
    /// Gets the cached object if not expired or sets it's value from the specified function.
    /// </summary>
    /// <param name="obtainFunc">The function that returns <typeparamref name="TValue"/>.</param>
    public TValue GetOrSet(Func<TValue> obtainFunc)
    {
        if (this.HasValue)
        {
            return this._item.Value;
        }
        else
        {
            TValue tval = obtainFunc();
            this.Value = tval;
            return tval;
        }
    }

    /// <summary>
    /// Asynchronously gets the cached object if not expired or sets it's value from the specified function.
    /// </summary>
    /// <param name="obtainFunc">The async function that returns <typeparamref name="TValue"/>.</param>
    public async Task<TValue> GetOrSetAsync(Func<Task<TValue>> obtainFunc)
    {
        if (this.HasValue)
        {
            return this._item.Value;
        }
        else
        {
            TValue tval = await obtainFunc();
            this.Value = tval;
            return tval;
        }
    }

    /// <summary>
    /// Removes the linked object from this cache object.
    /// </summary>
    public void Clear()
    {
        (this._item.Value as IDisposable)?.Dispose();
        this._item = default;
    }

    /// <summary>
    /// Removes the linked object from this cache object if it is expired.
    /// </summary>
    public int RemoveExpiredEntities()
    {
        return this.HasValue ? 0 : 1;
    }

    /// <inheritdoc/>
    public bool Equals(TValue? other)
    {
        return this.Value?.Equals(other) == true;
    }

    /// <inheritdoc/>
    public bool Equals(CachedObject<TValue> other)
    {
        return this.Value?.Equals(other.Value) == true;
    }

    /// <summary>
    /// Creates an new empty <see cref="CachedObject{TValue}"/> instance.
    /// </summary>
    public CachedObject()
    {
        this._item = default;
        this.Expiration = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Creates an new <see cref="CachedObject{TValue}"/> instance with the specified
    /// initial value.
    /// </summary>
    public CachedObject(TValue value)
    {
        this.Expiration = TimeSpan.FromMinutes(10);
        this._item = new CacheItem<TValue>() { Value = value, ExpiresAt = DateTime.Now + this.Expiration };
    }

    /// <summary>
    /// Creates an new <see cref="CachedObject{TValue}"/> instance with the specified
    /// initial value and expiration time.
    /// </summary>
    public CachedObject(TValue value, TimeSpan expiresIn)
    {
        this.Expiration = expiresIn;
        this._item = new CacheItem<TValue>() { Value = value, ExpiresAt = DateTime.Now + this.Expiration };
    }

    /// <inheritdoc/>
    public static implicit operator TValue?(CachedObject<TValue> cached)
    {
        return cached.Value;
    }

    /// <inheritdoc/>
    public static implicit operator CachedObject<TValue>(TValue obj)
    {
        return new CachedObject<TValue>(obj);
    }

    /// <inheritdoc/>
    public static bool operator ==(CachedObject<TValue> a, TValue? b)
    {
        return a.Equals(b);
    }

    /// <inheritdoc/>
    public static bool operator !=(CachedObject<TValue> a, TValue? b)
    {
        return !a.Equals(b);
    }

    /// <inheritdoc/>
    public static bool operator ==(CachedObject<TValue> a, CachedObject<TValue> b)
    {
        return a.Equals(b);
    }

    /// <inheritdoc/>
    public static bool operator !=(CachedObject<TValue> a, CachedObject<TValue> b)
    {
        return !a.Equals(b);
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is CachedObject<TValue> cached)
        {
            return this.Equals(cached);
        }
        else if (obj is TValue val)
        {
            return this.Equals(val);
        }
        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return this.Value?.GetHashCode() ?? 0;
    }

    /// <inheritdoc/>
    public override string? ToString()
    {
        return this.Value?.ToString();
    }
}

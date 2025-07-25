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
            if (_item.IsExpired())
            {
                Clear();
                return default;
            }
            else
            {
                return _item.Value;
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Clear();
            _item = new CacheItem<TValue>(value, DateTime.Now.Add(Expiration));
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
            if (_item.IsExpired())
            {
                Clear();
                return false;
            }
            else
                return true;
        }
    }

    /// <summary>
    /// Gets the cached object if not expired or sets it's value from the specified function.
    /// </summary>
    /// <param name="obtainFunc">The function that returns <typeparamref name="TValue"/>.</param>
    public TValue GetOrSet(Func<TValue> obtainFunc)
    {
        if (HasValue)
        {
            return _item.Value;
        }
        else
        {
            TValue tval = obtainFunc();
            Value = tval;
            return tval;
        }
    }

    /// <summary>
    /// Asynchronously gets the cached object if not expired or sets it's value from the specified function.
    /// </summary>
    /// <param name="obtainFunc">The async function that returns <typeparamref name="TValue"/>.</param>
    public async Task<TValue> GetOrSetAsync(Func<Task<TValue>> obtainFunc)
    {
        if (HasValue)
        {
            return _item.Value;
        }
        else
        {
            TValue tval = await obtainFunc();
            Value = tval;
            return tval;
        }
    }

    /// <summary>
    /// Renews the expiration time of this <typeparamref name="TValue"/>.
    /// </summary>
    public void Renew()
    {
        _item.ExpiresAt = DateTime.Now.Add(Expiration);
    }

    /// <summary>
    /// Renews the expiration time of this <typeparamref name="TValue"/> with the
    /// specified expiration time.
    /// </summary>
    /// <param name="expiration">The amount of time to give to the object before it expires.</param>
    public void Renew(TimeSpan expiration)
    {
        _item.ExpiresAt = DateTime.Now.Add(expiration);
    }

    /// <summary>
    /// Removes the linked object from this cache object.
    /// </summary>
    public void Clear()
    {
        (_item.Value as IDisposable)?.Dispose();
        _item = CacheItem<TValue>.Empty;
    }

    /// <summary>
    /// Removes the linked object from this cache object if it is expired.
    /// </summary>
    public int RemoveExpiredEntities()
    {
        return HasValue ? 0 : 1;
    }

    /// <inheritdoc/>
    public bool Equals(TValue? other)
    {
        return Value?.Equals(other) == true;
    }

    /// <inheritdoc/>
    public bool Equals(CachedObject<TValue> other)
    {
        return Value?.Equals(other.Value) == true;
    }

    /// <summary>
    /// Creates an new empty <see cref="CachedObject{TValue}"/> instance.
    /// </summary>
    public CachedObject()
    {
        _item = CacheItem<TValue>.Empty;
        Expiration = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Creates an new empty <see cref="CachedObject{TValue}"/> instance with the specified
    /// expiration time.
    /// </summary>
    public CachedObject(TimeSpan expiresIn)
    {
        _item = CacheItem<TValue>.Empty;
        Expiration = expiresIn;
    }

    /// <summary>
    /// Creates an new <see cref="CachedObject{TValue}"/> instance with the specified
    /// initial value.
    /// </summary>
    public CachedObject(TValue value)
    {
        Expiration = TimeSpan.FromMinutes(10);
        _item = new CacheItem<TValue>(value, DateTime.Now.Add(Expiration));
    }

    /// <summary>
    /// Creates an new <see cref="CachedObject{TValue}"/> instance with the specified
    /// initial value and expiration time.
    /// </summary>
    public CachedObject(TValue value, TimeSpan expiresIn)
    {
        Expiration = expiresIn;
        _item = new CacheItem<TValue>(value, DateTime.Now.Add(Expiration));
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
            return Equals(cached);
        }
        else if (obj is TValue val)
        {
            return Equals(val);
        }
        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Value?.GetHashCode() ?? 0;
    }

    /// <inheritdoc/>
    public override string? ToString()
    {
        return Value?.ToString();
    }
}

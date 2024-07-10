﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                _item = default;
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
            _item = new CacheItem<TValue>()
            {
                ExpiresAt = DateTime.Now.Add(Expiration),
                Value = value
            };
        }
    }

    /// <summary>
    /// Gets an boolean indicating if this <see cref="CachedObject{TValue}"/> has an valid, non-expired
    /// value.
    /// </summary>
    public bool HasValue { get => !_item.IsExpired(); }

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
    /// Removes the linked object from this cache object.
    /// </summary>
    public void Clear()
    {
        _item = default;
    }

    /// <summary>
    /// Removes the linked object from this cache object if it is expired.
    /// </summary>
    public int RemoveExpiredEntities()
    {
        if (_item.IsExpired())
        {
            _item = default;
            return 1;
        }
        return 0;
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
        _item = default;
        Expiration = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Creates an new <see cref="CachedObject{TValue}"/> instance with the specified
    /// initial value.
    /// </summary>
    public CachedObject(TValue value)
    {
        Expiration = TimeSpan.FromMinutes(10);
        _item = new CacheItem<TValue>() { Value = value, ExpiresAt = DateTime.Now + Expiration };
    }

    /// <summary>
    /// Creates an new <see cref="CachedObject{TValue}"/> instance with the specified
    /// initial value and expiration time.
    /// </summary>
    public CachedObject(TValue value, TimeSpan expiresIn)
    {
        Expiration = expiresIn;
        _item = new CacheItem<TValue>() { Value = value, ExpiresAt = DateTime.Now + Expiration };
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

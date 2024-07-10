using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheStorage;

/// <summary>
/// Represents the generic implementation for <see cref="ICacheStorage{TKey, TValue}"/>.
/// </summary>
public interface ICacheStorage
{
}

/// <summary>
/// Represents the interface of an cache storage.
/// </summary>
/// <typeparam name="TKey">Defines the cache key type.</typeparam>
/// <typeparam name="TValue">Defines the cache value type.</typeparam>
public interface ICacheStorage<TKey, TValue> : ICacheStorage where TKey : notnull
{
    /// <summary>
    /// Adds or sets a cached item to this cache storage.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="value">The object value.</param>
    /// <returns>An boolean indicating if the value was added or not.</returns>
    public bool Add(TKey key, TValue value);

    /// <summary>
    /// Tries to remove an cached object from the specified key.
    /// </summary>
    /// <param name="key">The object key to be removed.</param>
    /// <returns>An boolean indicating if the object was removed or not.</returns>
    public bool Remove(TKey key);

    /// <summary>
    /// Gets an boolean indicating if the specified key has an valid cached object associated with it.
    /// </summary>
    /// <param name="key">The object key.</param>
    public bool ContainsKey(TKey key);

    /// <summary>
    /// Gets an <typeparamref name="TValue"/> associated with the specified key.
    /// </summary>
    /// <param name="key">The object key.</param>
    /// <param name="value">When this method returns, outputs the found object associated with the specified key.</param>
    /// <returns>An boolean indicating if the object was found or not.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Removes all cached entities from this cache storage.
    /// </summary>
    public void Clear();

    /// <summary>
    /// Gets or sets an item from their key.
    /// </summary>
    /// <param name="key">The key to set or get the item.</param>
    /// <returns></returns>
    public TValue this[TKey key] { get; set; }

    /// <summary>
    /// Gets the number of cached items in this cache storage.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets an array of defined keys in this cache storage.
    /// </summary>
    public IEnumerable<TKey> Keys { get; }

    /// <summary>
    /// Gets an array of defined values in this cache storage.
    /// </summary>
    public IEnumerable<TValue> Values { get; }
}
using System.Runtime.CompilerServices;

namespace CacheStorage;

internal sealed class CacheItem<TValue>
{
    public static readonly CacheItem<TValue> Empty = new(default!, DateTime.MinValue);

    public DateTime ExpiresAt { get; }
    public TValue Value { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;

    public CacheItem(TValue value, DateTime expiresAt)
    {
        Value = value;
        ExpiresAt = expiresAt;
    }
}

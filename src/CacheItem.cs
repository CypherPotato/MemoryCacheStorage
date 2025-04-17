using System.Runtime.CompilerServices;

namespace CacheStorage;

internal class CacheItem<TValue> {
    public static CacheItem<TValue> Empty = new CacheItem<TValue> ( default!, DateTime.MinValue );

    public DateTime ExpiresAt { get; set; }
    public TValue Value { get; set; }

    [MethodImpl ( MethodImplOptions.AggressiveInlining )]
    public bool IsExpired () => DateTime.Now > ExpiresAt;

    public CacheItem ( TValue value, DateTime expiresAt ) {
        Value = value;
        ExpiresAt = expiresAt;
    }
}

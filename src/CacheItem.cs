using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CypherPotato;

internal struct CacheItem<TKey, TValue> where TKey : notnull
{
    public IComparer<TKey>? KeyComparer { get; set; }
    public TKey[] Keys { get; set; } = Array.Empty<TKey>();
    public TValue Value { get; set; }
    public DateTime ExpiresAt { get; set; }

    public static CacheItem<TKey, TValue> Default = new CacheItem<TKey, TValue>(Array.Empty<TKey>(), default!, default, null);

    public void Renew(DateTime expiresAt)
    {
        if (expiresAt < DateTime.Now)
        {
            throw new ArgumentException("The renewal time cannot be less than the current date and time.");
        }
        ExpiresAt = expiresAt;
    }

    public CacheItem(TKey[] identifiers, TValue value, DateTime expiresAt, IComparer<TKey>? keyComparer)
    {
        Keys = identifiers;
        Value = value;
        ExpiresAt = expiresAt;
        KeyComparer = keyComparer;
    }

    public bool IsDefined(TKey key)
    {
        var kspan = Keys.AsSpan();
        for (int i = 0; i < kspan.Length; i++)
        {
            if (KeyComparer == null ? kspan[i].Equals(key) : KeyComparer.Compare(key, kspan[i]) == 0)
            {
                return true;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExpired()
    {
        return DateTime.Now > ExpiresAt;
    }
}

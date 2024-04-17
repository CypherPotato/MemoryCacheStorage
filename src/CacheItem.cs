using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CypherPotato;

internal struct CacheItem<TKey, TValue> where TKey : notnull
{
    public TKey[] Keys { get; set; } = Array.Empty<TKey>();
    public TValue Value { get; set; }
    public DateTime ExpiresAt { get; set; }

    public static CacheItem<TKey, TValue> Default = new CacheItem<TKey, TValue>(Array.Empty<TKey>(), default(TValue)!, default(DateTime));

    public void Renew(DateTime expiresAt)
    {
        ExpiresAt = expiresAt;
    }

    public CacheItem(TKey[] identifiers, TValue value, DateTime expiresAt)
    {
        Keys = identifiers;
        Value = value;
        ExpiresAt = expiresAt;
    }
}

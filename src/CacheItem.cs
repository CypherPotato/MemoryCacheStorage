using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CacheStorage;

internal struct CacheItem<TValue>
{
    public DateTime ExpiresAt { get; set; }
    public TValue Value { get; set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExpired() => DateTime.Now > ExpiresAt;
}

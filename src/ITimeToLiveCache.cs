using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheStorage;

/// <summary>
/// Provides an interface for time-to-live based cache storages.
/// </summary>
public interface ITimeToLiveCache
{
    /// <summary>
    /// Removes all expired entities from this storage.
    /// </summary>
    /// <returns></returns>
    public int RemoveExpiredEntities();
}

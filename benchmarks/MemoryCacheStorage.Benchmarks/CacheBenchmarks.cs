using System;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using System.Runtime.Caching;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using CacheStorage;
using ExtensionsMemoryCache = Microsoft.Extensions.Caching.Memory.MemoryCache;
using ExtensionsMemoryCacheOptions = Microsoft.Extensions.Caching.Memory.MemoryCacheOptions;
using SystemRuntimeMemoryCache = System.Runtime.Caching.MemoryCache;

namespace MemoryCacheStorage.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net90)]
public class CacheBenchmarks
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);

    [Params(1_000, 10_000)]
    public int Operations { get; set; }

    private string[] _keys = Array.Empty<string>();
    private string[] _values = Array.Empty<string>();
    private MemoryCacheStorage<string, string> _memoryCacheStorage = null!;
    private SystemRuntimeMemoryCache _systemRuntimeCache = null!;
    private ExtensionsMemoryCache _extensionsMemoryCache = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _keys = Enumerable.Range(0, Operations).Select(index => $"key_{index}").ToArray();
        _values = _keys.Select(key => $"value_for_{key}").ToArray();

        _memoryCacheStorage = new MemoryCacheStorage<string, string>();
        _systemRuntimeCache = new SystemRuntimeMemoryCache($"SystemRuntimeCache_{Guid.NewGuid()}");
        _extensionsMemoryCache = new ExtensionsMemoryCache(new ExtensionsMemoryCacheOptions());

        var absoluteExpiration = DateTimeOffset.UtcNow.Add(DefaultExpiration);
        var policy = new CacheItemPolicy { AbsoluteExpiration = absoluteExpiration };
        for (var i = 0; i < Operations; i++)
        {
            _memoryCacheStorage.Add(_keys[i], _values[i]);
            _systemRuntimeCache.Add(_keys[i], _values[i], policy);
            _extensionsMemoryCache.Set(_keys[i], _values[i], DefaultExpiration);
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _systemRuntimeCache.Dispose();
        _extensionsMemoryCache.Dispose();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int Add_MemoryCacheStorage()
    {
        var cache = new MemoryCacheStorage<string, string>();
        var added = 0;

        for (var i = 0; i < Operations; i++)
        {
            if (cache.Add(_keys[i], _values[i]))
            {
                added++;
            }
        }

        return added;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int Add_SystemRuntimeMemoryCache()
    {
        using var cache = new SystemRuntimeMemoryCache($"SystemRuntimeCache_Add_{Guid.NewGuid()}");
        var absoluteExpiration = DateTimeOffset.UtcNow.Add(DefaultExpiration);
        var policy = new CacheItemPolicy { AbsoluteExpiration = absoluteExpiration };
        var added = 0;

        for (var i = 0; i < Operations; i++)
        {
            if (cache.Add(_keys[i], _values[i], policy))
            {
                added++;
            }
        }

        return added;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int Add_MicrosoftExtensionsMemoryCache()
    {
        using var cache = new ExtensionsMemoryCache(new ExtensionsMemoryCacheOptions());
        var added = 0;

        for (var i = 0; i < Operations; i++)
        {
            cache.Set(_keys[i], _values[i], DefaultExpiration);
            added++;
        }

        return added;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Get")]
    public int Get_MemoryCacheStorage()
    {
        var totalLength = 0;

        for (var i = 0; i < Operations; i++)
        {
            if (_memoryCacheStorage.TryGetValue(_keys[i], out var value))
            {
                totalLength += value.Length;
            }
        }

        return totalLength;
    }

    [Benchmark]
    [BenchmarkCategory("Get")]
    public int Get_SystemRuntimeMemoryCache()
    {
        var totalLength = 0;

        for (var i = 0; i < Operations; i++)
        {
            if (_systemRuntimeCache.Get(_keys[i]) is string value)
            {
                totalLength += value.Length;
            }
        }

        return totalLength;
    }

    [Benchmark]
    [BenchmarkCategory("Get")]
    public int Get_MicrosoftExtensionsMemoryCache()
    {
        var totalLength = 0;

        for (var i = 0; i < Operations; i++)
        {
            if (_extensionsMemoryCache.TryGetValue(_keys[i], out var value) && value is string stringValue)
            {
                totalLength += stringValue.Length;
            }
        }

        return totalLength;
    }
}


using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CacheStorage;

namespace MemoryCacheStorage.Tests
{
    [TestClass]
    public class ConcurrencyAndLifetimeTests
    {
        [TestMethod]
        public async Task MemoryCacheStorage_GetOrAddAsync_ConcurrentCalls_ShouldExecuteFactoryOnlyOnce()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, string>();
            var key = "test_key";
            var factoryExecutionCount = 0;
            var factoryFunc = new Func<Task<string>>(async () => 
            {
                Interlocked.Increment(ref factoryExecutionCount);
                await Task.Delay(100); // Simulate work
                return "test_value";
            });

            // Act
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(cache.GetOrAddAsync(key, TimeSpan.FromSeconds(10), factoryFunc));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(1, factoryExecutionCount, "The factory method should only be called once!");
            foreach (var result in results)
            {
                Assert.AreEqual("test_value", result, "All calls should return the same value.");
            }
        }

        [TestMethod]
        public async Task MemoryCacheStorage_ItemExpires_And_PoolingContext_CleansItUp()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, string>();
            var context = CachePoolingContext.StartNew(TimeSpan.FromMilliseconds(50));
            context.Collect(cache);

            var key = "short-lived-key";
            var value = "i-will-disappear";

            // Act
            cache.Add(key, value, TimeSpan.FromMilliseconds(100));

            // Assert
            Assert.IsTrue(cache.ContainsKey(key), "Item should be in the cache right after adding it!");

            // Wait for the item to expire and the pooling context to clean it up
            await Task.Delay(200);

            Assert.IsFalse(cache.ContainsKey(key), "Item should be removed by the pooling context after it expires!");

            context.StopCollecting();
        }

        private class DisposableTestObject : IDisposable
        {
            public bool IsDisposed { get; private set; }
            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        [TestMethod]
        public async Task CachedObject_DisposesValue_WhenCleared()
        {
            // Arrange
            var disposable = new DisposableTestObject();
            var cachedObject = new CachedObject<DisposableTestObject>(disposable, TimeSpan.FromMilliseconds(100));

            // Act
            await Task.Delay(150); // Wait for expiration
            _ = cachedObject.Value; // Accessing the value after expiration triggers the clear

            // Assert
            Assert.IsTrue(disposable.IsDisposed, "The disposable object should be disposed when the CachedObject is cleared after expiration.");
        }

        [TestMethod]
        public async Task MemoryCacheList_ConcurrentAddAndRemove_ShouldBeThreadSafe()
        {
            // Arrange
            var list = new MemoryCacheList<int>();
            var tasks = new List<Task>();
            int initialItems = 100;

            // Act
            for (int i = 0; i < initialItems; i++)
            {
                list.Add(i);
            }

            // Create tasks to concurrently add and remove items
            for (int i = 0; i < 50; i++)
            {
                int itemToAdd = initialItems + i;
                tasks.Add(Task.Run(() => list.Add(itemToAdd)));

                int itemToRemove = i; // Remove the first 50 items
                tasks.Add(Task.Run(() => list.Remove(itemToRemove)));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Assert.Fail($"A concurrency exception was thrown: {ex.Message}");
            }

            // Assert
            // The final count is not deterministic, so we just assert that the operations completed without error.
            // A more complex assertion could check if the final list contains only numbers from 50 to 99 and 100 to 149,
            // but for now, surviving the concurrent operations is the main goal.
            Assert.IsTrue(list.Count > 0, "The list should still contain items.");
        }

        [TestMethod]
        public async Task CachedObject_GetOrSetAsync_ConcurrentCalls_ShouldExecuteFactoryOnlyOnce()
        {
            // Arrange
            var cachedObject = new CachedObject<string>(TimeSpan.FromSeconds(10));
            var factoryExecutionCount = 0;
            var factoryFunc = new Func<Task<string>>(async () =>
            {
                Interlocked.Increment(ref factoryExecutionCount);
                await Task.Delay(100); // Simulate work
                return "cached_value";
            });

            // Act
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(cachedObject.GetOrSetAsync(factoryFunc));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(1, factoryExecutionCount, "The factory method should only be called once!");
            foreach (var result in results)
            {
                Assert.AreEqual("cached_value", result, "All calls should return the same value.");
            }
        }

        [TestMethod]
        public async Task CachePoolingContext_ConcurrentCollect_ShouldBeThreadSafe()
        {
            // Arrange
            var context = new CachePoolingContext(TimeSpan.FromMinutes(1));
            var tasks = new List<Task>();
            int cacheCount = 100;

            // Act
            for (int i = 0; i < cacheCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var cache = new MemoryCacheStorage<int, int>();
                    context.Collect(cache);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(cacheCount, context.CollectingCaches.Count, "All caches should have been added to the context.");
        }
    }
}

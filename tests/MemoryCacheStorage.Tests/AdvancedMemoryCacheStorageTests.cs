
using CacheStorage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MemoryCacheStorage.Tests
{
    [TestClass]
    public class AdvancedMemoryCacheStorageTests
    {
        [TestMethod]
        public async Task GetOrAddAsync_FactoryIsOnlyCalledOnce_OnConcurrentAccess()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, int>();
            var key = "concurrentKey";
            int factoryExecutionCount = 0;

            Func<Task<int>> factory = async () =>
            {
                Interlocked.Increment(ref factoryExecutionCount);
                await Task.Delay(100); // Simulate async work
                return 42;
            };

            // Act
            var tasks = new Task<int>[100];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = cache.GetOrAddAsync(key, factory);
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(1, factoryExecutionCount, "The factory method should only be executed once.");
            foreach (var task in tasks)
            {
                Assert.AreEqual(42, task.Result, "All concurrent callers should receive the value from the single factory execution.");
            }
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public void GetOrAdd_AfterExpiration_ShouldReExecuteFactory()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, DateTime>();
            var key = "expiringKey";
            int factoryExecutionCount = 0;
            Func<DateTime> factory = () =>
            {
                Interlocked.Increment(ref factoryExecutionCount);
                return DateTime.UtcNow;
            };

            // Act
            var value1 = cache.GetOrAdd(key, TimeSpan.FromMilliseconds(50), factory);
            Assert.AreEqual(1, factoryExecutionCount);

            Thread.Sleep(100); // Wait for expiration

            var value2 = cache.GetOrAdd(key, TimeSpan.FromMilliseconds(50), factory);

            // Assert
            Assert.AreEqual(2, factoryExecutionCount, "Factory should be executed again after the first item expired.");
            Assert.AreNotEqual(value1, value2, "A new value should be generated after expiration.");
        }

        [TestMethod]
        public void Deadlock_ShouldNotOccur_WithCrossLockingCallbacks()
        {
            // This test is designed to fail if the callbacks are invoked inside a lock,
            // demonstrating a classic deadlock scenario.
            // Arrange
            var cache1 = new MemoryCacheStorage<int, int>();
            var cache2 = new MemoryCacheStorage<int, int>();

            // Setup callbacks that lock on the other cache
            cache1.AddItemCallback += (sender, val) =>
            {
                lock (cache2)
                {
                    Thread.Sleep(100);
                }
            };
            cache2.AddItemCallback += (sender, val) =>
            {
                lock (cache1)
                {
                    Thread.Sleep(100);
                }
            };

            // Act
            var task1 = Task.Run(() =>
            {
                cache1.Add(1, 1);
            });

            var task2 = Task.Run(() =>
            {
                cache2.Add(1, 1);
            });

            bool completed = Task.WaitAll(new[] { task1, task2 }, TimeSpan.FromSeconds(2));

            // Assert
            Assert.IsTrue(completed, "Tasks should complete without a deadlock.");
        }
    }
}


using Microsoft.VisualStudio.TestTools.UnitTesting;
using CacheStorage;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MemoryCacheStorage.Tests
{
    [TestClass]
    public class MemoryCacheStorageTests
    {
        [TestMethod]
        public void GetOrAddAsync_Should_Prevent_Deadlock()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, int>();
            var key = "testKey";

            Func<Task<int>> getValueFunc = async () =>
            {
                await Task.Delay(100).ConfigureAwait(false);
                return 1;
            };

            // Act
            var task1 = cache.GetOrAddAsync(key, getValueFunc);
            var task2 = cache.GetOrAddAsync(key, getValueFunc);

            Task.WaitAll(task1, task2);

            // Assert
            Assert.AreEqual(1, task1.Result);
            Assert.AreEqual(1, task2.Result);
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public void Callback_Should_Not_Cause_Deadlock()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, int>();
            var lockObject = new object();

            cache.AddItemCallback += (sender, value) =>
            {
                lock (lockObject)
                {
                    // Simulate work inside lock
                    Task.Delay(10).Wait();
                }
            };

            // Act
            var task = Task.Run(() =>
            {
                lock (lockObject)
                {
                    cache.Add("key", 1);
                }
            });

            // Assert
            bool completed = task.Wait(TimeSpan.FromSeconds(1));
            Assert.IsTrue(completed, "The task should complete without a deadlock.");
        }

        [TestMethod]
        public void Add_And_Get_Item()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, string>();
            var key = "key";
            var value = "value";

            // Act
            cache.Add(key, value);
            var result = cache[key];

            // Assert
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void GetOrAdd_ReturnsExistingItem()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, int>();
            cache.Add("key", 1);

            // Act
            var result = cache.GetOrAdd("key", () => 2);

            // Assert
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void GetOrAdd_AddsNewItem()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, int>();

            // Act
            var result = cache.GetOrAdd("key", () => 2);

            // Assert
            Assert.AreEqual(2, result);
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public void Remove_RemovesItem()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, int>();
            cache.Add("key", 1);

            // Act
            cache.Remove("key");

            // Assert
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public void RemoveIfExpired_RemovesExpiredItem()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, int>();
            cache.Add("key", 1, TimeSpan.FromMilliseconds(10));

            // Act
            Task.Delay(20).Wait();
            cache.RemoveIfExpired("key");

            // Assert
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public void AddOrRenew_AddsNewItem()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, int>();

            // Act
            cache.AddOrRenew("key", 1);

            // Assert
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public void AddOrRenew_RenewsExistingItem()
        {
            // Arrange
            var cache = new MemoryCacheStorage<string, int>();
            cache.Add("key", 1, TimeSpan.FromMilliseconds(50));

            // Act
            Task.Delay(30).Wait();
            cache.AddOrRenew("key", 1);
            Task.Delay(30).Wait();

            // Assert
            Assert.IsTrue(cache.ContainsKey("key"));
        }
    }
}

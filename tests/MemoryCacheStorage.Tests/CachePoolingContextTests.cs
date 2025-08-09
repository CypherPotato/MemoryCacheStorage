
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CacheStorage;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace MemoryCacheStorage.Tests
{
    [TestClass]
    public class CachePoolingContextTests
    {
        [TestMethod]
        public void Shared_Instance_Is_Singleton_And_ThreadSafe()
        {
            // Arrange
            CachePoolingContext instance1 = null;
            CachePoolingContext instance2 = null;

            // Act
            var task1 = Task.Run(() => instance1 = CachePoolingContext.Shared);
            var task2 = Task.Run(() => instance2 = CachePoolingContext.Shared);

            Task.WaitAll(task1, task2);

            // Assert
            Assert.IsNotNull(instance1);
            Assert.IsNotNull(instance2);
            Assert.AreSame(instance1, instance2, "CachePoolingContext.Shared should return the same instance.");
        }

        [TestMethod]
        public void CollectInterval_CanBeSetAndGet()
        {
            // Arrange
            var interval = TimeSpan.FromSeconds(30);
            var context = new CachePoolingContext(TimeSpan.FromMinutes(1));

            // Act
            context.CollectInterval = interval;

            // Assert
            Assert.AreEqual(interval, context.CollectInterval);
        }

        [TestMethod]
        public void IsCollecting_CanBeSetAndGet()
        {
            // Arrange
            var context = new CachePoolingContext(TimeSpan.FromMinutes(1));

            // Act
            context.IsCollecting = true;

            // Assert
            Assert.IsTrue(context.IsCollecting);

            // Act
            context.IsCollecting = false;

            // Assert
            Assert.IsFalse(context.IsCollecting);
        }

        [TestMethod]
        public void StartNew_CreatesAndStartsContext()
        {
            // Arrange
            var cache = new MemoryCacheList<string>();
            
            // Act
            var context = CachePoolingContext.StartNew(TimeSpan.FromMilliseconds(100), cache);

            // Assert
            Assert.IsTrue(context.IsCollecting);
            Assert.IsTrue(context.CollectingCaches.Contains(cache));
        }

        [TestMethod]
        public void Collect_AddsCacheToCollection()
        {
            // Arrange
            var context = new CachePoolingContext(TimeSpan.FromMinutes(1));
            var cache = new MemoryCacheList<string>();

            // Act
            context.Collect(cache);

            // Assert
            Assert.IsTrue(context.CollectingCaches.Contains(cache));
        }

        [TestMethod]
        public void Collect_DoesNotAddDuplicateCache()
        {
            // Arrange
            var context = new CachePoolingContext(TimeSpan.FromMinutes(1));
            var cache = new MemoryCacheList<string>();

            // Act
            context.Collect(cache);
            context.Collect(cache);

            // Assert
            Assert.AreEqual(1, context.CollectingCaches.Count);
        }

        [TestMethod]
        public void CollectAll_RemovesExpiredItemsFromCaches()
        {
            // Arrange
            var context = new CachePoolingContext(TimeSpan.FromMinutes(1));
            var cache1 = new MemoryCacheList<string>();
            var cache2 = new MemoryCacheStorage<int, string>();

            context.Collect(cache1);
            context.Collect(cache2);

            cache1.Add("a", TimeSpan.FromMilliseconds(1));
            cache1.Add("b");
            cache2.Add(1, "a", TimeSpan.FromMilliseconds(1));
            cache2.Add(2, "b");

            Thread.Sleep(10); // Wait for items to expire

            // Act
            int removedCount = context.CollectAll();

            // Assert
            Assert.AreEqual(2, removedCount, "Should remove one item from each cache.");
            Assert.AreEqual(1, cache1.Count);
            Assert.AreEqual(1, cache2.Count);
        }
    }
}

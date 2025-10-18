
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CacheStorage;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MemoryCacheStorage.Tests
{
    [TestClass]
    public class MemoryCacheListTests
    {
        [TestMethod]
        public void Concurrent_Add_And_Count_Should_Be_ThreadSafe()
        {
            // Arrange
            var list = new MemoryCacheList<int>();
            list.DefaultExpiration = TimeSpan.FromSeconds(10);

            // Act
            Parallel.For(0, 1000, (i) =>
            {
                list.Add(i);
            });

            // Assert
            Assert.AreEqual(1000, list.Count, "The list count should be accurate after concurrent adds.");
        }

        [TestMethod]
        public void IndexOf_ShouldBehaveCorrectly_WithExpiredItems()
        {
            // Arrange
            var list = new MemoryCacheList<int>();
            list.Add(1, TimeSpan.FromMilliseconds(10));
            list.Add(2);

            // Act
            Task.Delay(20).Wait(); // Wait for the first item to expire

            // Assert
            Assert.AreEqual(-1, list.IndexOf(1), "IndexOf should return -1 for expired items.");
            Assert.AreEqual(0, list.IndexOf(2), "IndexOf should return the correct index for non-expired items.");
        }

        [TestMethod]
        public void AddOrRenew_AddsNewItem()
        {
            // Arrange
            var list = new MemoryCacheList<string>();

            // Act
            list.AddOrRenew("test");

            // Assert
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(list.Contains("test"));
        }

        [TestMethod]
        public void AddOrRenew_RenewsExistingItem()
        {
            // Arrange
            var list = new MemoryCacheList<string>();
            list.Add("test", TimeSpan.FromMilliseconds(50));

            // Act
            Task.Delay(30).Wait();
            list.AddOrRenew("test");
            Task.Delay(30).Wait();

            // Assert
            Assert.IsTrue(list.Contains("test"));
        }

        [TestMethod]
        public void Remove_RemovesItem()
        {
            // Arrange
            var list = new MemoryCacheList<string>();
            list.Add("test");

            // Act
            list.Remove("test");

            // Assert
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void RemoveAt_RemovesItemAtIndex()
        {
            // Arrange
            var list = new MemoryCacheList<string>();
            list.Add("test");

            // Act
            list.RemoveAt(0);

            // Assert
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void Clear_RemovesAllItems()
        {
            // Arrange
            var list = new MemoryCacheList<string>();
            list.Add("a");
            list.Add("b");

            // Act
            list.Clear();

            // Assert
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void Insert_InsertsItemAtIndex()
        {
            // Arrange
            var list = new MemoryCacheList<string>();
            list.Add("a");
            list.Add("c");

            // Act
            list.Insert(1, "b");

            // Assert
            Assert.AreEqual("b", list[1]);
        }

        [TestMethod]
        public void CopyTo_SkipsExpiredItems()
        {
            // Arrange
            var list = new MemoryCacheList<string>();
            list.Add("first");
            list.Add("transient", TimeSpan.FromMilliseconds(20));
            list.Add("second");

            Task.Delay(40).Wait();

            // Act
            var target = new string[2];
            list.CopyTo(target, 0);

            var secondTarget = new string[2];
            ((System.Collections.ICollection)list).CopyTo(secondTarget, 0);

            // Assert
            CollectionAssert.AreEquivalent(new[] { "first", "second" }, target);
            CollectionAssert.AreEquivalent(new[] { "first", "second" }, secondTarget);
        }
    }
}

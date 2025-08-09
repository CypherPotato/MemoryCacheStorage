
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CacheStorage;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace MemoryCacheStorage.Tests
{
    [TestClass]
    public class AdvancedMemoryCacheListTests
    {
        [TestMethod]
        public void List_Enumeration_While_Items_Expire_Should_Not_Throw_Exception()
        {
            // Arrange
            var list = new MemoryCacheList<int>();
            list.Add(1, TimeSpan.FromMilliseconds(100));
            list.Add(2, TimeSpan.FromMilliseconds(100));
            list.Add(3, TimeSpan.FromMilliseconds(500));
            list.Add(4, TimeSpan.FromMilliseconds(500));

            Exception thrownException = null;

            // Act
            try
            {
                var task = Task.Run(() =>
                {
                    Thread.Sleep(200); // Wait for some items to expire
                    list.RemoveExpiredEntities();
                });

                foreach (var item in list)
                {
                    Thread.Sleep(60); // Make enumeration slow
                }

                task.Wait();
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }

            // Assert
            Assert.IsNull(thrownException, "Enumerating a list while items are removed by the collector should not throw an exception.");
        }

        [TestMethod]
        public void Indexer_Set_With_Callbacks_And_Expiration()
        {
            // Arrange
            var list = new MemoryCacheList<string>();
            list.DefaultExpiration = TimeSpan.FromMilliseconds(100);
            string addedItem = null;
            string removedItem = null;

            list.AddItemCallback += (sender, item) => addedItem = item;
            list.RemoveItemCallback += (sender, item) => removedItem = item;

            list.Add("first");

            // Act
            list[0] = "second";
            Thread.Sleep(150);
            var countAfterExpiration = list.Count;

            // Assert
            Assert.AreEqual("second", addedItem, "AddItemCallback should have been called with the new item.");
            Assert.AreEqual("first", removedItem, "RemoveItemCallback should have been called for the replaced item.");
            Assert.AreEqual(0, countAfterExpiration, "Item should have expired and been removed.");
        }
    }
}

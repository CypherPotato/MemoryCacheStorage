using CacheStorage;

namespace MemoryCacheStorage.Tests
{
    [TestClass]
    public class CachedObjectTests
    {
        [TestMethod]
        public void GetOrSet_RaceCondition_ShouldNotOccur()
        {
            // Arrange
            var cachedObject = new CachedObject<int>(TimeSpan.FromMilliseconds(100));
            int executionCount = 0;

            Func<int> obtainFunc = () =>
            {
                System.Threading.Interlocked.Increment(ref executionCount);
                return 1;
            };

            // Act
            Parallel.For(0, 100, (i) =>
            {
                cachedObject.GetOrSet(obtainFunc);
            });

            // Assert
            Assert.AreEqual(1, executionCount, "The obtainFunc should only be called once.");
        }

        [TestMethod]
        public void Value_ReturnsDefault_WhenExpired()
        {
            // Arrange
            var cachedObject = new CachedObject<string>("test", TimeSpan.FromMilliseconds(10));

            // Act
            Task.Delay(20).Wait();

            // Assert
            Assert.IsNull(cachedObject.Value);
        }

        [TestMethod]
        public void HasValue_IsFalse_WhenExpired()
        {
            // Arrange
            var cachedObject = new CachedObject<string>("test", TimeSpan.FromMilliseconds(10));

            // Act
            Task.Delay(20).Wait();

            // Assert
            Assert.IsFalse(cachedObject.HasValue);
        }

        [TestMethod]
        public void HasValue_IsFalse_WhenExpired_PreciseAlt()
        {
            // Arrange
            var cachedObject = new CachedObject<string>("test", TimeSpan.FromMilliseconds(10));

            // Act
            Thread.Sleep(11);

            // Assert
            Assert.IsFalse(cachedObject.HasValue);
        }

        [TestMethod]
        public async Task GetOrSetAsync_WorksCorrectly()
        {
            // Arrange
            var cachedObject = new CachedObject<int>(TimeSpan.FromSeconds(1));
            int executionCount = 0;

            Func<Task<int>> obtainFunc = async () =>
            {
                await Task.Delay(10);
                System.Threading.Interlocked.Increment(ref executionCount);
                return 1;
            };

            // Act
            var value1 = await cachedObject.GetOrSetAsync(obtainFunc);
            var value2 = await cachedObject.GetOrSetAsync(obtainFunc);

            // Assert
            Assert.AreEqual(1, value1);
            Assert.AreEqual(1, value2);
            Assert.AreEqual(1, executionCount);
        }

        [TestMethod]
        public void Renew_ExtendsExpiration()
        {
            // Arrange
            var cachedObject = new CachedObject<string>("test", TimeSpan.FromMilliseconds(200));

            // Act
            Task.Delay(80).Wait();
            cachedObject.Renew();
            Task.Delay(120).Wait();

            // Assert
            Assert.IsTrue(cachedObject.HasValue);
        }

        [TestMethod]
        public void Clear_RemovesValue()
        {
            // Arrange
            var cachedObject = new CachedObject<string>("test");

            // Act
            cachedObject.Clear();

            // Assert
            Assert.IsFalse(cachedObject.HasValue);
        }
    }
}

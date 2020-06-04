using ILGPU.Runtime;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class MemoryCacheOperations : TestBase
    {
        protected MemoryCacheOperations(
            ITestOutputHelper output,
            TestContext testContext)
            : base(output, testContext)
        { }

        public static TheoryData<object> AllocationTestData =>
            SizeOfValues.SizeOfTestData;

        [Theory]
        [MemberData(nameof(AllocationTestData))]
        [SuppressMessage(
            "Usage",
            "xUnit1026:Theory methods should use all of their parameters",
            Justification = "Required for generic argument")]
        public void Allocate<T>(T _)
            where T : unmanaged
        {
            using var buffer = new MemoryBufferCache(Accelerator);

            // Allocate several elements one after another
            for (int i = 1; i <= 1024; i <<= 1)
            {
                var view = buffer.Allocate<T>(i);

                Assert.Equal(view.Length, i);
                Assert.Equal(view.LengthInBytes, Interop.SizeOf<T>() * i);

                Assert.True(buffer.CacheSizeInBytes >= Interop.SizeOf<T>() * i);
                Assert.True(buffer.GetCacheSize<T>() == i);
            }

            // "Allocate" the same elements in reverse order
            for (int i = 1024; i >= 1; i >>= 1)
            {
                var view = buffer.Allocate<T>(i);

                Assert.Equal(view.Length, i);
                Assert.Equal(view.LengthInBytes, Interop.SizeOf<T>() * i);

                Assert.True(buffer.CacheSizeInBytes >= Interop.SizeOf<T>() * i);
                Assert.True(buffer.GetCacheSize<T>() >= i);
            }
        }
    }
}

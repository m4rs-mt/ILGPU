using ILGPU.Backends.SPIRV;
using Xunit;

namespace ILGPU.Tests.SPIRV
{
    public class SPIRVBuilderUtilsTests
    {
        [Fact]
        void JoinOpCodeWordCount()
        {
            var result = SPIRVBuilderUtils.JoinOpCodeWordCount(0, 1);
            Assert.Equal<uint>(65536, result);
        }
    }
}

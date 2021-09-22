using ILGPU.Backends.SPIRV;
using System.Collections.Generic;
using Xunit;

namespace ILGPU.Tests.SPIRV
{
    public class SPIRVBuilderUtilsTests
    {
        [Fact]
        public void ToUintListUint()
        {
            var result = SPIRVBuilderUtils.ToUintList((uint)0);
            Assert.Equal(new List<uint> {0}, result);
        }

        [Fact]
        public void ToUintListString()
        {
            var result = SPIRVBuilderUtils.ToUintList("GLSL");
            // This is what the SPIRV Compiler produces for this string
            Assert.Equal(new List<uint> {1280527431, 0}, result);
        }

        [Fact]
        public void ToUintListUintArray()
        {
            var result = SPIRVBuilderUtils.ToUintList(new uint[] {0, 1, 2, 3});
            Assert.Equal(new List<uint> {0, 1, 2, 3}, result);
        }

        struct SampleStruct
        {
            public uint a;
            public uint b;
        }

        [Fact]
        public void ToUintListStruct()
        {
            var result = SPIRVBuilderUtils.ToUintList(
                new SampleStruct() {a=1, b=2});
            Assert.Equal(new List<uint> {1, 2}, result);
        }

        enum SampleEnum
        {
            SampleValue = 10
        }

        [Fact]
        public void ToUintListEnum()
        {
            var result = SPIRVBuilderUtils.ToUintList(SampleEnum.SampleValue);
            Assert.Equal(new List<uint> {10}, result);
        }

        [Fact]
        void JoinOpCodeWordCount()
        {
            var result = SPIRVBuilderUtils.JoinOpCodeWordCount(0, 1);
            Assert.Equal<uint>(65536, result);
        }
    }
}

using ILGPU.Util;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class FormatStringTests : TestBase
    {
        protected FormatStringTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        [Fact]
        public void EscapedArgument()
        {
            Assert.True(FormatString.TryParse("{{{0}}}", out var expressions));
            Assert.Equal(3, expressions.Length);
            Assert.Equal("{", expressions[0].String);
            Assert.Equal(0, expressions[1].Argument);
            Assert.Equal("}", expressions[2].String);
        }

        [Fact]
        public void DanglingOpenBracket()
        {
            Assert.True(FormatString.TryParse("{0} {", out var expressions));
            Assert.Equal(3, expressions.Length);
            Assert.Equal(0, expressions[0].Argument);
            Assert.Equal(" ", expressions[1].String);
            Assert.Equal("{", expressions[2].String);
        }

        [Fact]
        public void DanglingCloseBracket()
        {
            Assert.False(FormatString.TryParse("{0} }", out _));
        }

        [Fact]
        public void MultipleArguments()
        {
            Assert.True(FormatString.TryParse("{1} {0}{2} ", out var expressions));
            Assert.Equal(5, expressions.Length);
            Assert.Equal(1, expressions[0].Argument);
            Assert.Equal(" ", expressions[1].String);
            Assert.Equal(0, expressions[2].Argument);
            Assert.Equal(2, expressions[3].Argument);
            Assert.Equal(" ", expressions[4].String);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class TypeGenerator : TestBase
    {
        protected TypeGenerator(ITestOutputHelper output, ContextProvider contextProvider)
            : base(output, contextProvider)
        { }


        public struct CompositeStruct
        {
            public CompositeStructComponent1 Field;
        }

        public struct CompositeStructComponent1
        {
            public CompositeStructComponent2 Field;
        }

        public struct CompositeStructComponent2
        {
            public int Field1;
            public float Field2;
        }
        
        public static void CompositeStructTypeKernel(Index index, CompositeStruct nestedStruct)
        {
        }

        [Theory]
        [InlineData(1)]
        [KernelMethod(nameof(CompositeStructTypeKernel))]
        public void CompositeStructGeneratedCorrectly(int length)
        {
            Execute(
                length,
                new CompositeStruct()
            );
        }
    }
}

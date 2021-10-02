// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: EnumValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class EnumValues : TestBase
    {
        protected EnumValues(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        internal enum BasicEnum1 : byte
        {
            Val0 = 0,
            Val1 = 1,
            Val2 = byte.MaxValue,
        }

        internal enum BasicEnum2 : short
        {
            Val0 = 1,
            Val1 = short.MinValue,
            Val2 = short.MaxValue,
        }

        internal enum BasicEnum3 : int
        {
            Val0 = 23,
            Val1 = int.MinValue,
            Val2 = int.MaxValue,
        }

        internal enum BasicEnum4 : long
        {
            Val0 = 23,
            Val1 = int.MinValue,
            Val2 = int.MaxValue,
        }

        public static TheoryData<object> EnumInteropData => new TheoryData<object>
        {
            { BasicEnum1.Val1 },
            { BasicEnum2.Val2 },
            { BasicEnum3.Val0 },
            { BasicEnum4.Val2 },
            { new TestStruct<BasicEnum2>()
            {
                Val0 = 1,
                Val1 = BasicEnum2.Val2,
                Val2 = short.MinValue
            } },
            { new TestStruct<BasicEnum2, TestStruct<BasicEnum3, BasicEnum1>>()
            {
                Val0 = BasicEnum2.Val1,
                Val1 = ushort.MaxValue,
                Val2 = new TestStruct<BasicEnum3, BasicEnum1>()
                {
                    Val0 = BasicEnum3.Val0,
                    Val1 = 23,
                    Val2 = BasicEnum1.Val1,
                }
            } }
        };

        internal static void EnumInteropKernel<T>(
            Index1D index,
            ArrayView1D<T, Stride1D.Dense> data,
            T value)
            where T : unmanaged
        {
            data[index] = value;
        }

        [Theory]
        [MemberData(nameof(EnumInteropData))]
        [KernelMethod(nameof(EnumInteropKernel))]
        public void EnumInterop<T>(T value)
            where T : unmanaged
        {
            using var buffer = Accelerator.Allocate1D<T>(1);
            Execute<Index1D, T>(buffer.IntExtent, buffer.View, value);

            var expected = new T[] { value };
            Verify(buffer.View, expected);
        }
    }
}

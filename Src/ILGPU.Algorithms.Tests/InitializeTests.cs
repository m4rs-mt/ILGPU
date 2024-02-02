// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: InitializeTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Tests;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Algorithms.Tests
{
    public abstract partial class InitializeTests : TestBase
    {
        protected InitializeTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        #region MemberData

        public static TheoryData<object, int> SimpleTestData =>
            new TheoryData<object, int>
        {
            { sbyte.MinValue, 1 },
            { byte.MaxValue, 1 },
            { short.MinValue, 1 },
            { ushort.MaxValue, 1 },
            { int.MinValue, 1 },
            { uint.MaxValue, 1 },
            { long.MinValue, 1 },
            { ulong.MaxValue, 1 },
            { float.Epsilon, 1 },
            { double.Epsilon, 1 },
            { default(EmptyStruct), 1 },
            { default(TestStruct), 1 },
            { default(TestStruct<TestStruct<byte>>), 1 },

            { byte.MaxValue, 2 },
            { short.MinValue, 2 },
            { int.MinValue, 2 },
            { long.MinValue, 2 },
            { default(EmptyStruct), 2 },
            { default(TestStruct), 2 },
            { default(TestStruct<TestStruct<byte>>), 2 },

            { byte.MaxValue, 31 },
            { short.MinValue, 31 },
            { int.MinValue, 31 },
            { long.MinValue, 31 },
            { default(EmptyStruct), 31 },
            { default(TestStruct<TestStruct<ushort>>), 31 },

            { byte.MaxValue, 32 },
            { short.MinValue, 32 },
            { int.MinValue, 32 },
            { long.MinValue, 32 },
            { default(EmptyStruct), 32 },
            { default(TestStruct<TestStruct<float>>), 32 },

            { byte.MaxValue, 33 },
            { short.MinValue, 33 },
            { int.MinValue, 33 },
            { long.MinValue, 33 },
            { default(EmptyStruct), 33 },
            { default(TestStruct<TestStruct<int>>), 33 },

            { byte.MaxValue, 65 },
            { short.MinValue, 65 },
            { int.MinValue, 65 },
            { long.MinValue, 65 },
            { default(EmptyStruct), 65 },
            { default(TestStruct<TestStruct<short>>), 65 },

            { byte.MaxValue, 127 },
            { short.MinValue, 127 },
            { int.MinValue, 127 },
            { long.MinValue, 127 },
            { default(EmptyStruct), 127 },
            { default(TestStruct<TestStruct<long>>), 127 },

            { byte.MaxValue, 1000 },
            { short.MinValue, 1000 },
            { int.MinValue, 1000 },
            { long.MinValue, 1000 },
            { default(EmptyStruct), 1000 },
            { default(TestStruct<TestStruct<long>>), 1000 },

            { byte.MaxValue, 10000 },
            { short.MinValue, 10000 },
            { int.MinValue, 10000 },
            { long.MinValue, 10000 },
            { default(EmptyStruct), 10000 },
            { default(TestStruct<TestStruct<long>>), 10000 },

            { byte.MaxValue, 100000 },
            { short.MinValue, 100000 },
            { int.MinValue, 100000 },
            { long.MinValue, 100000 },
            { default(EmptyStruct), 100000 },
            { default(TestStruct<TestStruct<long>>), 100000 },
        };

        #endregion

        [Theory]
        [MemberData(nameof(SimpleTestData))]
        public void InitializeSimple<T, TArraySize>(T value, int size)
            where T : unmanaged
        {
            using var buffer = Accelerator.Allocate1D<T>(size);
            using var stream = Accelerator.CreateStream();
            Accelerator.Initialize(stream, buffer.View, value);

            stream.Synchronize();
            var expected = Enumerable.Repeat(value, size).ToArray();
            Verify(buffer.View, expected);
        }
    }
}

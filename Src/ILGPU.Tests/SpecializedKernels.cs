// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: SpecializedKernels.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract partial class SpecializedKernels : TestBase
    {
        protected SpecializedKernels(
            ITestOutputHelper output,
            TestContext testContext)
            : base(output, testContext)
        { }

        public static TheoryData<object> SpecializedValueTestData =>
            new TheoryData<object>
        {
            { default(sbyte) },
            { default(byte) },
            { default(short) },
            { default(ushort) },
            { default(int) },
            { default(uint) },
            { default(long) },
            { default(ulong) },
            { default(float) },
            { default(double) },
            { default(EmptyStruct) },
            { default(NoHashCodeStruct) },
            { default(TestStruct) },
            { default(TestStructEquatable<TestStructEquatable<byte>>) },
            { default(
                TestStructEquatable<int, TestStructEquatable<short, EmptyStruct>>) },
        };

        internal static void SpecializedImplicitValueKernel<T>(
            Index1D _,
            ArrayView1D<T, Stride1D.Dense> data,
            SpecializedValue<T> value)
            where T : unmanaged, IEquatable<T>
        {
            data[0] = value;
        }

        [Theory]
        [MemberData(nameof(SpecializedValueTestData))]
        [KernelMethod(nameof(SpecializedImplicitValueKernel))]
        public void SpecializedImplicitKernel<T>(T value)
            where T : unmanaged, IEquatable<T>
        {
            var method = KernelMethodAttribute.GetKernelMethod(
                new Type[] { typeof(T) });
            var kernel = Accelerator.LoadAutoGroupedKernel(
                new Action<Index1D, ArrayView1D<T, Stride1D.Dense>, SpecializedValue<T>>(
                    SpecializedImplicitValueKernel));
            using var buffer = Accelerator.Allocate1D<T>(1);
            kernel(
                Accelerator.DefaultStream,
                1,
                buffer.View,
                new SpecializedValue<T>(value));
            Accelerator.Synchronize();

            var expected = new T[] { value };
            Verify(buffer.View, expected);
        }

        internal static void SpecializedExplicitValueKernel<T>(
            ArrayView1D<T, Stride1D.Dense> data,
            SpecializedValue<T> value)
            where T : unmanaged, IEquatable<T>
        {
            data[0] = value;
        }

        [Theory]
        [MemberData(nameof(SpecializedValueTestData))]
        [KernelMethod(nameof(SpecializedExplicitValueKernel))]
        public void SpecializedExplicitKernel<T>(T value)
            where T : unmanaged, IEquatable<T>
        {
            var method = KernelMethodAttribute.GetKernelMethod(
                new Type[] { typeof(T) });
            var kernel = Accelerator.LoadKernel(
                new Action<ArrayView1D<T, Stride1D.Dense>, SpecializedValue<T>>(
                    SpecializedExplicitValueKernel));
            using var buffer = Accelerator.Allocate1D<T>(1);
            kernel(
                Accelerator.DefaultStream,
                (1, 1),
                buffer.View,
                new SpecializedValue<T>(value));
            Accelerator.Synchronize();

            var expected = new T[] { value };
            Verify(buffer.View, expected);
        }
    }
}

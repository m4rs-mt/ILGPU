// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: InterfaceTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Tests
{
    public abstract class InterfaceTests : TestBase
    {
        protected InterfaceTests(ITestOutputHelper output, TestContext testContext) : base(output, testContext) { }

        [Fact]
        [KernelMethod(nameof(TestKernel2))]
        public void TestNestedDefaultInterface() {
            float[] expected = [138f];

            var array = Accelerator.Allocate1D([new TestStruct(2, 67)]);
            var output = Accelerator.Allocate1D<float>(1);

            var kernel = Accelerator.LoadAutoGroupedStreamKernel((Index1D i, ArrayView<TestStruct> data, ArrayView<float> output) => output[i] = TestKernel2(data[i]));
            kernel(array.View.IntExtent, array.View, output.View);

            Verify(output.View, expected);
        }

        [Fact]
        [KernelMethod(nameof(TestKernel1))]
        public void TestDefaultInterface() {
            float[] expected = [34.5f];

            var array = Accelerator.Allocate1D([new TestStruct(2, 67)]);
            var output = Accelerator.Allocate1D<float>(1);

            var kernel = Accelerator.LoadAutoGroupedStreamKernel((Index1D i, ArrayView<TestStruct> data, ArrayView<float> output) => output[i] = TestKernel1(data[i]));
            kernel(array.View.IntExtent, array.View, output.View);

            Verify(output.View, expected);
        }

        [Fact]
        [KernelMethod(nameof(TestKernel3))]
        public void TestDefaultInterfaceOverloading() {
            float[] expected = [69f];

            var array = Accelerator.Allocate1D([new TestStruct2(2, 67)]);
            var output = Accelerator.Allocate1D<float>(1);

            var kernel = Accelerator.LoadAutoGroupedStreamKernel((Index1D i, ArrayView<TestStruct2> data, ArrayView<float> output) => output[i] = TestKernel3(data[i]));
            kernel(array.View.IntExtent, array.View, output.View);

            Verify(output.View, expected);
        }

        private static float TestKernel1<TS>(TS v) where TS : ITestInterface {
            return v.Center;
        }

        private static float TestKernel2<TS>(TS v) where TS : ITestInterface {
            return v.NestedCenterCall;
        }

        private static float TestKernel3<TS>(TS v) where TS : ITestInterface {
            return v.Center;
        }

        public interface ITestInterface {
            public float Min { get; }
            public float Max { get; }
            public float Center => (Min + Max)/2;
            public float NestedCenterCall {
                [MethodImpl(MethodImplOptions.NoInlining)]
                get => Center*4;
            }
        }

        public record struct TestStruct(float Min, float Max) : ITestInterface;

        //Demonstrate correct overloading
        public record struct TestStruct2(float Min, float Max) : ITestInterface {
            float ITestInterface.Center => Min + Max;
        }
    }

}

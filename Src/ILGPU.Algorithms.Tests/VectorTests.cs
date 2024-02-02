// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2021-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: VectorTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.Random;
using ILGPU.Algorithms.Vectors;
using ILGPU.Runtime;
using ILGPU.Tests;
using System.Numerics;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable xUnit1026

namespace ILGPU.Algorithms.Tests
{
    public abstract partial class VectorTests : TestBase
    {
        protected VectorTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        #region MemberData

        public static TheoryData<int, object> Vector2dTestData =>
            new TheoryData<int, object>
            {
                { 1, default(Vector2Zero) },
                { 32, default(Vector2One) },
                { 64, default(Vector2UnitX) },
                { 128, default(Vector2UnitY) },
            };

        public static TheoryData<int, object> Vector3dTestData =>
            new TheoryData<int, object>
            {
                { 1, default(Vector3Zero) },
                { 32, default(Vector3One) },
                { 64, default(Vector3UnitX) },
                { 128, default(Vector3UnitY) },
                { 65, default(Vector3UnitZ) },
            };

        public static TheoryData<int, object> Vector4dTestData =>
            new TheoryData<int, object>
            {
                { 1, default(Vector4Zero) },
                { 32, default(Vector4One) },
                { 64, default(Vector4UnitX) },
                { 128, default(Vector4UnitY) },
                { 127, default(Vector4UnitZ) },
                { 48, default(Vector4UnitW) },
            };
        
#if NET7_0_OR_GREATER
        
        public static TheoryData<object, object, object, object>
            VectorStaticAbstractData =>
            new TheoryData<object, object, object, object>
            {
                { Int32x2.Zero, Int32x2.One, default(int),
                    XorShift64Star.Create(new System.Random(1337)) },
            };
        
#endif

        #endregion

        #region Kernels

        internal static void Vector2dAddKernel(
            Index1D index,
            ArrayView1D<Vector2, Stride1D.Dense> input,
            Vector2 operand)
        {
            var target = input.VariableView(index);
            target.AtomicAdd(operand);
        }

        internal static void Vector3dAddKernel(
            Index1D index,
            ArrayView1D<Vector3, Stride1D.Dense> input,
            Vector3 operand)
        {
            var target = input.VariableView(index);
            target.AtomicAdd(operand);
        }

        internal static void Vector4dAddKernel(
            Index1D index,
            ArrayView1D<Vector4, Stride1D.Dense> input,
            Vector4 operand)
        {
            var target = input.VariableView(index);
            target.AtomicAdd(operand);
        }
        
#if NET7_0_OR_GREATER
        
        internal static void VectorStaticAbstractCallKernel<
            TNumericType,
            TElementType,
            TRandom>(
            Index1D index,
            ArrayView1D<TNumericType, Stride1D.Dense> output,
            TNumericType lowerBound,
            TNumericType upperBound,
            TRandom random)
            where TNumericType : unmanaged, IVectorType<TNumericType, TElementType>
            where TElementType : unmanaged, INumber<TElementType>
            where TRandom : unmanaged, IRandomProvider<TRandom>
        {
            var initPosition = TNumericType.GetRandom(
                ref random,
                lowerBound,
                upperBound);
            output[index] = initPosition;
        }
        
#endif

        #endregion

        [Theory]
        [MemberData(nameof(Vector2dTestData))]
        [KernelMethod(nameof(Vector2dAddKernel))]
        public void Vector2dAdd<TVector>(int size, TVector vector)
            where TVector : struct, IVector<Vector2>
        {
            using var stream = Accelerator.CreateStream();
            using var targetBuffer = Accelerator.Allocate1D<Vector2>(size);

            var sequencer = new Vector2DSequencer();
            var sequence = sequencer.ComputeSequence(
                new Vector2(0, size - 1),
                new Vector2(1, -1),
                size);
            targetBuffer.CopyFromCPU(stream, sequence);

            Execute(targetBuffer.Length, targetBuffer.View, vector.GetVector());

            var expected = new Vector2[size];
            for (int i = 0; i < size; ++i)
                expected[i] = new Vector2(i, size - 1 - i) + vector.GetVector();

            Verify(targetBuffer.View, expected);
        }

        [Theory]
        [MemberData(nameof(Vector3dTestData))]
        [KernelMethod(nameof(Vector3dAddKernel))]
        public void Vector3dAdd<TVector>(int size, TVector vector)
            where TVector : struct, IVector<Vector3>
        {
            using var targetBuffer = Accelerator.Allocate1D<Vector3>(size);
            using var stream = Accelerator.CreateStream();

            var sequencer = new Vector3DSequencer();
            var sequence = sequencer.ComputeSequence(
                new Vector3(0, size - 1, 0),
                new Vector3(1, -1, 1),
                size);
            targetBuffer.CopyFromCPU(stream, sequence);

            Execute(targetBuffer.Length, targetBuffer.View, vector.GetVector());

            var expected = new Vector3[size];
            for (int i = 0; i < size; ++i)
                expected[i] = new Vector3(i, size - 1 - i, i) + vector.GetVector();

            Verify(targetBuffer.View, expected);
        }

        [Theory]
        [MemberData(nameof(Vector4dTestData))]
        [KernelMethod(nameof(Vector4dAddKernel))]
        public void Vector4dAdd<TVector>(int size, TVector vector)
            where TVector : struct, IVector<Vector4>
        {
            using var targetBuffer = Accelerator.Allocate1D<Vector4>(size);
            using var stream = Accelerator.CreateStream();

            var sequencer = new Vector4DSequencer();
            var sequence = sequencer.ComputeSequence(
                new Vector4(0, size - 1, 0, 0),
                new Vector4(1, -1, 1, 0),
                size);
            targetBuffer.CopyFromCPU(stream, sequence);

            Execute(targetBuffer.Length, targetBuffer.View, vector.GetVector());

            var expected = new Vector4[size];
            for (int i = 0; i < size; ++i)
                expected[i] = new Vector4(i, size - 1 - i, i, 0) + vector.GetVector();

            Verify(targetBuffer.View, expected);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 2)]
        [InlineData(2, 1)]
        public void Index2Vector2Conv(float x, float y)
        {
            Vector2 initVector = new Vector2(x, y);
            Index2D initIndex = new Index2D((int)x, (int)y);

            Index2D index = initVector.ToIndex();
            Vector2 vector = index.ToVector();

            Assert.Equal(initVector, vector);
            Assert.Equal(initIndex, index);
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(1, 2, 3)]
        [InlineData(3, 2, 1)]
        public void Index3Vector3Conv(float x, float y, float z)
        {
            Vector3 initVector = new Vector3(x, y, z);
            Index3D initIndex = new Index3D((int)x, (int)y, (int)z);

            Index3D index = initVector.ToIndex();
            Vector3 vector = index.ToVector();

            Assert.Equal(initVector, vector);
            Assert.Equal(initIndex, index);
        }
        
#if NET7_0_OR_GREATER
        [Theory]
        [MemberData(nameof(VectorStaticAbstractData))]
        [KernelMethod(nameof(VectorStaticAbstractCallKernel))]
        public void VectorStaticAbstractCall<TNumericType, TElementType, TRandom>(
            TNumericType lowerBound,
            TNumericType upperBound,
            TElementType _,
            TRandom random)
            where TNumericType : unmanaged
            where TElementType : unmanaged
            where TRandom : unmanaged
        {
            const int Length = 32;
            using var targetBuffer = Accelerator.Allocate1D<TNumericType>(Length);

            Execute<Index1D, TNumericType, TElementType, TRandom>(
                targetBuffer.IntExtent,
                targetBuffer.View,
                lowerBound,
                upperBound,
                random);
        }
#endif
    }
}

#pragma warning restore xUnit1026

using ILGPU.Runtime;
using ILGPU.Tests;
using System.Numerics;
using Xunit;
using Xunit.Abstractions;

namespace ILGPU.Algorithms.Tests
{
    public abstract partial class VectorTests : TestBase
    {
        protected VectorTests(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        #region MemberData

        public static TheoryData<object, object> Vector2dTestData =>
            new TheoryData<object, object>
            {
                { 1, default(Vector2Zero) },
                { 32, default(Vector2One) },
                { 64, default(Vector2UnitX) },
                { 128, default(Vector2UnitY) },
            };

        public static TheoryData<object, object> Vector3dTestData =>
            new TheoryData<object, object>
            {
                { 1, default(Vector3Zero) },
                { 32, default(Vector3One) },
                { 64, default(Vector3UnitX) },
                { 128, default(Vector3UnitY) },
                { 65, default(Vector3UnitZ) },
            };

        public static TheoryData<object, object> Vector4dTestData =>
            new TheoryData<object, object>
            {
                { 1, default(Vector4Zero) },
                { 32, default(Vector4One) },
                { 64, default(Vector4UnitX) },
                { 128, default(Vector4UnitY) },
                { 127, default(Vector4UnitZ) },
                { 48, default(Vector4UnitW) },
            };

        #endregion

        #region Kernels

        internal static void Vector2dAddKernel(
            Index1 index,
            ArrayView<Vector2> input,
            Vector2 operand)
        {
            var target = input.GetVariableView(index);
            target.AtomicAdd(operand);
        }

        internal static void Vector3dAddKernel(
            Index1 index,
            ArrayView<Vector3> input,
            Vector3 operand)
        {
            var target = input.GetVariableView(index);
            target.AtomicAdd(operand);
        }

        internal static void Vector4dAddKernel(
            Index1 index,
            ArrayView<Vector4> input,
            Vector4 operand)
        {
            var target = input.GetVariableView(index);
            target.AtomicAdd(operand);
        }

        #endregion

        [Theory]
        [MemberData(nameof(Vector2dTestData))]
        [KernelMethod(nameof(Vector2dAddKernel))]
        public void Vector2dAdd<TVector>(int size, TVector vector)
            where TVector : struct, IVector<Vector2>
        {
            using var stream = Accelerator.CreateStream();
            using var targetBuffer = Accelerator.Allocate<Vector2>(size);

            var sequencer = new Vector2DSequencer();
            var sequence = sequencer.ComputeSequence(
                new Vector2(0, size - 1),
                new Vector2(1, -1),
                size);
            targetBuffer.CopyFrom(stream, sequence, 0, 0, size);
            stream.Synchronize();
            
            Execute(targetBuffer.Length, targetBuffer.View, vector.GetVector());

            var expected = new Vector2[size];
            for (int i = 0; i < size; ++i)
                expected[i] = new Vector2(i, size - 1 - i) + vector.GetVector();

            Verify(targetBuffer, expected);
        }

        [Theory]
        [MemberData(nameof(Vector3dTestData))]
        [KernelMethod(nameof(Vector3dAddKernel))]
        public void Vector3dAdd<TVector>(int size, TVector vector)
            where TVector : struct, IVector<Vector3>
        {
            using var targetBuffer = Accelerator.Allocate<Vector3>(size);
            using var stream = Accelerator.CreateStream();

            var sequencer = new Vector3DSequencer();
            var sequence = sequencer.ComputeSequence(
                new Vector3(0, size - 1, 0),
                new Vector3(1, -1, 1),
                size);
            targetBuffer.CopyFrom(stream, sequence, 0, 0, size);
            stream.Synchronize();

            Execute(targetBuffer.Length, targetBuffer.View, vector.GetVector());

            var expected = new Vector3[size];
            for (int i = 0; i < size; ++i)
                expected[i] = new Vector3(i, size - 1 - i, i) + vector.GetVector();

            Verify(targetBuffer, expected);
        }

        [Theory]
        [MemberData(nameof(Vector4dTestData))]
        [KernelMethod(nameof(Vector4dAddKernel))]
        public void Vector4dAdd<TVector>(int size, TVector vector)
            where TVector : struct, IVector<Vector4>
        {
            using var targetBuffer = Accelerator.Allocate<Vector4>(size);
            using var stream = Accelerator.CreateStream();

            var sequencer = new Vector4DSequencer();
            var sequence = sequencer.ComputeSequence(
                new Vector4(0, size - 1, 0, 0),
                new Vector4(1, -1, 1, 0),
                size);
            targetBuffer.CopyFrom(stream, sequence, 0, 0, size);
            stream.Synchronize();

            Execute(targetBuffer.Length, targetBuffer.View, vector.GetVector());

            var expected = new Vector4[size];
            for (int i = 0; i < size; ++i)
                expected[i] = new Vector4(i, size - 1 - i, i, 0) + vector.GetVector();

            Verify(targetBuffer, expected);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 2)]
        [InlineData(2, 1)]
        public void Index2Vector2Conv(float x, float y)
        {
            Vector2 initVector = new Vector2(x, y);
            Index2 initIndex = new Index2((int)x, (int)y);

            Index2 index = initVector.ToIndex();
            Vector2 vector = index.ToVector();

            Assert.Equal(initVector, vector);
            Assert.Equal(initIndex, index);
        }

        [Theory]
        [InlineData(0,0,0)]
        [InlineData(1,2,3)]
        [InlineData(3,2,1)]
        public void Index3Vector3Conv(float x, float y, float z)
        {
            Vector3 initVector = new Vector3(x, y, z);
            Index3 initIndex = new Index3((int)x, (int)y, (int)z);

            Index3 index = initVector.ToIndex();
            Vector3 vector = index.ToVector();

            Assert.Equal(initVector, vector);
            Assert.Equal(initIndex, index);
        }
    }
}

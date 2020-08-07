// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: TypeInformation.ttinclude
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------



using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA2231 // Overload operator equals on overriding value type Equals
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not
// override Object.GetHashCode()

namespace ILGPU.Tests
{
    public unsafe struct FixedBufferStructInt8 :
        IXunitSerializable,
        IEquatable<FixedBufferStructInt8>
    {
        public fixed sbyte Data[FixedBuffers.Length];

        public FixedBufferStructInt8(sbyte data)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = data;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = info.GetValue<sbyte>(nameof(Data) + i);
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                info.AddValue(nameof(Data) + i, Data[i]);
        }

        public bool Equals(FixedBufferStructInt8 buffer)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
            {
                if (Data[i] != buffer.Data[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) =>
            obj is FixedBufferStructInt8 fixedStruct &&
            Equals(fixedStruct);
    }

    public unsafe struct FixedBufferStructInt16 :
        IXunitSerializable,
        IEquatable<FixedBufferStructInt16>
    {
        public fixed short Data[FixedBuffers.Length];

        public FixedBufferStructInt16(short data)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = data;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = info.GetValue<short>(nameof(Data) + i);
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                info.AddValue(nameof(Data) + i, Data[i]);
        }

        public bool Equals(FixedBufferStructInt16 buffer)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
            {
                if (Data[i] != buffer.Data[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) =>
            obj is FixedBufferStructInt16 fixedStruct &&
            Equals(fixedStruct);
    }

    public unsafe struct FixedBufferStructInt32 :
        IXunitSerializable,
        IEquatable<FixedBufferStructInt32>
    {
        public fixed int Data[FixedBuffers.Length];

        public FixedBufferStructInt32(int data)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = data;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = info.GetValue<int>(nameof(Data) + i);
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                info.AddValue(nameof(Data) + i, Data[i]);
        }

        public bool Equals(FixedBufferStructInt32 buffer)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
            {
                if (Data[i] != buffer.Data[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) =>
            obj is FixedBufferStructInt32 fixedStruct &&
            Equals(fixedStruct);
    }

    public unsafe struct FixedBufferStructInt64 :
        IXunitSerializable,
        IEquatable<FixedBufferStructInt64>
    {
        public fixed long Data[FixedBuffers.Length];

        public FixedBufferStructInt64(long data)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = data;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = info.GetValue<long>(nameof(Data) + i);
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                info.AddValue(nameof(Data) + i, Data[i]);
        }

        public bool Equals(FixedBufferStructInt64 buffer)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
            {
                if (Data[i] != buffer.Data[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) =>
            obj is FixedBufferStructInt64 fixedStruct &&
            Equals(fixedStruct);
    }

    public unsafe struct FixedBufferStructUInt8 :
        IXunitSerializable,
        IEquatable<FixedBufferStructUInt8>
    {
        public fixed byte Data[FixedBuffers.Length];

        public FixedBufferStructUInt8(byte data)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = data;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = info.GetValue<byte>(nameof(Data) + i);
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                info.AddValue(nameof(Data) + i, Data[i]);
        }

        public bool Equals(FixedBufferStructUInt8 buffer)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
            {
                if (Data[i] != buffer.Data[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) =>
            obj is FixedBufferStructUInt8 fixedStruct &&
            Equals(fixedStruct);
    }

    public unsafe struct FixedBufferStructUInt16 :
        IXunitSerializable,
        IEquatable<FixedBufferStructUInt16>
    {
        public fixed ushort Data[FixedBuffers.Length];

        public FixedBufferStructUInt16(ushort data)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = data;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = info.GetValue<ushort>(nameof(Data) + i);
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                info.AddValue(nameof(Data) + i, Data[i]);
        }

        public bool Equals(FixedBufferStructUInt16 buffer)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
            {
                if (Data[i] != buffer.Data[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) =>
            obj is FixedBufferStructUInt16 fixedStruct &&
            Equals(fixedStruct);
    }

    public unsafe struct FixedBufferStructUInt32 :
        IXunitSerializable,
        IEquatable<FixedBufferStructUInt32>
    {
        public fixed uint Data[FixedBuffers.Length];

        public FixedBufferStructUInt32(uint data)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = data;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = info.GetValue<uint>(nameof(Data) + i);
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                info.AddValue(nameof(Data) + i, Data[i]);
        }

        public bool Equals(FixedBufferStructUInt32 buffer)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
            {
                if (Data[i] != buffer.Data[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) =>
            obj is FixedBufferStructUInt32 fixedStruct &&
            Equals(fixedStruct);
    }

    public unsafe struct FixedBufferStructUInt64 :
        IXunitSerializable,
        IEquatable<FixedBufferStructUInt64>
    {
        public fixed ulong Data[FixedBuffers.Length];

        public FixedBufferStructUInt64(ulong data)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = data;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                Data[i] = info.GetValue<ulong>(nameof(Data) + i);
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
                info.AddValue(nameof(Data) + i, Data[i]);
        }

        public bool Equals(FixedBufferStructUInt64 buffer)
        {
            for (int i = 0; i < FixedBuffers.Length; ++i)
            {
                if (Data[i] != buffer.Data[i])
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) =>
            obj is FixedBufferStructUInt64 fixedStruct &&
            Equals(fixedStruct);
    }


    public unsafe abstract class FixedBuffers : TestBase
    {
        public const int Length = 9;

        protected FixedBuffers(ITestOutputHelper output, TestContext testContext)
            : base(output, testContext)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustBuffer(
            ref FixedBufferStructInt8 value,
            sbyte scalarValue)
        {
            for (int i = 0; i < Length; ++i)
                value.Data[i] += scalarValue;
        }

        internal static void FixedBufferInt8Kernel(
            Index1 index,
            ArrayView<sbyte> data,
            ArrayView<sbyte> data2,
            FixedBufferStructInt8 value,
            sbyte scalarValue)
        {
            data[index] = value.Data[index];
            AdjustBuffer(ref value, scalarValue);
            data2[index] = value.Data[index];
        }

        [Fact]
        [KernelMethod(nameof(FixedBufferInt8Kernel))]
        public void FixedBufferInt8()
        {
            using var buffer1 = Accelerator.Allocate<sbyte>(Length);
            using var buffer2 = Accelerator.Allocate<sbyte>(Length);

            sbyte scalarValue = 2;
            var fixedBufferData1 = new FixedBufferStructInt8(scalarValue);
            Execute(Length, buffer1.View, buffer2.View, fixedBufferData1, scalarValue);

            var expected1 = Enumerable.Repeat(scalarValue, Length).ToArray();
            var expected2 = Enumerable.Repeat(
                (sbyte)(scalarValue + scalarValue),
                Length).ToArray();
            Verify(buffer1, expected1);
            Verify(buffer2, expected2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustBuffer(
            ref FixedBufferStructInt16 value,
            short scalarValue)
        {
            for (int i = 0; i < Length; ++i)
                value.Data[i] += scalarValue;
        }

        internal static void FixedBufferInt16Kernel(
            Index1 index,
            ArrayView<short> data,
            ArrayView<short> data2,
            FixedBufferStructInt16 value,
            short scalarValue)
        {
            data[index] = value.Data[index];
            AdjustBuffer(ref value, scalarValue);
            data2[index] = value.Data[index];
        }

        [Fact]
        [KernelMethod(nameof(FixedBufferInt16Kernel))]
        public void FixedBufferInt16()
        {
            using var buffer1 = Accelerator.Allocate<short>(Length);
            using var buffer2 = Accelerator.Allocate<short>(Length);

            short scalarValue = 2;
            var fixedBufferData1 = new FixedBufferStructInt16(scalarValue);
            Execute(Length, buffer1.View, buffer2.View, fixedBufferData1, scalarValue);

            var expected1 = Enumerable.Repeat(scalarValue, Length).ToArray();
            var expected2 = Enumerable.Repeat(
                (short)(scalarValue + scalarValue),
                Length).ToArray();
            Verify(buffer1, expected1);
            Verify(buffer2, expected2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustBuffer(
            ref FixedBufferStructInt32 value,
            int scalarValue)
        {
            for (int i = 0; i < Length; ++i)
                value.Data[i] += scalarValue;
        }

        internal static void FixedBufferInt32Kernel(
            Index1 index,
            ArrayView<int> data,
            ArrayView<int> data2,
            FixedBufferStructInt32 value,
            int scalarValue)
        {
            data[index] = value.Data[index];
            AdjustBuffer(ref value, scalarValue);
            data2[index] = value.Data[index];
        }

        [Fact]
        [KernelMethod(nameof(FixedBufferInt32Kernel))]
        public void FixedBufferInt32()
        {
            using var buffer1 = Accelerator.Allocate<int>(Length);
            using var buffer2 = Accelerator.Allocate<int>(Length);

            int scalarValue = 2;
            var fixedBufferData1 = new FixedBufferStructInt32(scalarValue);
            Execute(Length, buffer1.View, buffer2.View, fixedBufferData1, scalarValue);

            var expected1 = Enumerable.Repeat(scalarValue, Length).ToArray();
            var expected2 = Enumerable.Repeat(
                (int)(scalarValue + scalarValue),
                Length).ToArray();
            Verify(buffer1, expected1);
            Verify(buffer2, expected2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustBuffer(
            ref FixedBufferStructInt64 value,
            long scalarValue)
        {
            for (int i = 0; i < Length; ++i)
                value.Data[i] += scalarValue;
        }

        internal static void FixedBufferInt64Kernel(
            Index1 index,
            ArrayView<long> data,
            ArrayView<long> data2,
            FixedBufferStructInt64 value,
            long scalarValue)
        {
            data[index] = value.Data[index];
            AdjustBuffer(ref value, scalarValue);
            data2[index] = value.Data[index];
        }

        [Fact]
        [KernelMethod(nameof(FixedBufferInt64Kernel))]
        public void FixedBufferInt64()
        {
            using var buffer1 = Accelerator.Allocate<long>(Length);
            using var buffer2 = Accelerator.Allocate<long>(Length);

            long scalarValue = 2;
            var fixedBufferData1 = new FixedBufferStructInt64(scalarValue);
            Execute(Length, buffer1.View, buffer2.View, fixedBufferData1, scalarValue);

            var expected1 = Enumerable.Repeat(scalarValue, Length).ToArray();
            var expected2 = Enumerable.Repeat(
                (long)(scalarValue + scalarValue),
                Length).ToArray();
            Verify(buffer1, expected1);
            Verify(buffer2, expected2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustBuffer(
            ref FixedBufferStructUInt8 value,
            byte scalarValue)
        {
            for (int i = 0; i < Length; ++i)
                value.Data[i] += scalarValue;
        }

        internal static void FixedBufferUInt8Kernel(
            Index1 index,
            ArrayView<byte> data,
            ArrayView<byte> data2,
            FixedBufferStructUInt8 value,
            byte scalarValue)
        {
            data[index] = value.Data[index];
            AdjustBuffer(ref value, scalarValue);
            data2[index] = value.Data[index];
        }

        [Fact]
        [KernelMethod(nameof(FixedBufferUInt8Kernel))]
        public void FixedBufferUInt8()
        {
            using var buffer1 = Accelerator.Allocate<byte>(Length);
            using var buffer2 = Accelerator.Allocate<byte>(Length);

            byte scalarValue = 2;
            var fixedBufferData1 = new FixedBufferStructUInt8(scalarValue);
            Execute(Length, buffer1.View, buffer2.View, fixedBufferData1, scalarValue);

            var expected1 = Enumerable.Repeat(scalarValue, Length).ToArray();
            var expected2 = Enumerable.Repeat(
                (byte)(scalarValue + scalarValue),
                Length).ToArray();
            Verify(buffer1, expected1);
            Verify(buffer2, expected2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustBuffer(
            ref FixedBufferStructUInt16 value,
            ushort scalarValue)
        {
            for (int i = 0; i < Length; ++i)
                value.Data[i] += scalarValue;
        }

        internal static void FixedBufferUInt16Kernel(
            Index1 index,
            ArrayView<ushort> data,
            ArrayView<ushort> data2,
            FixedBufferStructUInt16 value,
            ushort scalarValue)
        {
            data[index] = value.Data[index];
            AdjustBuffer(ref value, scalarValue);
            data2[index] = value.Data[index];
        }

        [Fact]
        [KernelMethod(nameof(FixedBufferUInt16Kernel))]
        public void FixedBufferUInt16()
        {
            using var buffer1 = Accelerator.Allocate<ushort>(Length);
            using var buffer2 = Accelerator.Allocate<ushort>(Length);

            ushort scalarValue = 2;
            var fixedBufferData1 = new FixedBufferStructUInt16(scalarValue);
            Execute(Length, buffer1.View, buffer2.View, fixedBufferData1, scalarValue);

            var expected1 = Enumerable.Repeat(scalarValue, Length).ToArray();
            var expected2 = Enumerable.Repeat(
                (ushort)(scalarValue + scalarValue),
                Length).ToArray();
            Verify(buffer1, expected1);
            Verify(buffer2, expected2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustBuffer(
            ref FixedBufferStructUInt32 value,
            uint scalarValue)
        {
            for (int i = 0; i < Length; ++i)
                value.Data[i] += scalarValue;
        }

        internal static void FixedBufferUInt32Kernel(
            Index1 index,
            ArrayView<uint> data,
            ArrayView<uint> data2,
            FixedBufferStructUInt32 value,
            uint scalarValue)
        {
            data[index] = value.Data[index];
            AdjustBuffer(ref value, scalarValue);
            data2[index] = value.Data[index];
        }

        [Fact]
        [KernelMethod(nameof(FixedBufferUInt32Kernel))]
        public void FixedBufferUInt32()
        {
            using var buffer1 = Accelerator.Allocate<uint>(Length);
            using var buffer2 = Accelerator.Allocate<uint>(Length);

            uint scalarValue = 2;
            var fixedBufferData1 = new FixedBufferStructUInt32(scalarValue);
            Execute(Length, buffer1.View, buffer2.View, fixedBufferData1, scalarValue);

            var expected1 = Enumerable.Repeat(scalarValue, Length).ToArray();
            var expected2 = Enumerable.Repeat(
                (uint)(scalarValue + scalarValue),
                Length).ToArray();
            Verify(buffer1, expected1);
            Verify(buffer2, expected2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustBuffer(
            ref FixedBufferStructUInt64 value,
            ulong scalarValue)
        {
            for (int i = 0; i < Length; ++i)
                value.Data[i] += scalarValue;
        }

        internal static void FixedBufferUInt64Kernel(
            Index1 index,
            ArrayView<ulong> data,
            ArrayView<ulong> data2,
            FixedBufferStructUInt64 value,
            ulong scalarValue)
        {
            data[index] = value.Data[index];
            AdjustBuffer(ref value, scalarValue);
            data2[index] = value.Data[index];
        }

        [Fact]
        [KernelMethod(nameof(FixedBufferUInt64Kernel))]
        public void FixedBufferUInt64()
        {
            using var buffer1 = Accelerator.Allocate<ulong>(Length);
            using var buffer2 = Accelerator.Allocate<ulong>(Length);

            ulong scalarValue = 2;
            var fixedBufferData1 = new FixedBufferStructUInt64(scalarValue);
            Execute(Length, buffer1.View, buffer2.View, fixedBufferData1, scalarValue);

            var expected1 = Enumerable.Repeat(scalarValue, Length).ToArray();
            var expected2 = Enumerable.Repeat(
                (ulong)(scalarValue + scalarValue),
                Length).ToArray();
            Verify(buffer1, expected1);
            Verify(buffer2, expected2);
        }
    }
}

#pragma warning restore CA2231 // Overload operator equals on overriding value type Equals
#pragma warning restore CA1051 // Do not declare visible instance fields
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not
// override Object.GetHashCode()
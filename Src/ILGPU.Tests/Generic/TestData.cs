using System;
using Xunit.Abstractions;

#pragma warning disable CA1815 // Override equals and operator equals on value types
#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA2231 // Overload operator equals on overriding value type Equals

// Uses annotated structures supporting the IXunitSerializable interface
// More information can be found here: https://github.com/xunit/xunit/issues/429

namespace ILGPU.Tests
{
    /// <summary>
    /// Implements a test data helper.
    /// </summary>
    public static class TestData
    {
        /// <summary>
        /// Creates a new serializable test data instance.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="data">The data element.</param>
        /// <returns>The wrapped test data instance.</returns>
        public static TestData<T> Create<T>(T data) => new TestData<T>(data);
    }

    /// <summary>
    /// Wraps a test value.
    /// </summary>
    /// <typeparam name="T">The value to wrap.</typeparam>
    public class TestData<T> : IXunitSerializable
    {
        public TestData() { }

        public TestData(T value)
        {
            Value = value;
        }

        public T Value { get; private set; }

        public void Deserialize(IXunitSerializationInfo info)
        {
            Value = info.GetValue<T>(nameof(Value));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Value), Value);
        }

        public override string ToString() => Value.ToString();
    }

    #region Data Structures

    /// <summary>
    /// An abstract value structure that contains a nested property of type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface IValueStructure<T>
    {
        /// <summary>
        /// The nested element value.
        /// </summary>
        T NestedValue { get; set; }
    }

    [Serializable]
    public struct EmptyStruct : IXunitSerializable, IEquatable<EmptyStruct>
    {
        public void Deserialize(IXunitSerializationInfo info) { }

        public void Serialize(IXunitSerializationInfo info) { }

        public bool Equals(EmptyStruct other) => true;

        public override bool Equals(object obj) =>
            obj is EmptyStruct other && Equals(other);

        public override int GetHashCode() => 0;
    }

    [Serializable]
    public struct TestStruct : IXunitSerializable, IEquatable<TestStruct>
    {
        public int X;
        public long Y;
        public short Z;
        public int W;

        public void Deserialize(IXunitSerializationInfo info)
        {
            X = info.GetValue<int>(nameof(X));
            Y = info.GetValue<long>(nameof(Y));
            Z = info.GetValue<short>(nameof(Z));
            W = info.GetValue<int>(nameof(W));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(X), X);
            info.AddValue(nameof(Y), Y);
            info.AddValue(nameof(Z), Z);
            info.AddValue(nameof(W), W);
        }

        public bool Equals(TestStruct other) =>
            X == other.X &&
            Y == other.Y &&
            Z == other.Z &&
            W == other.W;

        public override bool Equals(object obj) =>
            obj is TestStruct other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(X, Y, Z, W);
    }

    [Serializable]
    public struct TestStruct<T> : IXunitSerializable, IValueStructure<T>
        where T : struct
    {
        public byte Val0;
        public T Val1;
        public short Val2;

        T IValueStructure<T>.NestedValue
        {
            get => Val1;
            set => Val1 = value;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            Val0 = info.GetValue<byte>(nameof(Val0));
            Val1 = info.GetValue<T>(nameof(Val1));
            Val2 = info.GetValue<short>(nameof(Val2));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Val0), Val0);
            info.AddValue(nameof(Val1), Val1);
            info.AddValue(nameof(Val2), Val2);
        }

        public override int GetHashCode() =>
            HashCode.Combine(Val0, Val1, Val2);

        public override string ToString() => $"{Val0}, {Val1}, {Val2}";
    }

    [Serializable]
    public struct TestStructEquatable<T> :
        IXunitSerializable,
        IValueStructure<T>,
        IEquatable<TestStructEquatable<T>>
        where T : struct, IEquatable<T>
    {
        private TestStruct<T> data;

        public byte Val0
        {
            get => data.Val0;
            set => data.Val0 = value;
        }

        public T Val1
        {
            get => data.Val1;
            set => data.Val1 = value;
        }

        public short Val2
        {
            get => data.Val2;
            set => data.Val2 = value;
        }

        T IValueStructure<T>.NestedValue
        {
            get => data.Val1;
            set => data.Val1 = value;
        }

        public void Deserialize(IXunitSerializationInfo info) =>
            data.Deserialize(info);

        public void Serialize(IXunitSerializationInfo info) =>
            data.Serialize(info);

        public bool Equals(TestStructEquatable<T> other) =>
            Val0 == other.Val0 &&
            Val2 == other.Val2 &&
            Val1.Equals(other.Val1);

        public override bool Equals(object obj) =>
            obj is TestStructEquatable<T> other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Val0, Val1, Val2);

        public override string ToString() => data.ToString();
    }

    [Serializable]
    public struct TestStruct<T1, T2> : IXunitSerializable, IValueStructure<T2>
        where T1 : struct
        where T2 : struct
    {
        public T1 Val0;
        public ushort Val1;
        public T2 Val2;

        T2 IValueStructure<T2>.NestedValue
        {
            get => Val2;
            set => Val2 = value;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            Val0 = info.GetValue<T1>(nameof(Val0));
            Val1 = info.GetValue<ushort>(nameof(Val1));
            Val2 = info.GetValue<T2>(nameof(Val2));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Val0), Val0);
            info.AddValue(nameof(Val1), Val1);
            info.AddValue(nameof(Val2), Val2);
        }

        public override int GetHashCode() =>
            HashCode.Combine(Val0, Val1, Val2);

        public override string ToString() => $"{Val0}, {Val1}, {Val2}";
    }

    [Serializable]
    public struct TestStructEquatable<T1, T2> :
        IXunitSerializable,
        IValueStructure<T2>,
        IEquatable<TestStructEquatable<T1, T2>>
        where T1 : struct, IEquatable<T1>
        where T2 : struct, IEquatable<T2>
    {
        private TestStruct<T1, T2> data;

        public T1 Val0
        {
            get => data.Val0;
            set => data.Val0 = value;
        }

        public ushort Val1
        {
            get => data.Val1;
            set => data.Val1 = value;
        }

        public T2 Val2
        {
            get => data.Val2;
            set => data.Val2 = value;
        }

        T2 IValueStructure<T2>.NestedValue
        {
            get => data.Val2;
            set => data.Val2 = value;
        }

        public void Deserialize(IXunitSerializationInfo info) =>
            data.Deserialize(info);

        public void Serialize(IXunitSerializationInfo info) =>
            data.Serialize(info);

        public bool Equals(TestStructEquatable<T1, T2> other) =>
            Val1 == other.Val1 &&
            Val0.Equals(other.Val0) &&
            Val2.Equals(other.Val2);

        public override bool Equals(object obj) =>
            obj is TestStructEquatable<T1, T2> other && Equals(other);

        public override int GetHashCode() => data.GetHashCode();

        public override string ToString() => data.ToString();
    }

    public struct DeepStructure<T> : IXunitSerializable
        where T : struct
    {
        public TestStruct<
            T,
            TestStruct<
                long,
                TestStruct<
                    float,
                    TestStruct<
                        T,
                        T>>>> Value;

        public DeepStructure(T val0, T val1, T val2)
        {
            Value = new TestStruct<
                T,
                TestStruct<
                    long,
                    TestStruct<
                        float,
                        TestStruct<
                            T,
                            T>>>>()
            {
                Val0 = val0,
                Val2 = new TestStruct<
                    long,
                    TestStruct<float, TestStruct<T, T>>>()
                {
                    Val2 = new TestStruct<
                        float,
                        TestStruct<T, T>>()
                    {

                        Val2 = new TestStruct<T, T>()
                        {
                            Val0 = val1,
                            Val2 = val2,
                        }
                    }
                }
            };
        }

        public T Val0 => Value.Val0;

        public T Val1 => Value.Val2.Val2.Val2.Val0;

        public T Val2 => Value.Val2.Val2.Val2.Val2;

        public void Deserialize(IXunitSerializationInfo info) =>
            Value.Deserialize(info);

        public void Serialize(IXunitSerializationInfo info) =>
            Value.Serialize(info);
    }

    #endregion

    #region Length Structures

    /// <summary>
    /// An abstraction to inline a specialized sizes.
    /// </summary>
    public interface ILength : IXunitSerializable
    {
        int Length { get; }
    }

    /// <summary>
    /// Array size of 0.
    /// </summary>
    public struct Length0 : ILength
    {
        public int Length => 0;

        public void Deserialize(IXunitSerializationInfo info) { }

        public void Serialize(IXunitSerializationInfo info) { }
    }

    /// <summary>
    /// Array size of 1.
    /// </summary>
    public struct Length1 : ILength
    {
        public int Length => 1;

        public void Deserialize(IXunitSerializationInfo info) { }

        public void Serialize(IXunitSerializationInfo info) { }
    }

    /// <summary>
    /// Array size of 2.
    /// </summary>
    public struct Length2 : ILength
    {
        public int Length => 2;

        public void Deserialize(IXunitSerializationInfo info) { }

        public void Serialize(IXunitSerializationInfo info) { }
    }

    /// <summary>
    /// Array size of 31.
    /// </summary>
    public struct Length31 : ILength
    {
        public int Length => 31;

        public void Deserialize(IXunitSerializationInfo info) { }

        public void Serialize(IXunitSerializationInfo info) { }
    }

    /// <summary>
    /// Array size of 32.
    /// </summary>
    public struct Length32 : ILength
    {
        public int Length => 32;

        public void Deserialize(IXunitSerializationInfo info) { }

        public void Serialize(IXunitSerializationInfo info) { }
    }

    /// <summary>
    /// Array size of 33.
    /// </summary>
    public struct Length33 : ILength
    {
        public int Length => 33;

        public void Deserialize(IXunitSerializationInfo info) { }

        public void Serialize(IXunitSerializationInfo info) { }
    }

    /// <summary>
    /// Array size of 65.
    /// </summary>
    public struct Length65 : ILength
    {
        public int Length => 65;

        public void Deserialize(IXunitSerializationInfo info) { }

        public void Serialize(IXunitSerializationInfo info) { }
    }

    /// <summary>
    /// Array size of 127.
    /// </summary>
    public struct Length127 : ILength
    {
        public int Length => 127;

        public void Deserialize(IXunitSerializationInfo info) { }

        public void Serialize(IXunitSerializationInfo info) { }
    }

    #endregion
}

#pragma warning restore CA2231 // Overload operator equals on overriding value type Equals
#pragma warning restore CA1051 // Do not declare visible instance fields
#pragma warning restore CA1815 // Override equals and operator equals on value types

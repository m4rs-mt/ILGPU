// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Runtime.CompilerServices;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an allocation operation of a new array in a particular address space.
    /// </summary>
    [ValueKind(ValueKind.Array)]
    public sealed class NewArray : ControlFlowValue
    {
        #region Nested Types

        /// <summary>
        /// An instance builder for array values.
        /// </summary>
        public struct Builder
        {
            #region Instance

            private ValueList builder;

            /// <summary>
            /// Initializes a new array value builder.
            /// </summary>
            /// <param name="irBuilder">The current IR builder.</param>
            /// <param name="location">The current location.</param>
            /// <param name="arrayType">The parent array type of this value.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Builder(IRBuilder irBuilder, Location location, ArrayType arrayType)
            {
                builder = ValueList.Create(arrayType.NumDimensions);
                IRBuilder = irBuilder;
                Location = location;
                ArrayType = arrayType;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent builder.
            /// </summary>
            public IRBuilder IRBuilder { get; }

            /// <summary>
            /// Returns the current location.
            /// </summary>
            public Location Location { get; }

            /// <summary>
            /// Returns the array type.
            /// </summary>
            public ArrayType ArrayType { get; }

            /// <summary>
            /// The number of dimensions.
            /// </summary>
            public int Count => builder.Count;

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given dimension length to the array value builder.
            /// </summary>
            /// <param name="dimension">The value to add.</param>
            public void Add(Value dimension)
            {
                Location.AssertNotNull(dimension);
                Location.Assert(Count < ArrayType.NumDimensions);
                builder.Add(dimension);
            }

            /// <summary>
            /// Constructs a new value that represents the current array value.
            /// </summary>
            /// <returns>The resulting value reference.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public NewArray Seal() =>
                IRBuilder.FinishNewArray(Location, ArrayType, ref builder);

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a array.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="arrayType">The parent array type of this array.</param>
        /// <param name="dimensions">The list of all array dimension lengths.</param>
        internal NewArray(
            in ValueInitializer initializer,
            ArrayType arrayType,
            ref ValueList dimensions)
            : base(initializer, arrayType)
        {
            Seal(ref dimensions);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Array;

        /// <summary>
        /// Returns the array type of this value.
        /// </summary>
        public new ArrayType Type => base.Type as ArrayType;

        /// <summary>
        /// Returns the array's element type.
        /// </summary>
        public TypeNode ElementType => Type.ElementType;

        /// <summary>
        /// Returns the number of array dimensions.
        /// </summary>
        public int NumDimensions => Type.NumDimensions;

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder)
        {
            var dimensions = rebuilder.Rebuild(Nodes);
            return builder.FinishNewArray(Location, Type, ref dimensions);
        }

        /// <inheritdoc/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <inheritdoc/>
        protected override string ToPrefixString() => $"array{NumDimensions}D";

        #endregion
    }

    /// <summary>
    /// Represents an abstract array value operation.
    /// </summary>
    public interface IArrayValueOperation
    {
        /// <summary>
        /// Returns the source array value.
        /// </summary>
        ValueReference ArrayValue { get; }
    }

    /// <summary>
    /// Gets the length of an array value or a particular array dimension.
    /// </summary>
    [ValueKind(ValueKind.GetArrayLength)]
    public sealed class GetArrayLength : Value, IArrayValueOperation
    {
        #region Instance

        /// <summary>
        /// Constructs a array.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="arrayValue">The parent array value.</param>
        /// <param name="dimension">The value of the dimension to get.</param>
        internal GetArrayLength(
            in ValueInitializer initializer,
            ValueReference arrayValue,
            ValueReference dimension)
            : base(
                  initializer,
                  initializer.Context.GetPrimitiveType(BasicValueType.Int32))
        {
            Seal(arrayValue, dimension);
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public override ValueKind ValueKind => ValueKind.GetArrayLength;

        /// <summary>
        /// Returns the source array value.
        /// </summary>
        public ValueReference ArrayValue => this[0];

        /// <summary>
        /// Returns the source dimension.
        /// </summary>
        public ValueReference Dimension => this[1];

        /// <summary>
        /// Returns true if this length value returns the full linear length of the array.
        /// </summary>
        public bool IsFullLength => Dimension.Resolve() is UndefinedValue;

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGetArrayLength(
                Location,
                rebuilder.Rebuild(ArrayValue),
                rebuilder.Rebuild(Dimension));

        /// <inheritdoc/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <inheritdoc/>
        protected override string ToPrefixString() =>
            IsFullLength
            ? "array.dim"
            : "array.len";

        /// <inheritdoc/>
        protected override string ToArgString() =>
            IsFullLength
            ? Dimension.ToString()
            : string.Empty;

        #endregion
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ArrayValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an array value.
    /// </summary>
    [ValueKind(ValueKind.Array)]
    public sealed class ArrayValue : ClassValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new array value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="arrayType">The associated array type.</param>
        /// <param name="extent">The array length.</param>
        internal ArrayValue(
            in ValueInitializer initializer,
            ArrayType arrayType,
            ValueReference extent)
            : base(initializer)
        {
            ArrayType = arrayType;
            Seal(extent);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Array;

        /// <summary>
        /// Returns the array type.
        /// </summary>
        public ArrayType ArrayType { get; }

        /// <summary>
        /// Returns the number of array dimensions.
        /// </summary>
        public int Dimensions => ArrayType.Dimensions;

        /// <summary>
        /// Returns the number of elements.
        /// </summary>
        public ValueReference Extent => this[0];

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            ArrayType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateArray(
                Location,
                ArrayType,
                rebuilder.Rebuild(Extent));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() =>
            "array<" + ArrayType.ElementType + ", " + Dimensions + ">";

        #endregion
    }

    /// <summary>
    /// Represents an operation on structure values.
    /// </summary>
    public abstract class ArrayOperationValue : ClassOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract structure operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        internal ArrayOperationValue(in ValueInitializer initializer)
            : base(initializer)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the array type.
        /// </summary>
        public ArrayType ArrayType => ObjectType as ArrayType;

        /// <summary>
        /// Returns the associated element type.
        /// </summary>
        public TypeNode ElementType => ArrayType.ElementType;

        /// <summary>
        /// Returns the array index.
        /// </summary>
        public ValueReference Index => this[1];

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve the operation index to a constant value.
        /// </summary>
        /// <param name="index">The resolved constant index (if any).</param>
        /// <returns>True, if a constant index could be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolveConstantIndex(out int index)
        {
            if (Index.Resolve() is PrimitiveValue primitiveValue)
            {
                index = primitiveValue.Int32Value;
                return true;
            }

            index = -1;
            return false;
        }

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{ObjectValue}[{Index}]";

        #endregion
    }

    /// <summary>
    /// Represents an operation to extract the extent from an array value.
    /// </summary>
    [ValueKind(ValueKind.GetArrayExtent)]
    public sealed class GetArrayExtent : ArrayOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new element load.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="arrayValue">The array value.</param>
        internal GetArrayExtent(
            in ValueInitializer initializer,
            ValueReference arrayValue)
            : base(initializer)
        {
            Seal(arrayValue);
        }

        #endregion

        #region Methods

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GetArrayExtent;

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.GetIndexType(
                (ObjectValue.Type as ArrayType).Dimensions);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGetArrayExtent(
                Location,
                rebuilder.Rebuild(ObjectValue));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => ObjectValue.ToString();

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "getext";

        #endregion
    }

    /// <summary>
    /// Represents an operation to load a single element from an array.
    /// </summary>
    [ValueKind(ValueKind.GetArrayElement)]
    public sealed class GetArrayElement : ArrayOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new element load.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="arrayIndex">The array index.</param>
        internal GetArrayElement(
            in ValueInitializer initializer,
            ValueReference arrayValue,
            ValueReference arrayIndex)
            : base(initializer)
        {
            Seal(arrayValue, arrayIndex);
        }

        #endregion

        #region Methods

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GetArrayElement;

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            (ObjectValue.Type as ArrayType).ElementType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGetArrayElement(
                Location,
                rebuilder.Rebuild(ObjectValue),
                rebuilder.Rebuild(Index));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "gelem";

        #endregion
    }

    /// <summary>
    /// Represents an operation to store a single value into an array.
    /// </summary>
    [ValueKind(ValueKind.SetArrayElement)]
    public sealed class SetArrayElement : ArrayOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new element store.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="arrayIndex">The array index.</param>
        /// <param name="value">The value to store.</param>
        internal SetArrayElement(
            in ValueInitializer initializer,
            ValueReference arrayValue,
            ValueReference arrayIndex,
            ValueReference value)
            : base(initializer)
        {
            Seal(arrayValue, arrayIndex, value);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.SetArrayElement;

        /// <summary>
        /// Returns the value to store.
        /// </summary>
        public ValueReference Value => this[2];

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.VoidType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateSetArrayElement(
                Location,
                rebuilder.Rebuild(ObjectValue),
                rebuilder.Rebuild(Index),
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "selem";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            base.ToArgString() + " -> " + Value;

        #endregion
    }
}

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
using System.Collections.Immutable;
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
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="arrayType">The associated array type.</param>
        /// <param name="extent">The array length.</param>
        internal ArrayValue(
            BasicBlock basicBlock,
            ArrayType arrayType,
            ValueReference extent)
            : base(basicBlock, arrayType)
        {
            ArrayType = arrayType;
            Seal(ImmutableArray.Create(extent));
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

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) => ArrayType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateArray(ArrayType, rebuilder.Rebuild(Extent));

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
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="values">All child values.</param>
        /// <param name="initialType">The initial node type.</param>
        internal ArrayOperationValue(
            BasicBlock basicBlock,
            ImmutableArray<ValueReference> values,
            TypeNode initialType)
            : base(basicBlock, values, initialType)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the array type.
        /// </summary>
        public ArrayType ArrayType => Type as ArrayType;

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
        #region Static

        /// <summary>
        /// Computes a get extent node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="arrayType">The current array type.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeNode ComputeType(
            IRContext context,
            ArrayType arrayType) =>
            context.GetIndexType(arrayType.Dimensions);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new element load.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="arrayValue">The array value.</param>
        internal GetArrayExtent(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference arrayValue)
            : base(
                basicBlock,
                ImmutableArray.Create(arrayValue),
                ComputeType(context, arrayValue.Type as ArrayType))
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GetArrayExtent;

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, ArrayType);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGetArrayExtent(rebuilder.Rebuild(ObjectValue));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

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
        #region Static

        /// <summary>
        /// Computes a get element node type.
        /// </summary>
        /// <param name="arrayValue">The current array value.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeNode ComputeType(ValueReference arrayValue) =>
            (arrayValue.Type as ArrayType).ElementType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new element load.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="arrayIndex">The array index.</param>
        internal GetArrayElement(
            BasicBlock basicBlock,
            ValueReference arrayValue,
            ValueReference arrayIndex)
            : base(
                basicBlock,
                ImmutableArray.Create(arrayValue, arrayIndex),
                ComputeType(arrayValue))
        { }

        #endregion

        #region Methods

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.GetArrayElement;

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(ObjectValue);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateGetArrayElement(
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
        #region Static

        /// <summary>
        /// Computes a set element node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeNode ComputeType(IRContext context) =>
            context.VoidType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new element store.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="arrayIndex">The array index.</param>
        /// <param name="value">The value to store.</param>
        internal SetArrayElement(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference arrayValue,
            ValueReference arrayIndex,
            ValueReference value)
            : base(
                basicBlock,
                ImmutableArray.Create(arrayValue, arrayIndex, value),
                ComputeType(context))
        { }

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

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateSetArrayElement(
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

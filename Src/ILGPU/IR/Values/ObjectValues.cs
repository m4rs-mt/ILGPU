// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ObjectValues.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an operation on object values.
    /// </summary>
    public abstract class ObjectOperationValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract object operation.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="initialType">The initial node type.</param>
        internal ObjectOperationValue(BasicBlock basicBlock, TypeNode initialType)
            : base(basicBlock, initialType)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the structure value to load from.
        /// </summary>
        public ValueReference ObjectValue => this[0];

        /// <summary>
        /// Returns the structure type.
        /// </summary>
        public ObjectType ObjectType => ObjectValue.Type as ObjectType;

        #endregion
    }

    #region Structure Operations

    /// <summary>
    /// Represents an operation on structure values.
    /// </summary>
    public abstract class StructureOperationValue : ObjectOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract structure operation.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="initialType">The initial node type.</param>
        /// <param name="fieldIndex">The structure field index.</param>
        internal StructureOperationValue(
            BasicBlock basicBlock,
            TypeNode initialType,
            int fieldIndex)
            : base(basicBlock, initialType)
        {
            FieldIndex = fieldIndex;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the structure type.
        /// </summary>
        public StructureType StructureType => ObjectType as StructureType;

        /// <summary>
        /// Returns the associated field type.
        /// </summary>
        public TypeNode FieldType => GetField.ComputeType(ObjectValue, FieldIndex);

        /// <summary>(
        /// Returns the field index.
        /// </summary>
        public int FieldIndex { get; }

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{ObjectValue}[{FieldIndex}]";

        #endregion
    }

    /// <summary>
    /// Represents an operation to load a single field from an object.
    /// </summary>
    public sealed class GetField : StructureOperationValue
    {
        #region Static

        /// <summary>
        /// Computes a get field node type.
        /// </summary>
        /// <param name="structValue">The current structure value.</param>
        /// <param name="fieldIndex">The associated field index.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeNode ComputeType(
            ValueReference structValue,
            int fieldIndex) =>
            (structValue.Type as StructureType).Fields[fieldIndex];

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new field load.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="structValue">The structure value.</param>
        /// <param name="fieldIndex">The structure field index.</param>
        internal GetField(
            BasicBlock basicBlock,
            ValueReference structValue,
            int fieldIndex)
            : base(
                  basicBlock,
                  ComputeType(structValue, fieldIndex),
                  fieldIndex)
        {
            Seal(ImmutableArray.Create(structValue));
        }

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(ObjectValue, FieldIndex);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateGetField(
                rebuilder.Rebuild(ObjectValue),
                FieldIndex);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "gfld";

        #endregion
    }

    /// <summary>
    /// Represents an operation to store a single field of an object.
    /// </summary>
    public sealed class SetField : StructureOperationValue
    {
        #region Static

        /// <summary>
        /// Computes a set field node type.
        /// </summary>
        /// <param name="structValue">The current structure value.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(ValueReference structValue) =>
            structValue.Type;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new field store.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="structValue">The structure value.</param>
        /// <param name="fieldIndex">The structure field index.</param>
        /// <param name="value">The value to store.</param>
        internal SetField(
            BasicBlock basicBlock,
            ValueReference structValue,
            int fieldIndex,
            ValueReference value)
            : base(
                  basicBlock,
                  ComputeType(structValue),
                  fieldIndex)
        {
            Seal(ImmutableArray.Create(structValue, value));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the value to store.
        /// </summary>
        public ValueReference Value => this[1];

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(ObjectValue);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateSetField(
                rebuilder.Rebuild(ObjectValue),
                FieldIndex,
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "sfld";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            base.ToArgString() + " -> " + Value;

        #endregion
    }

    #endregion

    #region Array Operations

    /// <summary>
    /// Represents an operation on structure values.
    /// </summary>
    public abstract class ArrayOperationValue : ObjectOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract structure operation.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="initialType">The initial node type.</param>
        internal ArrayOperationValue(BasicBlock basicBlock, TypeNode initialType)
            : base(basicBlock, initialType)
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
    /// Represents an operation to load a single element from an array.
    /// </summary>
    public sealed class GetElement : ArrayOperationValue
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
        internal GetElement(
            BasicBlock basicBlock,
            ValueReference arrayValue,
            ValueReference arrayIndex)
            : base(basicBlock, ComputeType(arrayValue))
        {
            Debug.Assert(
                arrayIndex.BasicValueType == BasicValueType.Int32,
                "Invalid array index");
            Seal(ImmutableArray.Create(arrayValue, arrayIndex));
        }

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(ObjectValue);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateGetElement(
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
    public sealed class SetElement : ArrayOperationValue
    {
        #region Static

        /// <summary>
        /// Computes a set element node type.
        /// </summary>
        /// <param name="arrayValue">The current array value.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeNode ComputeType(ValueReference arrayValue) =>
            arrayValue.Type;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new element store.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="arrayValue">The array value.</param>
        /// <param name="arrayIndex">The array index.</param>
        /// <param name="value">The value to store.</param>
        internal SetElement(
            BasicBlock basicBlock,
            ValueReference arrayValue,
            ValueReference arrayIndex,
            ValueReference value)
            : base(basicBlock, ComputeType(arrayValue))
        {
            Debug.Assert(
                arrayIndex.BasicValueType == BasicValueType.Int32,
                "Invalid array index");
            Seal(ImmutableArray.Create(arrayValue, arrayIndex, value));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the value to store.
        /// </summary>
        public ValueReference Value => this[2];

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(ObjectValue);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateSetElement(
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

    #endregion
}

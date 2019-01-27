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
        /// Constructs a new abstract structure operation.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="initialType">The initial node type.</param>
        /// <param name="fieldIndex">The structure field index.</param>
        internal ObjectOperationValue(
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
        /// Returns the structure value to load from.
        /// </summary>
        public ValueReference StructValue => this[0];

        /// <summary>
        /// Returns the structure type.
        /// </summary>
        public StructureType StructureType =>
            StructValue.Type as StructureType;

        /// <summary>
        /// Returns the associated field type.
        /// </summary>
        public TypeNode FieldType => GetField.ComputeType(StructValue, FieldIndex);

        /// <summary>
        /// Returns the field index.
        /// </summary>
        public int FieldIndex { get; }

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{StructValue}[{FieldIndex}]";

        #endregion
    }

    /// <summary>
    /// Represents an operation to load a single field from an object.
    /// </summary>
    public sealed class GetField : ObjectOperationValue
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
            (structValue.Type as StructureType).Children[fieldIndex];

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
            ComputeType(StructValue, FieldIndex);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateGetField(
                rebuilder.Rebuild(StructValue),
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
    public sealed class SetField : ObjectOperationValue
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
            ComputeType(StructValue);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateSetField(
                rebuilder.Rebuild(StructValue),
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
}

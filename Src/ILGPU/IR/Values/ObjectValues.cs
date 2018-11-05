// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
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

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an operation on object values.
    /// </summary>
    public abstract class ObjectOperationValue : UnifiedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract structure operation.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="fieldIndex">The structure field index.</param>
        internal ObjectOperationValue(
            ValueGeneration generation,
            int fieldIndex)
            : base(generation)
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
        public TypeNode FieldType =>
            (StructValue.Type as StructureType).Children[FieldIndex];

        /// <summary>
        /// Returns the field index.
        /// </summary>
        public int FieldIndex { get; }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (obj is ObjectOperationValue structOperation)
                return structOperation.FieldIndex == FieldIndex &&
                    base.Equals(obj);
            return false;
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ FieldIndex;
        }

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{StructValue}[{FieldIndex}]";

        #endregion
    }

    /// <summary>
    /// Represents an operation to load a single field from an object.
    /// </summary>
    public sealed class GetField : ObjectOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new field load.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="structValue">The structure value.</param>
        /// <param name="fieldIndex">The structure field index.</param>
        internal GetField(
            ValueGeneration generation,
            ValueReference structValue,
            int fieldIndex)
            : base(generation, fieldIndex)
        {
            var fieldType = (structValue.Type as StructureType).Children[fieldIndex];
            Seal(ImmutableArray.Create(structValue), fieldType);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.Type"/>
        public override TypeNode Type => StructureType.Children[FieldIndex];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateGetField(
                rebuilder.Rebuild(StructValue),
                FieldIndex);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (obj is GetField)
                return base.Equals(obj);
            return false;
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x7A43B81;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "gfld";

        #endregion
    }

    /// <summary>
    /// Represents an operation to store a single field of an object.
    /// </summary>
    public sealed class SetField : ObjectOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new field store.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="structValue">The structure value.</param>
        /// <param name="fieldIndex">The structure field index.</param>
        /// <param name="value">The value to store.</param>
        internal SetField(
            ValueGeneration generation,
            ValueReference structValue,
            int fieldIndex,
            ValueReference value)
            : base(generation, fieldIndex)
        {
            Seal(
                ImmutableArray.Create(structValue, value),
                structValue.Type);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the value to store.
        /// </summary>
        public ValueReference Value => this[1];

        /// <summary cref="Value.Type"/>
        public override TypeNode Type => StructureType;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateSetField(
                rebuilder.Rebuild(StructValue),
                FieldIndex,
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (obj is SetField)
                return base.Equals(obj);
            return false;
        }

        /// <summary cref="UnifiedValue.Equals(object)"/>
        public override int GetHashCode()
        {
            return base.GetHashCode() ^ 0x20205F8B;
        }

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "sfld";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            base.ToArgString() + " -> " + Value;

        #endregion
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: AlignValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract alignment operation value.
    /// </summary>
    public abstract class BaseAlignOperationValue : PointerValue
    {
        #region Instance

        /// <summary>
        /// Constructs an alignment operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="source">The underlying source.</param>
        /// <param name="alignmentInBytes">The alignment in bytes.</param>
        internal BaseAlignOperationValue(
            in ValueInitializer initializer,
            ValueReference source,
            ValueReference alignmentInBytes)
            : base(initializer)
        {
            Seal(source, alignmentInBytes);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the alignment in bytes.
        /// </summary>
        public ValueReference AlignmentInBytes => this[1];

        /// <summary>
        /// Returns true if the current operation works on a view.
        /// </summary>
        public bool IsViewOperation => Source.Type.IsViewType;

        /// <summary>
        /// Returns true if the current operation works on a pointer.
        /// </summary>
        public bool IsPointerOperation => Source.Type.IsPointerType;

        #endregion

        #region Methods

        /// <summary>
        /// Tries to determine an explicit alignment compile-time constant (primarily
        /// for compiler analysis purposes). If this alignment information could not be
        /// resolved, the function returns the worst-case alignment of 1.
        /// </summary>
        public int GetAlignmentConstant() =>
            TryGetAlignmentConstant(out int constant) ? constant : 1;

        /// <summary>
        /// Tries to determine a compile-time known alignment constant.
        /// </summary>
        /// <param name="alignmentConstant">
        /// The determined alignment constant (if any).
        /// </param>
        /// <returns>True, if an alignment constant could be determined.</returns>
        public bool TryGetAlignmentConstant(out int alignmentConstant)
        {
            if (AlignmentInBytes.Resolve() is PrimitiveValue primitive)
            {
                alignmentConstant = Math.Max(primitive.Int32Value, 1);
                return true;
            }
            alignmentConstant = 0;
            return false;
        }

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{base.ToArgString()}, {AlignmentInBytes}";

        #endregion
    }

    /// <summary>
    /// Aligns a pointer or a view to a specified alignment in bytes.
    /// </summary>
    [ValueKind(ValueKind.AlignTo)]
    public sealed class AlignTo : BaseAlignOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs an aligned pointer/view.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="view">The underlying view.</param>
        /// <param name="alignmentInBytes">The alignment in bytes.</param>
        internal AlignTo(
            in ValueInitializer initializer,
            ValueReference view,
            ValueReference alignmentInBytes)
            : base(initializer, view, alignmentInBytes)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// The structure type.
        /// </summary>
        public StructureType StructureType => Type.As<StructureType>(this);

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.AlignTo;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer)
        {
            var context = initializer.Context;
            if (IsViewOperation)
            {
                // The return type will be a structure type on an unaligned prefix part
                // and an aligned main view part
                var builder = context.CreateStructureType(2);
                builder.Add(Source.Type);
                builder.Add(Source.Type);
                return builder.Seal();
            }
            else
            {
                // In the case of a structure, this operation will return a single
                // pointer only
                return Source.Type;
            }
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateAlignTo(
                Location,
                rebuilder.Rebuild(Source),
                rebuilder.Rebuild(AlignmentInBytes));

        /// <summary cref="Value.GetExportData">
        protected internal override long GetExportData() => Type is AddressSpaceType ? (long)AddressSpace : -1;

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() =>
            IsViewOperation ? "alignViewTo" : "alignPtrTo";

        #endregion
    }

    /// <summary>
    /// Interprets the given pointer or view to be aligned to the given alignment in
    /// bytes.
    /// </summary>
    [ValueKind(ValueKind.AsAligned)]
    public sealed class AsAligned : BaseAlignOperationValue
    {
        #region Instance

        /// <summary>
        /// Constructs an alignment interpretation value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="view">The underlying view.</param>
        /// <param name="alignmentInBytes">The alignment in bytes.</param>
        internal AsAligned(
            in ValueInitializer initializer,
            ValueReference view,
            ValueReference alignmentInBytes)
            : base(initializer, view, alignmentInBytes)
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.AsAligned;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            Source.Type;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateAsAligned(
                Location,
                rebuilder.Rebuild(Source),
                rebuilder.Rebuild(AlignmentInBytes));

        /// <summary cref="Value.GetExportData">
        protected internal override long GetExportData() => Type is AddressSpaceType ? (long)AddressSpace : -1;

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() =>
            IsViewOperation ? "asAlignedView" : "asAlignedPtr";

        #endregion
    }
}

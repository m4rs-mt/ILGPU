// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Memory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract value with side effects.
    /// </summary>
    public abstract class MemoryValue : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new memory value.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="values">All child values.</param>
        /// <param name="initialType">The initial node type.</param>
        internal MemoryValue(
            BasicBlock basicBlock,
            ImmutableArray<ValueReference> values,
            TypeNode initialType)
            : base(basicBlock, initialType)
        {
            Seal(values);
        }

        #endregion
    }

    /// <summary>
    /// Represents an allocation operation on the stack.
    /// </summary>
    [ValueKind(ValueKind.Alloca)]
    public sealed class Alloca : MemoryValue
    {
        #region Static

        /// <summary>
        /// Computes an alloca node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="allocaType">The allocation type.</param>
        /// <param name="addressSpace">The target address space.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(
            IRContext context,
            TypeNode allocaType,
            MemoryAddressSpace addressSpace) =>
            context.CreatePointerType(
                allocaType,
                addressSpace);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new alloca node.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="arrayLength">The array length to allocate.</param>
        /// <param name="allocaType">The allocation type.</param>
        /// <param name="addressSpace">The target address space.</param>
        internal Alloca(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference arrayLength,
            TypeNode allocaType,
            MemoryAddressSpace addressSpace)
            : base(
                  basicBlock,
                  ImmutableArray.Create(arrayLength),
                  ComputeType(context, allocaType, addressSpace))
        {
            Debug.Assert(
                addressSpace == MemoryAddressSpace.Local ||
                addressSpace == MemoryAddressSpace.Shared,
                "Invalid alloca address space");

            AllocaType = allocaType;
            AddressSpace = addressSpace;

            InvalidateType();
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Alloca;

        /// <summary>
        /// Returns the allocation type.
        /// </summary>
        public TypeNode AllocaType { get; }

        /// <summary>
        /// Returns the address space of this allocation.
        /// </summary>
        public MemoryAddressSpace AddressSpace { get; }

        /// <summary>
        /// Returns the array length.
        /// </summary>
        public ValueReference ArrayLength => this[0];

        /// <summary>
        /// Returns true if this allocation is a simple allocation.
        /// </summary>
        public bool IsSimpleAllocation =>
            ArrayLength.ResolveAs<UndefinedValue>() != null;

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if this allocation is an array allocation.
        /// </summary>
        public bool IsArrayAllocation(out PrimitiveValue primitive)
        {
            primitive = ArrayLength.ResolveAs<PrimitiveValue>();
            return primitive != null;
        }

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, AllocaType, AddressSpace);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateAlloca(
                AllocaType,
                AddressSpace,
                rebuilder.Rebuild(ArrayLength));

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "alloca";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            ArrayLength.Resolve() is PrimitiveValue value
            ? $"{Type} [{value}]"
            : Type.ToString();

        #endregion
    }

    /// <summary>
    /// Represents the kind of a memory-barrier operation.
    /// </summary>
    public enum MemoryBarrierKind
    {
        /// <summary>
        /// The barrier works on the group level.
        /// </summary>
        GroupLevel,

        /// <summary>
        /// The barrier works on the device level.
        /// </summary>
        DeviceLevel,

        /// <summary>
        /// The barrier works on the system level.
        /// </summary>
        SystemLevel
    }

    /// <summary>
    /// Represents a memory barrier that hinders reordering of memory operations
    /// with side effects.
    /// </summary>
    [ValueKind(ValueKind.MemoryBarrier)]
    public sealed class MemoryBarrier : MemoryValue
    {
        #region Static

        /// <summary>
        /// Computes a barrier node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(IRContext context) =>
            context.VoidType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new memory barrier.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="kind">The barrier kind.</param>
        internal MemoryBarrier(
            IRContext context,
            BasicBlock basicBlock,
            MemoryBarrierKind kind)
            : base(
                  basicBlock,
                  ImmutableArray<ValueReference>.Empty,
                  ComputeType(context))
        {
            Kind = kind;
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.MemoryBarrier;

        /// <summary>
        /// Returns the kind of the barrier.
        /// </summary>
        public MemoryBarrierKind Kind { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateMemoryBarrier(Kind);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "memBarrier";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Kind.ToString();

        #endregion
    }

    /// <summary>
    /// Represents a load operation with side effects.
    /// </summary>
    [ValueKind(ValueKind.Load)]
    public sealed class Load : MemoryValue
    {
        #region Static

        /// <summary>
        /// Computes a load node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="sourceType">The source type.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(
            IRContext context,
            TypeNode sourceType) =>
            (sourceType as PointerType).ElementType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new load operation.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="source">The source view.</param>
        internal Load(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference source)
            : base(
                  basicBlock,
                  ImmutableArray.Create(source),
                  ComputeType(context, source.Type))
        {
            InvalidateType();
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Load;

        /// <summary>
        /// Returns the source view.
        /// </summary>
        public ValueReference Source => this[0];

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context, Source.Type);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateLoad(
                rebuilder.Rebuild(Source));

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "ld";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => Source.ToString();

        #endregion
    }

    /// <summary>
    /// Represents a store operation with side effects.
    /// </summary>
    [ValueKind(ValueKind.Store)]
    public sealed class Store : MemoryValue
    {
        #region Static

        /// <summary>
        /// Computes a store node type.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(IRContext context) =>
            context.VoidType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new store operation.
        /// </summary>
        /// <param name="context">The parent IR context.</param>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="target">The target view.</param>
        /// <param name="value">The value to store.</param>
        internal Store(
            IRContext context,
            BasicBlock basicBlock,
            ValueReference target,
            ValueReference value)
            : base(
                  basicBlock,
                  ImmutableArray.Create(target, value),
                  ComputeType(context))
        { }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Store;

        /// <summary>
        /// Returns the target view.
        /// </summary>
        public ValueReference Target => this[0];

        /// <summary>
        /// Returns the value to store.
        /// </summary>
        public ValueReference Value => this[1];

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(context);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateStore(
                rebuilder.Rebuild(Target),
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "st";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"{Target} -> {Value}";

        #endregion
    }
}

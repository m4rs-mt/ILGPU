// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Memory.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.Util;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract value operating on memory.
    /// </summary>
    public abstract class MemoryValue : SideEffectValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new memory value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        internal MemoryValue(in ValueInitializer initializer)
            : base(initializer)
        { }

        /// <summary>
        /// Constructs a new memory value.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="staticType">The static type.</param>
        internal MemoryValue(in ValueInitializer initializer, TypeNode staticType)
            : base(initializer, staticType)
        { }

        #endregion
    }

    /// <summary>
    /// Represents an allocation operation on the stack.
    /// </summary>
    [ValueKind(ValueKind.Alloca)]
    public sealed class Alloca : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new alloca node.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="arrayLength">The array length to allocate.</param>
        /// <param name="allocaType">The allocation type.</param>
        /// <param name="addressSpace">The target address space.</param>
        internal Alloca(
            in ValueInitializer initializer,
            ValueReference arrayLength,
            TypeNode allocaType,
            MemoryAddressSpace addressSpace)
            : base(initializer)
        {
            this.Assert(
                addressSpace == MemoryAddressSpace.Local ||
                addressSpace == MemoryAddressSpace.Shared);

            AllocaType = allocaType;
            AddressSpace = addressSpace;

            Seal(arrayLength);
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

        /// <summary>
        /// Returns true if this allocation is a dynamic allocation.
        /// </summary>
        public bool IsDynamicAllocation =>
            ArrayLength.ResolveAs<DynamicMemoryLengthValue>() != null;

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if this allocation is an array allocation.
        /// </summary>
        public bool IsArrayAllocation([NotNullWhen(true)] out PrimitiveValue? primitive)
        {
            primitive = ArrayLength.ResolveAs<PrimitiveValue>();
            return primitive != null;
        }

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.CreatePointerType(
                AllocaType,
                AddressSpace);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateAlloca(
                Location,
                AllocaType,
                AddressSpace,
                rebuilder.Rebuild(ArrayLength));

        /// <summary cref="Value.Serialize(IRSerializer)"/>
        protected internal override void Serialize(IRSerializer serializer) =>
            serializer.Serialize(AddressSpace);

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
        #region Instance

        /// <summary>
        /// Constructs a new memory barrier.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="kind">The barrier kind.</param>
        internal MemoryBarrier(
            in ValueInitializer initializer,
            MemoryBarrierKind kind)
            : base(initializer)
        {
            Kind = kind;
            Seal();
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

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.VoidType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateMemoryBarrier(Location, Kind);

        /// <summary cref="Value.Serialize(IRSerializer)"/>
        protected internal override void Serialize(IRSerializer serializer) =>
            serializer.Serialize(Kind);

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
        #region Instance

        /// <summary>
        /// Constructs a new load operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="source">The source view.</param>
        internal Load(
            in ValueInitializer initializer,
            ValueReference source)
            : base(initializer)
        {
            Seal(source);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Load;

        /// <summary>
        /// Returns the source view.
        /// </summary>
        public ValueReference Source => this[0];

        /// <summary>
        /// Returns the source address space this load reads from.
        /// </summary>
        public MemoryAddressSpace SourceAddressSpace =>
            Source.Type.As<AddressSpaceType>(this).AddressSpace;

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            Source.Type.AsNotNullCast<PointerType>().ElementType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateLoad(
                Location,
                rebuilder.Rebuild(Source));

        /// <summary cref="Value.Serialize(IRSerializer)"/>
        protected internal override void Serialize(IRSerializer serializer) { }

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
        #region Instance

        /// <summary>
        /// Constructs a new store operation.
        /// </summary>
        /// <param name="initializer">The value initializer.</param>
        /// <param name="target">The target view.</param>
        /// <param name="value">The value to store.</param>
        internal Store(
            in ValueInitializer initializer,
            ValueReference target,
            ValueReference value)
            : base(initializer)
        {
            Seal(target, value);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.Store;

        /// <summary>
        /// Returns the target view.
        /// </summary>
        public ValueReference Target => this[0];

        /// <summary>
        /// Returns the target address space this store writes to.
        /// </summary>
        public MemoryAddressSpace TargetAddressSpace =>
            Target.Type.As<AddressSpaceType>(this).AddressSpace;

        /// <summary>
        /// Returns the value to store.
        /// </summary>
        public ValueReference Value => this[1];

        #endregion

        #region Methods

        /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
        protected override TypeNode ComputeType(in ValueInitializer initializer) =>
            initializer.Context.VoidType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(
            IRBuilder builder,
            IRRebuilder rebuilder) =>
            builder.CreateStore(
                Location,
                rebuilder.Rebuild(Target),
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Serialize(IRSerializer)"/>
        protected internal override void Serialize(IRSerializer serializer) { }

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

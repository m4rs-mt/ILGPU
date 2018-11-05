// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Memory.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents an abstract node in the scope of a memory chain.
    /// </summary>
    public interface IMemoryChainNode
    {
        /// <summary>
        /// Returns the parent memory chain element.
        /// </summary>
        ValueReference Parent { get; }
    }

    /// <summary>
    /// Represents a reference to a memory value.
    /// </summary>
    public sealed class MemoryRef : InstantiatedValue, IMemoryChainNode
    {
        #region Static

        /// <summary>
        /// Unlinks the given node from the memory chain.
        /// </summary>
        /// <param name="memoryValue">The memory value to unlink.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Unlink(MemoryValue memoryValue)
        {
            Debug.Assert(memoryValue != null, "Invalid memory value");
            var parent = memoryValue.Parent.Resolve();
            foreach (var use in memoryValue.Uses)
            {
                if (use.Resolve() is MemoryRef parentRef)
                    parentRef.Replace(parent);
            }
        }

        /// <summary>
        /// Replaces the given memory value with a new chain.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="memoryValue">The memory value to replace.</param>
        /// <param name="chainStart">The chain start.</param>
        /// <param name="chainEnd">The chain end.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Replace(
            IRBuilder builder,
            MemoryValue memoryValue,
            MemoryRef chainStart,
            MemoryRef chainEnd)
        {
            Debug.Assert(builder != null, "Invalid builder");
            Debug.Assert(memoryValue != null, "Invalid memory value");
            Debug.Assert(chainStart != null, "Invalid chain start");
            Debug.Assert(chainEnd != null, "Invalid chain end");

            var parent = memoryValue.Parent.ResolveAs<MemoryRef>();
            var newParent = builder.CreateMemoryReference(parent.Parent);

            parent.Replace(newParent);
            chainStart.Replace(newParent);

            // Wire chain end
            foreach (var use in memoryValue.Uses)
            {
                if (use.Resolve() is MemoryRef parentRef)
                    parentRef.Replace(chainEnd);
            }
        }

        /// <summary>
        /// Returns true iff the given node is member of a memory chain.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>True, iff the given node is a member of a memory chain.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMemoryChainMember(Value node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            return node is IMemoryChainNode ||
                node.Type.IsMemoryType && (node is UndefValue || node is Parameter);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new memory reference.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="parentNode">The parent node.</param>
        /// <param name="memoryType">The memory type.</param>
        internal MemoryRef(
            ValueGeneration generation,
            ValueReference parentNode,
            MemoryType memoryType)
            : base(generation)
        {
            Debug.Assert(
                parentNode.Type.IsMemoryType ||
                parentNode.Resolve().GetType().IsSubclassOf(typeof(MemoryValue)), "Invalid parent memory value");
            Seal(ImmutableArray.Create(parentNode), memoryType);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent memory operation.
        /// </summary>
        public ValueReference Parent => this[0];

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateMemoryReference(rebuilder.Rebuild(Parent));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "mparent";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"(=> {Parent} =>) ";

        #endregion
    }

    /// <summary>
    /// Represents an abstract value with side effects.
    /// </summary>
    public abstract class MemoryValue : InstantiatedValue, IMemoryChainNode
    {
        #region Instance

        /// <summary>
        /// Constructs a new memory value.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="memoryRef">The parent memory value.</param>
        /// <param name="values">All child values.</param>
        /// <param name="type">The type of the value.</param>
        internal MemoryValue(
            ValueGeneration generation,
            ValueReference memoryRef,
            ImmutableArray<ValueReference> values,
            TypeNode type)
            : base(generation)
        {
            Debug.Assert(memoryRef.IsValid, "Invalid parent memory node");
            Debug.Assert(memoryRef.Type.IsMemoryType, "Invalid parent memory type");
            Seal(ImmutableArray.Create(memoryRef).AddRange(values), type);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent memory operation.
        /// </summary>
        public ValueReference Parent => this[0];

        #endregion

        #region Object

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => $"(=> {Parent} =>) ";

        #endregion
    }

    /// <summary>
    /// Represents an allocation operation on the stack.
    /// </summary>
    public sealed class Alloca : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new alloca node.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="memoryRef">The parent memory value.</param>
        /// <param name="arrayLength">The array length to allocate.</param>
        /// <param name="type">The allocation type.</param>
        /// <param name="addressSpace">The target address space.</param>
        internal Alloca(
            ValueGeneration generation,
            ValueReference memoryRef,
            ValueReference arrayLength,
            TypeNode type,
            MemoryAddressSpace addressSpace)
            : base(
                  generation,
                  memoryRef,
                  ImmutableArray.Create(arrayLength),
                  type)
        {
            Debug.Assert(
                type.IsPointerType,
                "Invalid pointer type");
            Debug.Assert(
                arrayLength.Resolve().IsInstantiatedConstant(),
                "Invalid array length to allocate");
            Debug.Assert(
                addressSpace == MemoryAddressSpace.Local ||
                addressSpace == MemoryAddressSpace.Shared,
                "Invalid alloca address space");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the allocation type.
        /// </summary>
        public TypeNode AllocaType => (Type as PointerType).ElementType;

        /// <summary>
        /// Returns the array length.
        /// </summary>
        public ValueReference ArrayLength => this[1];

        /// <summary>
        /// Returns the address space of this allocation.
        /// </summary>
        public MemoryAddressSpace AddressSpace => (Type as PointerType).AddressSpace;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateAlloca(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                rebuilder.Rebuild(ArrayLength),
                rebuilder.Rebuild(AllocaType),
                AddressSpace);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "alloca";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{base.ToArgString()}{Type.ToString()} [{ArrayLength.Resolve().ToString()}]";

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
    public sealed class MemoryBarrier : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new memory barrier.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="memoryRef">The parent memory value.</param>
        /// <param name="kind">The barrier kind.</param>
        /// <param name="voidType">The void type.</param>
        internal MemoryBarrier(
            ValueGeneration generation,
            ValueReference memoryRef,
            MemoryBarrierKind kind,
            VoidType voidType)
            : base(
                  generation,
                  memoryRef,
                  ImmutableArray<ValueReference>.Empty,
                  voidType)
        {
            Kind = kind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the kind of the barrier.
        /// </summary>
        public MemoryBarrierKind Kind { get; }

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateMemoryBarrier(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                Kind);

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

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
    public sealed class Load : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new load operation.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="memoryRef">The parent memory value.</param>
        /// <param name="source">The source view.</param>
        internal Load(
            ValueGeneration generation,
            ValueReference memoryRef,
            ValueReference source)
            : base(
                  generation,
                  memoryRef,
                  ImmutableArray.Create(source),
                  (source.Type as PointerType).ElementType)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the source view.
        /// </summary>
        public ValueReference Source => this[1];

        /// <summary cref="Value.Type"/>
        public override TypeNode Type => (Source.Type as PointerType).ElementType;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateLoad(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                rebuilder.Rebuild(Source));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "ld";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() => base.ToArgString() + Source;

        #endregion
    }

    /// <summary>
    /// Represents a store operation with side effects.
    /// </summary>
    public sealed class Store : MemoryValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new store operation.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="memoryRef">The parent memory value.</param>
        /// <param name="target">The target view.</param>
        /// <param name="value">The value to store.</param>
        internal Store(
            ValueGeneration generation,
            ValueReference memoryRef,
            ValueReference target,
            ValueReference value)
            : base(
                generation,
                memoryRef,
                ImmutableArray.Create(target, value),
                target.Type)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target view.
        /// </summary>
        public ValueReference Target => this[1];

        /// <summary>
        /// Returns the value to store.
        /// </summary>
        public ValueReference Value => this[2];

        /// <summary cref="Value.Type"/>
        public override TypeNode Type => Target.Type;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder) =>
            builder.CreateStore(
                rebuilder.RebuildAs<MemoryRef>(Parent),
                rebuilder.Rebuild(Target),
                rebuilder.Rebuild(Value));

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "st";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            $"{base.ToArgString()}{Target} -> {Value}";

        #endregion
    }
}

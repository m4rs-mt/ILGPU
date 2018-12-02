// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: RegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a generic register allocator.
    /// </summary>
    /// <typeparam name="TKind">The register kind.</typeparam>
    /// <remarks>The members of this class are not thread safe.</remarks>
    abstract class RegisterAllocator<TKind>
        where TKind : struct
    {
        #region Nested Types

        public interface IRegisterHandler
        {
            void Handle(int index, PrimitiveRegister register);
        }

        /// <summary>
        /// Represents an abstract register
        /// </summary>
        public abstract class Register
        {
            /// <summary>
            /// Constructs a new abstract register.
            /// </summary>
            protected Register() { }

            /// <summary>
            /// Returns true if this register is a primitive register.
            /// </summary>
            public bool IsPrimitive => this is PrimitiveRegister;

            /// <summary>
            /// Returns true if this register is a compound register.
            /// </summary>
            public bool IsCompound => this is CompoundRegister;
        }

        public sealed class PrimitiveRegister : Register
        {
            /// <summary>
            /// Constructs a new primitive register.
            /// </summary>
            /// <param name="kind">The actual register kind.</param>
            /// <param name="registerValue">The associated register value.</param>
            internal PrimitiveRegister(TKind kind, int registerValue)
            {
                Kind = kind;
                RegisterValue = registerValue;
            }

            /// <summary>
            /// Returns the actual register kind.
            /// </summary>
            public TKind Kind { get; }

            /// <summary>
            /// Returns the register index value.
            /// </summary>
            public int RegisterValue { get; }

            public override string ToString() =>
                $"Register {Kind}, {RegisterValue}";
        }

        /// <summary>
        /// Represents a compound register of a complex type.
        /// </summary>
        public abstract class CompoundRegister : Register
        {
            #region Instance

            /// <summary>
            /// Constructs a new compound register.
            /// </summary>
            /// <param name="typeNode">The underlying type node.</param>
            /// <param name="registers">The child registers.</param>
            internal CompoundRegister(TypeNode typeNode, ImmutableArray<Register> registers)
            {
                Type = typeNode;
                Children = registers;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the underlying type.k
            /// </summary>
            public TypeNode Type { get; }

            /// <summary>
            /// Returns all child registers.
            /// </summary>
            public ImmutableArray<Register> Children { get; }

            /// <summary>
            /// Returns the number of child registers.
            /// </summary>
            public int NumChildren => Children.Length;

            #endregion
        }

        /// <summary>
        /// Represents a compound register of a structure type.
        /// </summary>
        public sealed class StructureRegister : CompoundRegister
        {
            #region Instance

            /// <summary>
            /// Constructs a new structure register.
            /// </summary>
            /// <param name="structureType">The structure type.</param>
            /// <param name="registers">The child registers.</param>
            internal StructureRegister(
                StructureType structureType,
                ImmutableArray<Register> registers)
                : base(structureType, registers)
            { }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the underlying structure type.
            /// </summary>
            public StructureType StructureType => Type as StructureType;

            #endregion
        }

        /// <summary>
        /// Represents a compound register of a view type.
        /// </summary>
        public abstract class ViewRegister : Register
        {
            #region Instance

            /// <summary>
            /// Constructs a new view register.
            /// </summary>
            /// <param name="viewType">The view type.</param>
            internal ViewRegister(ViewType viewType)
            {
                ViewType = viewType;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the underlying view type.
            /// </summary>
            public ViewType ViewType { get; }

            #endregion
        }

        /// <summary>
        /// Represents a register mapping entry.
        /// </summary>
        private readonly struct RegisterEntry
        {
            /// <summary>
            /// Constructs a new mapping entry.
            /// </summary>
            /// <param name="register">The register.</param>
            /// <param name="node">The node.</param>
            public RegisterEntry(Register register, Value node)
            {
                Register = register;
                Node = node;
            }

            /// <summary>
            /// Returns the associated register.
            /// </summary>
            public Register Register { get; }

            /// <summary>
            /// Returns the associated value.
            /// </summary>
            public Value Node { get; }
        }

        #endregion

        #region Instance

        private readonly Dictionary<Value, RegisterEntry> registerLookup =
            new Dictionary<Value, RegisterEntry>();
        private readonly Dictionary<Value, Value> aliases =
            new Dictionary<Value, Value>();

        /// <summary>
        /// Constructs a new register allocator.
        /// </summary>
        /// <param name="abi">The underlying ABI.</param>
        public RegisterAllocator(ABI abi)
        {
            Debug.Assert(abi != null, "Invalid ABI");
            ABI = abi;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying ABI.
        /// </summary>
        public ABI ABI { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Converts the given type to a register kind.
        /// </summary>
        /// <param name="type">The type to convert to.</param>
        /// <returns>The resolved register kind.</returns>
        protected abstract TKind ConvertTypeToKind(TypeNode type);

        /// <summary>
        /// Allocates a new primitive register of the given kind.
        /// </summary>
        /// <param name="kind">The register kind to allocate.</param>
        /// <returns>The allocated register.</returns>
        public abstract PrimitiveRegister AllocateRegister(TKind kind);

        /// <summary>
        /// Frees the given register.
        /// </summary>
        /// <param name="register">The register to free.</param>
        public abstract void FreeRegister(PrimitiveRegister register);

        /// <summary>
        /// Allocates a new register of a view type.
        /// </summary>
        /// <param name="viewType">The view type to allocate.</param>
        /// <returns>The allocated register.</returns>
        public abstract Register AllocateViewRegister(ViewType viewType);

        /// <summary>
        /// Allocates a new register of a view type.
        /// </summary>
        /// <param name="viewType">The view type to allocate.</param>
        /// <returns>The allocated register.</returns>
        public T AllocateViewRegisterAs<T>(ViewType viewType)
            where T : Register
        {
            var result = AllocateViewRegister(viewType) as T;
            Debug.Assert(result != null, "Invalid view register");
            return result;
        }

        /// <summary>
        /// Frees the given view register.
        /// </summary>
        /// <param name="register">The view register to free.</param>
        public abstract void FreeViewRegister(ViewRegister register);

        /// <summary>
        /// Allocates a specific register kind for the given node.
        /// </summary>
        /// <param name="node">The node to allocate the register for.</param>
        /// <param name="kind">The register kind to allocate.</param>
        /// <returns>The allocated register.</returns>
        public PrimitiveRegister Allocate(Value node, TKind kind)
        {
            Debug.Assert(node != null, "Invalid node");

            if (aliases.TryGetValue(node, out Value alias))
                node = alias;
            if (!registerLookup.TryGetValue(node, out RegisterEntry entry))
            {
                var targetRegister = AllocateRegister(kind);
                entry = new RegisterEntry(targetRegister, node);
                registerLookup.Add(node, entry);
            }
            var result = entry.Register as PrimitiveRegister;
            Debug.Assert(result != null, "Invalid primitive register");
            return result;
        }

        /// <summary>
        /// Allocates a specific register kind for the given node.
        /// </summary>
        /// <param name="node">The node to allocate the register for.</param>
        /// <returns>The allocated register.</returns>
        public Register Allocate(Value node)
        {
            Debug.Assert(node != null, "Invalid node");
            if (aliases.TryGetValue(node, out Value alias))
                node = alias;
            if (!registerLookup.TryGetValue(node, out RegisterEntry entry))
            {
                var targetRegister = AllocateType(node.Type);
                entry = new RegisterEntry(targetRegister, node);
                registerLookup.Add(node, entry);
            }
            return entry.Register;
        }

        /// <summary>
        /// Binds the given value to the target register.
        /// </summary>
        /// <param name="node">The node to bind.</param>
        /// <param name="targetRegister">The target register to bind to.</param>
        public void Bind(Value node, Register targetRegister)
        {
            registerLookup[node] = new RegisterEntry(
                targetRegister,
                node);
        }

        /// <summary>
        /// Allocates a new register recursively
        /// </summary>
        /// <param name="typeNode">The node type to allocate.</param>
        public Register AllocateType(TypeNode typeNode)
        {
            switch (typeNode)
            {
                case PrimitiveType primitiveType:
                    var primitiveRegisterKind = ConvertTypeToKind(primitiveType);
                    return AllocateRegister(primitiveRegisterKind);
                case StructureType structureType:
                    var childRegisters = ImmutableArray.CreateBuilder<Register>(
                        structureType.NumChildren);
                    for (int i = 0, e = structureType.NumChildren; i < e; ++i)
                        childRegisters.Add(AllocateType(structureType.Children[i]));
                    return new StructureRegister(structureType, childRegisters.MoveToImmutable());
                case ViewType viewType:
                    return AllocateViewRegister(viewType);
                case PointerType _:
                case StringType _:
                    return AllocateType(ABI.PointerType);
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Regisers a register alias.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="alias">The alias node.</param>
        public void Alias(Value node, Value alias)
        {
            Debug.Assert(node != null, "Invalid node");
            Debug.Assert(alias != null, "Invalid alias");
            if (aliases.TryGetValue(alias, out Value otherAlias))
                alias = otherAlias;
            aliases[node] = alias;
        }

        /// <summary>
        /// Loads the allocated register of the given node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>The allocated register.</returns>
        public T LoadAs<T>(Value node)
            where T : Register
        {
            var result = Load(node) as T;
            Debug.Assert(result != null, "Invalid register loading operation");
            return result;
        }

        /// <summary>
        /// Loads the allocated register of the given node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>The allocated register.</returns>
        public Register Load(Value node)
        {
            Debug.Assert(node != null, "Invalid node");
            if (aliases.TryGetValue(node, out Value alias))
                node = alias;
            if (!registerLookup.TryGetValue(node, out RegisterEntry entry))
                throw new InvalidCodeGenerationException();
            return entry.Register;
        }

        /// <summary>
        /// Loads the allocated primitive register of the given node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>The allocated register.</returns>
        public PrimitiveRegister LoadPrimitive(Value node)
        {
            var result = Load(node);
            Debug.Assert(result != null, "Invalid primitive register");
            return result as PrimitiveRegister;
        }

        /// <summary>
        /// Frees the given node.
        /// </summary>
        /// <param name="node">The node to free.</param>
        public void Free(Value node)
        {
            Debug.Assert(node != null, "Invalid node");
            FreeRecursive(registerLookup[node].Register);
            registerLookup.Remove(node);
        }

        /// <summary>
        /// Frees the given register recursively.
        /// </summary>
        /// <param name="register">The register to free.</param>
        private void FreeRecursive(Register register)
        {
            switch (register)
            {
                case PrimitiveRegister primitiveRegister:
                    FreeRegister(primitiveRegister);
                    break;
                case CompoundRegister compoundRegister:
                    foreach (var child in compoundRegister.Children)
                        FreeRecursive(child);
                    break;
                case ViewRegister viewRegister:
                    FreeViewRegister(viewRegister);
                    break;
                default:
                    throw new InvalidCodeGenerationException();
            }
        }

        #endregion
    }
}

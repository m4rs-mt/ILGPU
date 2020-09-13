// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: RegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a generic register allocator.
    /// </summary>
    /// <typeparam name="TKind">The register kind.</typeparam>
    /// <remarks>The members of this class are not thread safe.</remarks>
    public abstract class RegisterAllocator<TKind>
        where TKind : struct
    {
        #region Nested Types

        /// <summary>
        /// Describes allocation information of a single primitive register.
        /// </summary>
        public readonly struct RegisterDescription
        {
            /// <summary>
            /// Constructs a new register description.
            /// </summary>
            /// <param name="basicValueType">The basic value type.</param>
            /// <param name="kind">The register kind.</param>
            public RegisterDescription(BasicValueType basicValueType, TKind kind)
            {
                BasicValueType = basicValueType;
                Kind = kind;
            }

            /// <summary>
            /// Returns the associated basic value type.
            /// </summary>
            public BasicValueType BasicValueType { get; }

            /// <summary>
            /// Returns the associated register kind.
            /// </summary>
            public TKind Kind { get; }
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

        /// <summary>
        /// Represents a primitive register that might consume up to one hardware
        /// register.
        /// </summary>
        public abstract class PrimitiveRegister : Register
        {
            /// <summary>
            /// Constructs a new constant register.
            /// </summary>
            /// <param name="description">The current register description.</param>
            protected PrimitiveRegister(RegisterDescription description)
            {
                Description = description;
            }

            /// <summary>
            /// Returns the associated register description.
            /// </summary>
            public RegisterDescription Description { get; }

            /// <summary>
            /// Returns the associated basic value type.
            /// </summary>
            public BasicValueType BasicValueType => Description.BasicValueType;

            /// <summary>
            /// Returns the actual register kind.
            /// </summary>
            public TKind Kind => Description.Kind;
        }

        /// <summary>
        /// A primitive register with a constant value.
        /// </summary>
        public sealed class ConstantRegister : PrimitiveRegister
        {
            /// <summary>
            /// Constructs a new constant register.
            /// </summary>
            /// <param name="description">The current register description.</param>
            /// <param name="value">The primitive value.</param>
            public ConstantRegister(
                RegisterDescription description,
                PrimitiveValue value)
                : base(description)
            {
                Value = value;
            }

            /// <summary>
            /// Returns the associated value.
            /// </summary>
            public PrimitiveValue Value { get; }

            /// <summary>
            /// Returns the string representation of the current register.
            /// </summary>
            /// <returns>The string representation of the current register.</returns>
            public override string ToString() => $"Register {Kind} = {Value}";
        }

        /// <summary>
        /// Represents a primitive register that represents an actual hardware register.
        /// </summary>
        public sealed class HardwareRegister : PrimitiveRegister
        {
            /// <summary>
            /// Constructs a new hardware register.
            /// </summary>
            /// <param name="description">The current register description.</param>
            /// <param name="registerValue">The associated register value.</param>
            internal HardwareRegister(
                RegisterDescription description,
                int registerValue)
                : base(description)
            {
                RegisterValue = registerValue;
            }

            /// <summary>
            /// Returns the register index value.
            /// </summary>
            public int RegisterValue { get; }

            /// <summary>
            /// Returns the string representation of the current register.
            /// </summary>
            /// <returns>The string representation of the current register.</returns>
            public override string ToString() => $"Register {Kind}, {RegisterValue}";
        }

        /// <summary>
        /// Represents a compound register of a complex type.
        /// </summary>
        public sealed class CompoundRegister : Register
        {
            #region Instance

            /// <summary>
            /// Constructs a new compound register.
            /// </summary>
            /// <param name="type">The underlying type node.</param>
            /// <param name="registers">The child registers.</param>
            internal CompoundRegister(
                StructureType type,
                ImmutableArray<Register> registers)
            {
                Type = type;
                Children = registers;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the underlying type.
            /// </summary>
            public StructureType Type { get; }

            /// <summary>
            /// Returns all child registers.
            /// </summary>
            public ImmutableArray<Register> Children { get; }

            /// <summary>
            /// Returns the number of child registers.
            /// </summary>
            public int NumChildren => Children.Length;

            #endregion

            #region Methods

            /// <summary>
            /// Slices a subset of registers out of this compound register.
            /// </summary>
            /// <typeparam name="T">The target register type.</typeparam>
            /// <param name="index">The start index.</param>
            /// <param name="count">The number of registers to slice.</param>
            /// <returns>The sliced register array.</returns>
            public T[] SliceAs<T>(int index, int count)
                where T : Register
            {
                var result = new T[count];
                for (int i = 0; i < count; ++i)
                    result[i] = (T)Children[index + i];
                return result;
            }

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
        /// <param name="backend">The underlying backend.</param>
        protected RegisterAllocator(Backend backend)
        {
            Backend = backend;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying ABI.
        /// </summary>
        public Backend Backend { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Resolves a register description for the given type.
        /// </summary>
        /// <param name="type">The type to convert to.</param>
        /// <returns>The resolved register description.</returns>
        protected abstract RegisterDescription ResolveRegisterDescription(TypeNode type);

        /// <summary>
        /// Allocates a new hardware register of the given kind.
        /// </summary>
        /// <param name="description">
        /// The register description used for allocation.
        /// </param>
        /// <returns>The allocated register.</returns>
        public abstract HardwareRegister AllocateRegister(
            RegisterDescription description);

        /// <summary>
        /// Frees the given register.
        /// </summary>
        /// <param name="hardwareRegister">The register to free.</param>
        public abstract void FreeRegister(HardwareRegister hardwareRegister);

        /// <summary>
        /// Allocates a specific register kind for the given node.
        /// </summary>
        /// <param name="node">The node to allocate the register for.</param>
        /// <param name="description">The register description to allocate.</param>
        /// <returns>The allocated register.</returns>
        public HardwareRegister Allocate(Value node, RegisterDescription description)
        {
            node.AssertNotNull(node);

            if (aliases.TryGetValue(node, out Value alias))
                node = alias;
            if (!registerLookup.TryGetValue(node, out RegisterEntry entry))
            {
                var targetRegister = AllocateRegister(description);
                entry = new RegisterEntry(targetRegister, node);
                registerLookup.Add(node, entry);
            }
            var result = entry.Register as HardwareRegister;
            node.AssertNotNull(result);
            return result;
        }

        /// <summary>
        /// Allocates a specific register kind for the given node.
        /// </summary>
        /// <param name="node">The node to allocate the register for.</param>
        /// <returns>The allocated register.</returns>
        public HardwareRegister AllocateHardware(Value node)
        {
            var description = ResolveRegisterDescription(node.Type);
            return Allocate(node, description);
        }

        /// <summary>
        /// Allocates a specific register kind for the given node.
        /// </summary>
        /// <param name="node">The node to allocate the register for.</param>
        /// <returns>The allocated register.</returns>
        public Register Allocate(Value node)
        {
            node.AssertNotNull(node);
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
        public void Bind(Value node, Register targetRegister) =>
            registerLookup[node] = new RegisterEntry(
                targetRegister,
                node);

        /// <summary>
        /// Allocates a new register recursively
        /// </summary>
        /// <param name="typeNode">The node type to allocate.</param>
        public Register AllocateType(TypeNode typeNode)
        {
            switch (typeNode)
            {
                case PrimitiveType primitiveType:
                    var primitiveRegisterKind =
                        ResolveRegisterDescription(primitiveType);
                    return AllocateRegister(primitiveRegisterKind);
                case StructureType structureType:
                    var childRegisters = ImmutableArray.CreateBuilder<Register>(
                        structureType.NumFields);
                    for (int i = 0, e = structureType.NumFields; i < e; ++i)
                        childRegisters.Add(AllocateType(structureType.Fields[i]));
                    return new CompoundRegister(
                        structureType,
                        childRegisters.MoveToImmutable());
                case PointerType _:
                case StringType _:
                    return AllocateType(Backend.PointerType);
                case PaddingType paddingType:
                    var paddingRegisterKind =
                        ResolveRegisterDescription(paddingType.PrimitiveType);
                    return AllocateRegister(paddingRegisterKind);
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Registers a register alias.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="aliasNode">The alias node.</param>
        public void Alias(Value node, Value aliasNode)
        {
            node.AssertNotNull(node);
            node.AssertNotNull(aliasNode);
            if (aliases.TryGetValue(aliasNode, out Value otherAlias))
                aliasNode = otherAlias;
            aliases[node] = aliasNode;
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
            node.AssertNotNull(result);
            return result;
        }

        /// <summary>
        /// Loads the allocated register of the given node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>The allocated register.</returns>
        public Register Load(Value node)
        {
            node.AssertNotNull(node);
            if (aliases.TryGetValue(node, out Value alias))
                node = alias;
            return registerLookup.TryGetValue(node, out RegisterEntry entry)
                ? entry.Register
                : throw new InvalidCodeGenerationException();
        }

        /// <summary>
        /// Loads the allocated primitive register of the given node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>The allocated register.</returns>
        public PrimitiveRegister LoadPrimitive(Value node)
        {
            var result = Load(node);
            node.AssertNotNull(result);
            return result as PrimitiveRegister;
        }

        /// <summary>
        /// Loads the allocated primitive register of the given node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>The allocated register.</returns>
        public HardwareRegister LoadHardware(Value node)
        {
            var result = Load(node);
            node.AssertNotNull(result);
            return result as HardwareRegister;
        }

        /// <summary>
        /// Frees the given node.
        /// </summary>
        /// <param name="node">The node to free.</param>
        public void Free(Value node)
        {
            node.AssertNotNull(node);
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
                case HardwareRegister hardwareRegister:
                    FreeRegister(hardwareRegister);
                    break;
                case CompoundRegister compoundRegister:
                    foreach (var child in compoundRegister.Children)
                        FreeRecursive(child);
                    break;
            }
        }

        #endregion
    }
}

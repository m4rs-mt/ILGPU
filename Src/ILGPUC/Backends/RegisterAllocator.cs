// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: RegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Types;
using ILGPUC.IR.Values;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPUC.Backends;

/// <summary>
/// Represents a generic register allocator.
/// </summary>
/// <typeparam name="TKind">The register kind.</typeparam>
/// <remarks>The members of this class are not thread safe.</remarks>
abstract class RegisterAllocator<TKind>
    where TKind : Enum
{
    #region Nested Types

    /// <summary>
    /// Describes allocation information of a single primitive register.
    /// </summary>
    /// <param name="Type">The type.</param>
    /// <param name="BasicValueType">
    /// The representative basic value type.
    /// </param>
    /// <param name="Kind">The register kind.</param>
    /// <remarks>
    /// Note that the basic value type can differ from type.BasicValueType.
    /// This is due the fact that more advanced types like views or pointers
    /// can be represented by a single platform specific integer register,
    /// for instance. This also holds true for strings or reference types.
    /// </remarks>
    internal readonly record struct RegisterDescription(
        TypeNode Type,
        BasicValueType BasicValueType,
        TKind Kind)
    {
        /// <summary>
        /// Creates a new primitive register description based on the given type.
        /// </summary>
        /// <param name="primitiveType">The primitive type to store.</param>
        /// <param name="kind">The register kind to use.</param>
        /// <returns>The created register description.</returns>
        public static RegisterDescription Create(
            PrimitiveType primitiveType,
            TKind kind) =>
            new(primitiveType, primitiveType.BasicValueType, kind);

        /// <summary>
        /// Creates a new advanced register description based on the given type.
        /// </summary>
        /// <param name="typeNode">The underlying type to store.</param>
        /// <param name="basicValueType">
        /// The base value type of the stored register.
        ///</param>
        /// <param name="kind">The register kind to use.</param>
        /// <returns>The created register description.</returns>
        public static RegisterDescription Create(
            TypeNode typeNode,
            BasicValueType basicValueType,
            TKind kind) =>
            new(typeNode, basicValueType, kind);
    }

    /// <summary>
    /// Represents an abstract register
    /// </summary>
    internal abstract class Register(TypeNode type, BasicValueType basicValueType) :
        ILocation
    {
        /// <summary>
        /// Returns the associated register type.
        /// </summary>
        public TypeNode Type { get; } = type;

        /// <summary>
        /// Returns the associated basic value type.
        /// </summary>
        public BasicValueType BasicValueType { get; } = basicValueType;

        /// <summary>
        /// Returns true if this register is a primitive register.
        /// </summary>
        public virtual bool IsPrimitive => false;

        /// <summary>
        /// Returns true if this register is a compound register.
        /// </summary>
        public virtual bool IsCompound => false;

        /// <summary>
        /// Returns the input error messages for assertion purposes.
        /// </summary>
        public string FormatErrorMessage(string message) => message;
    }

    /// <summary>
    /// Represents a primitive register that might consume up to one hardware
    /// register.
    /// </summary>
    /// <param name="description">The current register description.</param>
    internal abstract class PrimitiveRegister(in RegisterDescription description) :
        Register(description.Type, description.BasicValueType)
    {
        /// <inheritdoc/>
        public override bool IsPrimitive => true;

        /// <summary>
        /// Returns the actual register kind.
        /// </summary>
        public TKind Kind { get; } = description.Kind;

        /// <summary>
        /// Returns the associated register description.
        /// </summary>
        public RegisterDescription Description =>
            RegisterDescription.Create(Type, BasicValueType, Kind);
    }

    /// <summary>
    /// A primitive register with a constant value.
    /// </summary>
    /// <param name="description">The current register description.</param>
    /// <param name="value">The primitive value.</param>
    internal sealed class ConstantRegister(
        in RegisterDescription description,
        PrimitiveValue value) : PrimitiveRegister(description)
    {
        /// <summary>
        /// Returns the associated value.
        /// </summary>
        public PrimitiveValue Value { get; } = value;

        /// <summary>
        /// Returns the string representation of the current register.
        /// </summary>
        /// <returns>The string representation of the current register.</returns>
        public override string ToString() => $"Register {Kind} = {Value}";
    }

    /// <summary>
    /// Represents a primitive register that represents an actual hardware register.
    /// </summary>
    /// <param name="description">The current register description.</param>
    /// <param name="registerValue">The associated register value.</param>
    internal sealed class HardwareRegister(
        in RegisterDescription description,
        int registerValue) : PrimitiveRegister(description)
    {
        /// <summary>
        /// Returns the register index value.
        /// </summary>
        public int RegisterValue { get; } = registerValue;

        /// <summary>
        /// Returns the string representation of the current register.
        /// </summary>
        /// <returns>The string representation of the current register.</returns>
        public override string ToString() => $"Register {Kind}, {RegisterValue}";
    }

    /// <summary>
    /// Represents a compound register of a complex type.
    /// </summary>
    /// <param name="type">The underlying type node.</param>
    /// <param name="registers">The child registers.</param>
    internal sealed class CompoundRegister(
        StructureType type,
        ImmutableArray<Register> registers) : Register(type, BasicValueType.None)
    {
        /// <inheritdoc/>
        public override bool IsCompound => true;

        /// <summary>
        /// Returns the underlying type.
        /// </summary>
        public new StructureType Type => base.Type.AsNotNullCast<StructureType>();

        /// <summary>
        /// Returns all child registers.
        /// </summary>
        public ImmutableArray<Register> Children { get; } = registers;

        /// <summary>
        /// Returns the number of child registers.
        /// </summary>
        public int NumChildren => Children.Length;

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

    private readonly Dictionary<Value, RegisterEntry> _registerLookup = [];
    private readonly Dictionary<Value, Value> _aliases = [];

    #endregion

    #region Methods

    /// <summary>
    /// Returns the primitive type for the given basic value type.
    /// </summary>
    /// <param name="basicValueType">The source basic value type.</param>
    /// <returns>The determined primitive type.</returns>
    protected abstract PrimitiveType GetPrimitiveType(BasicValueType basicValueType);

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
    /// Allocates a new hardware register of the given kind.
    /// </summary>
    /// <param name="basicValueType">The source type.</param>
    /// <param name="kind">The register kind.</param>
    /// <returns>The allocated register.</returns>
    public HardwareRegister AllocateRegister(
        BasicValueType basicValueType,
        TKind kind) =>
        AllocateRegister(
            RegisterDescription.Create(
                GetPrimitiveType(basicValueType),
                kind));

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

        if (_aliases.TryGetValue(node, out Value? alias))
            node = alias;
        if (!_registerLookup.TryGetValue(node, out RegisterEntry entry))
        {
            var targetRegister = AllocateRegister(description);
            entry = new RegisterEntry(targetRegister, node);
            _registerLookup.Add(node, entry);
        }
        var result = entry.Register.AsNotNullCast<HardwareRegister>();
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
        if (_aliases.TryGetValue(node, out Value? alias))
            node = alias;
        if (!_registerLookup.TryGetValue(node, out RegisterEntry entry))
        {
            var targetRegister = AllocateType(node.Type);
            entry = new RegisterEntry(targetRegister, node);
            _registerLookup.Add(node, entry);
        }
        return entry.Register;
    }

    /// <summary>
    /// Binds the given value to the target register.
    /// </summary>
    /// <param name="node">The node to bind.</param>
    /// <param name="targetRegister">The target register to bind to.</param>
    public void Bind(Value node, Register targetRegister) =>
        _registerLookup[node] = new RegisterEntry(
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
            case PrimitiveType _:
            case PointerType _:
            case StringType _:
                return AllocateRegister(
                    ResolveRegisterDescription(typeNode));
            case StructureType structureType:
                var childRegisters = ImmutableArray.CreateBuilder<Register>(
                    structureType.NumFields);
                for (int i = 0, e = structureType.NumFields; i < e; ++i)
                    childRegisters.Add(AllocateType(structureType.Fields[i]));
                return new CompoundRegister(
                    structureType,
                    childRegisters.MoveToImmutable());
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
        if (_aliases.TryGetValue(aliasNode, out Value? otherAlias))
            aliasNode = otherAlias;
        _aliases[node] = aliasNode;
    }

    /// <summary>
    /// Loads the allocated register of the given node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The allocated register.</returns>
    public T LoadAs<T>(Value node)
        where T : Register
    {
        var result = Load(node).AsNotNullCast<T>();
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
        if (_aliases.TryGetValue(node, out Value? alias))
            node = alias;
        return _registerLookup.TryGetValue(node, out RegisterEntry entry)
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
        return result.AsNotNullCast<PrimitiveRegister>();
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
        return result.AsNotNullCast<HardwareRegister>();
    }

    /// <summary>
    /// Frees the given node.
    /// </summary>
    /// <param name="node">The node to free.</param>
    public void Free(Value node)
    {
        node.AssertNotNull(node);
        Free(_registerLookup[node].Register);
        _registerLookup.Remove(node);
    }

    /// <summary>
    /// Frees the given register recursively.
    /// </summary>
    /// <param name="register">The register to free.</param>
    public void Free(Register register)
    {
        switch (register)
        {
            case HardwareRegister hardwareRegister:
                FreeRegister(hardwareRegister);
                break;
            case CompoundRegister compoundRegister:
                foreach (var child in compoundRegister.Children)
                    Free(child);
                break;
        }
    }

    #endregion
}

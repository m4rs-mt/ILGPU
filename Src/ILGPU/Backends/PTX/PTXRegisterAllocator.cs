// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXRegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.PointerViews;
using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents the register kind of a PTX register.
    /// </summary>
    enum PTXRegisterKind
    {
        Predicate,

        Int16,
        Int32,
        Int64,

        Float32,
        Float64,

        Ctaid,
        Tid,

        NctaId,
        NtId,

        LaneId,
    }

    /// <summary>
    /// Represents a specialized PTX register allocator.
    /// </summary>
    class PTXRegisterAllocator : ViewRegisterAllocator<PTXRegisterKind>
    {
        #region Constants

        /// <summary>
        /// The number of possible register types.
        /// </summary>
        private const int NumRegisterTypes = (int)PTXRegisterKind.Float64 + 1;

        /// <summary>
        /// Maps basic types to PTX register kinds.
        /// </summary>
        private static readonly ImmutableArray<PTXRegisterKind> RegisterTypeMapping = ImmutableArray.Create(
            default, PTXRegisterKind.Predicate,
            PTXRegisterKind.Int16, PTXRegisterKind.Int16, PTXRegisterKind.Int32, PTXRegisterKind.Int64,
            PTXRegisterKind.Float32, PTXRegisterKind.Float64);

        /// <summary>
        /// Maps basic value types to their PTX-specific parameter-type counterparts.
        /// </summary>
        private static readonly ImmutableArray<BasicValueType> ParameterTypeRemapping = ImmutableArray.Create(
            default, BasicValueType.Int32,
            BasicValueType.Int8, BasicValueType.Int16, BasicValueType.Int32, BasicValueType.Int64,
            BasicValueType.Float32, BasicValueType.Float64);

        #endregion

        #region Static

        /// <summary>
        /// Returns the associated register kind.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The resolved register kind.</returns>
        public static PTXRegisterKind GetRegisterKind(BasicValueType basicValueType) =>
            RegisterTypeMapping[(int)basicValueType];

        /// <summary>
        /// Returns the associated register kind.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The resolved register kind.</returns>
        public static BasicValueType ResolveParameterBasicValueType(BasicValueType basicValueType) =>
            ParameterTypeRemapping[(int)basicValueType];

        /// <summary>
        /// Returns the corresponding device constant string value.
        /// </summary>
        /// <param name="register">The primitive register.</param>
        /// <returns>The corresponding device constant string value.</returns>
        private static string ResolveDeviceConstantValue(PrimitiveRegister register) =>
            ((DeviceConstantDimension3D)register.RegisterValue).ToString().ToLower();

        /// <summary>
        /// Returns the string representation of the given primitive register.
        /// </summary>
        /// <param name="register">The register.</param>
        /// <returns>The string representation.</returns>
        public static string GetStringRepresentation(PrimitiveRegister register)
        {
            switch (register.Kind)
            {
                case PTXRegisterKind.Predicate:
                    return "p" + register.RegisterValue;
                case PTXRegisterKind.Int16:
                    return "rs" + register.RegisterValue;
                case PTXRegisterKind.Int32:
                    return "r" + register.RegisterValue;
                case PTXRegisterKind.Int64:
                    return "rd" + register.RegisterValue;
                case PTXRegisterKind.Float32:
                    return "f" + register.RegisterValue;
                case PTXRegisterKind.Float64:
                    return "fd" + register.RegisterValue;
                case PTXRegisterKind.Ctaid:
                    return "ctaid." +
                        ResolveDeviceConstantValue(register);
                case PTXRegisterKind.Tid:
                    return "tid." +
                        ResolveDeviceConstantValue(register);
                case PTXRegisterKind.NctaId:
                    return "nctaid." +
                        ResolveDeviceConstantValue(register);
                case PTXRegisterKind.NtId:
                    return "ntid." +
                        ResolveDeviceConstantValue(register);
                case PTXRegisterKind.LaneId:
                    return "laneid";
                default:
                    throw new InvalidCodeGenerationException();
            }
        }

        #endregion

        #region Instance

        private readonly int[] registerCounters = new int[NumRegisterTypes];
        private readonly Stack<int>[] freeRegisters = new Stack<int>[NumRegisterTypes];

        /// <summary>
        /// Constructs a new register allocator.
        /// </summary>
        /// <param name="abi">The current ABI.</param>
        public PTXRegisterAllocator(ABI abi)
            : base(abi)
        {
            for (int i = 0; i < NumRegisterTypes; ++i)
                freeRegisters[i] = new Stack<int>();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Allocates a platform-specific register and returns the resulting PTX type
        /// for the current platform.
        /// </summary>
        /// <param name="description">The resolved register.</param>
        /// <returns>The allocated register.</returns>
        public PrimitiveRegister AllocatePlatformRegister(out RegisterDescription description)
        {
            description = ResolveRegisterDescription(ABI.PointerBasicValueType);
            return AllocateRegister(description);
        }

        /// <summary>
        /// Allocates a platform-specific register for the given node and
        /// returns the resulting PTX type for the current platform.
        /// </summary>
        /// <param name="node">The node to allocate.</param>
        /// <param name="description">The resolved register description.</param>
        /// <returns>The allocated register.</returns>
        public PrimitiveRegister AllocatePlatformRegister(Value node, out RegisterDescription description)
        {
            var register = AllocatePlatformRegister(out description);
            Bind(node, register);
            return register;
        }

        /// <summary>
        /// Resolves a register description for the basic value type.
        /// </summary>
        /// <param name="basicValueType">The basic value type to resolve.</param>
        /// <returns>The resolved register description.</returns>
        protected static RegisterDescription ResolveRegisterDescription(BasicValueType basicValueType) =>
            new RegisterDescription(
                basicValueType,
                GetRegisterKind(basicValueType));

        /// <summary>
        /// Resolves a register description for the given parameter type.
        /// </summary>
        /// <param name="type">The parameter type to resolve.</param>
        /// <returns>The resolved register description.</returns>
        protected RegisterDescription ResolveParameterRegisterDescription(TypeNode type)
        {
            if (type.IsPointerType)
                return ResolveRegisterDescription(ABI.PointerBasicValueType);
            // A return call cannot handle some types -> we have to
            // perform a PTX-specific type remapping
            var remapped = ResolveParameterBasicValueType(type.BasicValueType);
            return ResolveRegisterDescription(remapped);
        }

        /// <summary cref="RegisterAllocator{TKind}.ResolveRegisterDescription(TypeNode)"/>
        protected sealed override RegisterDescription ResolveRegisterDescription(TypeNode type)
        {
            if (type.IsPointerType)
                return ResolveRegisterDescription(ABI.PointerBasicValueType);
            return ResolveRegisterDescription(type.BasicValueType);
        }

        /// <summary cref="RegisterAllocator{TKind}.FreeRegister(RegisterAllocator{TKind}.PrimitiveRegister)"/>
        public sealed override void FreeRegister(PrimitiveRegister register)
        {
            var freeRegs = freeRegisters[(int)register.Kind];
            freeRegs.Push(register.RegisterValue);
        }

        /// <summary>
        /// Allocates a new 32bit integer register.
        /// </summary>
        /// <returns>The allocated primitive 32bit integer register.</returns>
        public PrimitiveRegister AllocateInt32Register() =>
            AllocateRegister(
                new RegisterDescription(BasicValueType.Int32, PTXRegisterKind.Int32));

        /// <summary cref="RegisterAllocator{TKind}.AllocateRegister(RegisterAllocator{TKind}.RegisterDescription)"/>
        public sealed override PrimitiveRegister AllocateRegister(RegisterDescription description)
        {
            var freeRegs = freeRegisters[(int)description.Kind];
            var registerValue = freeRegs.Count > 0 ?
                freeRegs.Pop() :
                ++registerCounters[(int)description.Kind];
            return new PrimitiveRegister(description, registerValue);
        }

        /// <summary>
        /// Appends register information to the given builder.
        /// </summary>
        /// <param name="builder">The builder to append to.</param>
        /// <param name="prefix">The prefix.</param>
        /// <param name="typeName">The type name.</param>
        /// <param name="registerName">The register name.</param>
        /// <param name="registerKind">The register kind.</param>
        private void AppendRegisterDeclaration(
            StringBuilder builder,
            string prefix,
            string typeName,
            string registerName,
            PTXRegisterKind registerKind)
        {
            var registerCounter = registerCounters[(int)registerKind];
            if (registerCounter < 1)
                return;

            builder.Append(prefix);
            builder.Append(".reg ");
            builder.Append(typeName);
            builder.Append('\t');
            builder.Append('%');
            builder.Append(registerName);
            builder.Append('<');
            builder.Append(registerCounter + 1);
            builder.Append('>');
            builder.AppendLine(";");
        }

        /// <summary>
        /// Generates register allocation information.
        /// </summary>
        /// <param name="prefix">The prefix to add.</param>
        /// <returns>Register allocation information.</returns>
        public string GenerateRegisterInformation(string prefix)
        {
            var builder = new StringBuilder();

            AppendRegisterDeclaration(builder, prefix, ".pred", "p", PTXRegisterKind.Predicate);

            AppendRegisterDeclaration(builder, prefix, ".b16", "rs", PTXRegisterKind.Int16);

            AppendRegisterDeclaration(builder, prefix, ".b32", "r", PTXRegisterKind.Int32);
            AppendRegisterDeclaration(builder, prefix, ".b64", "rd", PTXRegisterKind.Int64);

            AppendRegisterDeclaration(builder, prefix, ".f32", "f", PTXRegisterKind.Float32);
            AppendRegisterDeclaration(builder, prefix, ".f64", "fd", PTXRegisterKind.Float64);

            builder.AppendLine();

            return builder.ToString();
        }

        #endregion
    }
}

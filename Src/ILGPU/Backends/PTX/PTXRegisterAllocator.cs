// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXRegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

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
    public enum PTXRegisterKind
    {
        /// <summary>
        /// A predicate register.
        /// </summary>
        Predicate,

        /// <summary>
        /// An int16 register.
        /// </summary>
        Int16,

        /// <summary>
        /// An int32 register.
        /// </summary>
        Int32,

        /// <summary>
        /// An int64 register.
        /// </summary>
        Int64,

        /// <summary>
        /// A float32 register.
        /// </summary>
        Float32,

        /// <summary>
        /// A float64 register.
        /// </summary>
        Float64,

        /// <summary>
        /// The Ctaid register.
        /// </summary>
        Ctaid,

        /// <summary>
        /// The Tid register.
        /// </summary>
        Tid,

        /// <summary>
        /// The NctaId register.
        /// </summary>
        NctaId,

        /// <summary>
        /// The NtId register.
        /// </summary>
        NtId,

        /// <summary>
        /// The LaneId register.
        /// </summary>
        LaneId,
    }

    /// <summary>
    /// Represents a specialized PTX register allocator.
    /// </summary>
    public class PTXRegisterAllocator : RegisterAllocator<PTXRegisterKind>
    {
        #region Constants

        /// <summary>
        /// The number of possible register types.
        /// </summary>
        private const int NumRegisterTypes = (int)PTXRegisterKind.Float64 + 1;

        /// <summary>
        /// Maps basic types to PTX register kinds.
        /// </summary>
        private static readonly ImmutableArray<PTXRegisterKind> RegisterTypeMapping =
            ImmutableArray.Create(
                default, PTXRegisterKind.Predicate,
                PTXRegisterKind.Int16, PTXRegisterKind.Int16,
                PTXRegisterKind.Int32, PTXRegisterKind.Int64,
                PTXRegisterKind.Float32, PTXRegisterKind.Float64);

        /// <summary>
        /// Maps basic value types to their PTX-specific parameter-type counterparts.
        /// </summary>
        private static readonly ImmutableArray<BasicValueType> ParameterTypeRemapping =
            ImmutableArray.Create(
                default, BasicValueType.Int32,
                BasicValueType.Int8, BasicValueType.Int16,
                BasicValueType.Int32, BasicValueType.Int64,
                BasicValueType.Float32, BasicValueType.Float64);

        /// <summary>
        /// Declares all register kinds for which register declarations have to be
        /// generated.
        /// </summary>
        private static readonly ImmutableArray<(string, string, PTXRegisterKind)>
            RegisterDeclarations = ImmutableArray.Create(
                (".pred", "p", PTXRegisterKind.Predicate),
                (".b16", "rs", PTXRegisterKind.Int16),
                (".b32", "r", PTXRegisterKind.Int32),
                (".b64", "rd", PTXRegisterKind.Int64),
                (".f32", "f", PTXRegisterKind.Float32),
                (".f64", "fd", PTXRegisterKind.Float64));

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
        public static BasicValueType ResolveParameterBasicValueType(
            BasicValueType basicValueType) =>
            ParameterTypeRemapping[(int)basicValueType];

        /// <summary>
        /// Returns the corresponding device constant string value.
        /// </summary>
        /// <param name="register">The primitive register.</param>
        /// <returns>The corresponding device constant string value.</returns>
        private static string ResolveDeviceConstantValue(HardwareRegister register) =>
            ((DeviceConstantDimension3D)register.RegisterValue).ToString().ToLower();

        /// <summary>
        /// Returns the string representation of the given hardware register.
        /// </summary>
        /// <param name="register">The register.</param>
        /// <returns>The string representation.</returns>
        public static string GetStringRepresentation(HardwareRegister register)
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
        public HardwareRegister AllocatePlatformRegister(
            out RegisterDescription description)
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
        public HardwareRegister AllocatePlatformRegister(
            Value node,
            out RegisterDescription description)
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
        protected static RegisterDescription ResolveRegisterDescription(
            BasicValueType basicValueType) =>
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
            if (type.IsPointerType || type.IsStringType)
                return ResolveRegisterDescription(ABI.PointerBasicValueType);
            // A return call cannot handle some types -> we have to
            // perform a PTX-specific type remapping
            var remapped = ResolveParameterBasicValueType(type.BasicValueType);
            return ResolveRegisterDescription(remapped);
        }

        /// <summary>
        /// Resolves a new PTX compatible register description.
        /// </summary>
        protected sealed override RegisterDescription ResolveRegisterDescription(
            TypeNode type) =>
            type.IsPointerType || type.IsStringType
            ? ResolveRegisterDescription(ABI.PointerBasicValueType)
            : ResolveRegisterDescription(type.BasicValueType);

        /// <summary>
        /// Frees the given hardware register.
        /// </summary>
        public sealed override void FreeRegister(HardwareRegister hardwareRegister)
        {
            var freeRegs = freeRegisters[(int)hardwareRegister.Kind];
            freeRegs.Push(hardwareRegister.RegisterValue);
        }

        /// <summary>
        /// Allocates a new 32bit integer register.
        /// </summary>
        /// <returns>The allocated primitive 32bit integer register.</returns>
        public HardwareRegister AllocateInt32Register() =>
            AllocateRegister(
                new RegisterDescription(BasicValueType.Int32, PTXRegisterKind.Int32));

        /// <summary>
        /// Allocates a register that is compatible with the given description.
        /// </summary>
        public sealed override HardwareRegister AllocateRegister(
            RegisterDescription description)
        {
            var freeRegs = freeRegisters[(int)description.Kind];
            var registerValue = freeRegs.Count > 0 ?
                freeRegs.Pop() :
                ++registerCounters[(int)description.Kind];
            return new HardwareRegister(description, registerValue);
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
        internal string GenerateRegisterInformation(string prefix)
        {
            var builder = new StringBuilder();
            foreach (var (typeName, name, kind) in RegisterDeclarations)
            {
                AppendRegisterDeclaration(
                    builder,
                    prefix,
                    typeName,
                    name,
                    kind);
            }
            builder.AppendLine();
            return builder.ToString();
        }

        #endregion
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXRegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

        /// <summary>
        /// The DynamicSharedMemorySize register.
        /// </summary>
        DynamicSharedMemorySize,
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
                PTXRegisterKind.Int16, PTXRegisterKind.Float32, PTXRegisterKind.Float64);

        /// <summary>
        /// Maps basic value types to their PTX-specific parameter-type counterparts.
        /// </summary>
        private static readonly ImmutableArray<BasicValueType> ParameterTypeRemapping =
            ImmutableArray.Create(
                default, BasicValueType.Int32,
                BasicValueType.Int8, BasicValueType.Int16,
                BasicValueType.Int32, BasicValueType.Int64,
                BasicValueType.Int16, BasicValueType.Float32, BasicValueType.Float64);

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
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
        private static string ResolveDeviceConstantValue(HardwareRegister register) =>
            ((DeviceConstantDimension3D)register.RegisterValue)
                .ToString()
                .ToLowerInvariant();

        /// <summary>
        /// Returns the string representation of the given hardware register.
        /// </summary>
        /// <param name="register">The register.</param>
        /// <returns>The string representation.</returns>
        public static string GetStringRepresentation(HardwareRegister register) =>
            register.Kind switch
            {
                PTXRegisterKind.Predicate => "p" + register.RegisterValue,
                PTXRegisterKind.Int16 => "rs" + register.RegisterValue,
                PTXRegisterKind.Int32 => "r" + register.RegisterValue,
                PTXRegisterKind.Int64 => "rd" + register.RegisterValue,
                PTXRegisterKind.Float32 => "f" + register.RegisterValue,
                PTXRegisterKind.Float64 => "fd" + register.RegisterValue,
                PTXRegisterKind.Ctaid => "ctaid." +
                    ResolveDeviceConstantValue(register),
                PTXRegisterKind.Tid => "tid." +
                    ResolveDeviceConstantValue(register),
                PTXRegisterKind.NctaId => "nctaid." +
                    ResolveDeviceConstantValue(register),
                PTXRegisterKind.NtId => "ntid." +
                    ResolveDeviceConstantValue(register),
                PTXRegisterKind.LaneId => "laneid",
                PTXRegisterKind.DynamicSharedMemorySize => "dynamic_smem_size",
                _ => throw new InvalidCodeGenerationException(),
            };

        #endregion

        #region Instance

        private readonly int[] registerCounters = new int[NumRegisterTypes];
        private readonly Stack<int>[] freeRegisters = new Stack<int>[NumRegisterTypes];

        /// <summary>
        /// Constructs a new register allocator.
        /// </summary>
        /// <param name="backend">The associated backend.</param>
        public PTXRegisterAllocator(PTXBackend backend)
            : base(backend)
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
            description = ResolveRegisterDescription(Backend.PointerBasicValueType);
            return AllocateRegister(description);
        }

        /// <summary>
        /// Resolves a register description for the basic value type.
        /// </summary>
        /// <param name="basicValueType">The basic value type to resolve.</param>
        /// <returns>The resolved register description.</returns>
        protected RegisterDescription ResolveRegisterDescription(
            BasicValueType basicValueType) =>
            RegisterDescription.Create(
                TypeContext.GetPrimitiveType(basicValueType),
                GetRegisterKind(basicValueType));

        /// <summary>
        /// Resolves a register description for the given parameter type.
        /// </summary>
        /// <param name="type">The parameter type to resolve.</param>
        /// <returns>The resolved register description.</returns>
        protected RegisterDescription ResolveParameterRegisterDescription(TypeNode type)
        {
            if (type.IsPointerType || type.IsStringType)
            {
                return RegisterDescription.Create(
                    type,
                    Backend.PointerBasicValueType,
                    GetRegisterKind(Backend.PointerBasicValueType));
            }
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
            ? RegisterDescription.Create(
                type,
                Backend.PointerBasicValueType,
                GetRegisterKind(Backend.PointerBasicValueType))
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
                BasicValueType.Int32,
                PTXRegisterKind.Int32);

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

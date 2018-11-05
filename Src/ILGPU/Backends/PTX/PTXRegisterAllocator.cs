// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXRegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Represents the register kind of a PTX register.
    /// </summary>
    enum PTXRegisterKind
    {
        Predicate,

        Int8,
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
    /// Represents a single PTX register.
    /// </summary>
    readonly struct PTXRegister : IRegister<PTXRegisterKind>
    {
        /// <summary>
        /// Constructs a new PTX register.
        /// </summary>
        /// <param name="kind">The current register kind.</param>
        /// <param name="registerValue">The register index value.</param>
        public PTXRegister(PTXRegisterKind kind, int registerValue)
        {
            Kind = kind;
            RegisterValue = registerValue;
        }

        /// <summary cref="IRegister{TKind}.Kind"/>
        public PTXRegisterKind Kind { get; }

        /// <summary cref="IRegister{TKind}.RegisterValue"/>
        public int RegisterValue { get; }

        /// <summary>
        /// Returns the corresponding device constant string value.
        /// </summary>
        /// <returns>The corresponding device constant string value.</returns>
        private string ResolveDeviceConstantValue() =>
            ((DeviceConstantDimension3D)RegisterValue).ToString().ToLower();

        /// <summary>
        /// Returns the associated register name in PTX code.
        /// </summary>
        /// <returns>The register name in PTX code.</returns>
        public override string ToString()
        {
            switch (Kind)
            {
                case PTXRegisterKind.Predicate:
                    return "p" + RegisterValue;
                case PTXRegisterKind.Int8:
                    return "rb" + RegisterValue;
                case PTXRegisterKind.Int16:
                    return "rs" + RegisterValue;
                case PTXRegisterKind.Int32:
                    return "r" + RegisterValue;
                case PTXRegisterKind.Int64:
                    return "rd" + RegisterValue;
                case PTXRegisterKind.Float32:
                    return "f" + RegisterValue;
                case PTXRegisterKind.Float64:
                    return "fd" + RegisterValue;
                case PTXRegisterKind.Ctaid:
                    return "ctaid." +
                        ResolveDeviceConstantValue();
                case PTXRegisterKind.Tid:
                    return "tid." +
                        ResolveDeviceConstantValue();
                case PTXRegisterKind.NctaId:
                    return "nctaid." +
                        ResolveDeviceConstantValue();
                case PTXRegisterKind.NtId:
                    return "ntid." +
                        ResolveDeviceConstantValue();
                case PTXRegisterKind.LaneId:
                    return "laneid";
                default:
                    throw new InvalidCodeGenerationException();
            }
        }
    }

    /// <summary>
    /// Represents a specialized PTX register allocator.
    /// </summary>
    sealed class PTXRegisterAllocator
    {
        #region Constants

        /// <summary>
        /// The number of possible register types.
        /// </summary>
        private const int NumRegisterTypes = (int)PTXRegisterKind.Float64 + 1;

        #endregion

        #region Nested Types

        /// <summary>
        /// The default PTX allocator behavior.
        /// </summary>
        readonly struct Behavior : IRegisterAllocationBehavior<PTXRegisterKind, PTXRegister>
        {
            /// <summary>
            /// Constructs a new allocator behavior.
            /// </summary>
            /// <param name="allocator">The parent accelerator.</param>
            public Behavior(PTXRegisterAllocator allocator)
            {
                Allocator = allocator;
            }

            /// <summary>
            /// Returns the associated parent allocator.
            /// </summary>
            public PTXRegisterAllocator Allocator { get; }

            /// <summary cref="IRegisterAllocationBehavior{TKind, T}.AllocateRegister(TKind)"/>
            public PTXRegister AllocateRegister(PTXRegisterKind kind) =>
                Allocator.AllocateRegister(kind);

            /// <summary cref="IRegisterAllocationBehavior{TKind, T}.FreeRegister(T)"/>
            public void FreeRegister(PTXRegister register) =>
                Allocator.FreeRegister(register);
        }

        #endregion

        #region Instance

        private readonly RegisterAllocator<PTXRegisterKind, PTXRegister, Behavior> registerAllocator;
        private readonly int[] registerCounters = new int[NumRegisterTypes];
        private readonly Stack<int>[] freeRegisters = new Stack<int>[NumRegisterTypes];

        /// <summary>
        /// Constructs a new register allocator.
        /// </summary>
        /// <param name="abi">The current ABI.</param>
        public PTXRegisterAllocator(ABI abi)
        {
            Debug.Assert(abi != null, "Invalid abi");
            registerAllocator = new RegisterAllocator<PTXRegisterKind, PTXRegister, Behavior>(
                new Behavior(this));
            ABI = abi;
            for (int i = 0; i < NumRegisterTypes; ++i)
                freeRegisters[i] = new Stack<int>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current ABI.
        /// </summary>
        public ABI ABI { get; }

        #endregion

        #region Methods

        /// <summary cref="RegisterAllocator{TKind, T, TBehavior}.Allocate(Value, TKind)"/>
        public PTXRegister Allocate(Value node, PTXRegisterKind kind) =>
            registerAllocator.Allocate(node, kind);

        /// <summary cref="RegisterAllocator{TKind, T, TBehavior}.Alias(Value, Value)"/>
        public void Alias(Value node, Value alias) =>
            registerAllocator.Alias(node, alias);

        /// <summary>
        /// Allocates a platform-specific register and returns the resulting PTX type
        /// for the current platform.
        /// </summary>
        /// <param name="registerType">The platform register type.</param>
        /// <returns>The allocated register.</returns>
        public PTXRegister AllocatePlatformRegister(out PTXType registerType)
        {
            registerType = PTXType.GetPTXType(ABI.PointerArithmeticType);
            return AllocateRegister(registerType.RegisterKind);
        }

        /// <summary>
        /// Allocates a platform-specific register for the given node and
        /// returns the resulting PTX type for the current platform.
        /// </summary>
        /// <param name="node">The node to allocate.</param>
        /// <param name="registerType">The platform register type.</param>
        /// <returns>The allocated register.</returns>
        public PTXRegister AllocatePlatformRegister(Value node, out PTXType registerType)
        {
            registerType = PTXType.GetPTXType(ABI.PointerArithmeticType);
            return Allocate(node, registerType.RegisterKind);
        }

        /// <summary cref="IRegisterAllocationBehavior{TKind, T}.AllocateRegister(TKind)"/>
        public PTXRegister AllocateRegister(PTXRegisterKind kind)
        {
            var freeRegs = freeRegisters[(int)kind];
            var registerValue = freeRegs.Count > 0 ?
                freeRegs.Pop() :
                registerCounters[(int)kind]++;
            return new PTXRegister(
                kind,
                registerValue);
        }

        /// <summary cref="RegisterAllocator{TKind, T, TBehavior}.Free(Value)"/>
        public void Free(Value value) =>
            registerAllocator.Free(value);

        /// <summary cref="IRegisterAllocationBehavior{TKind, T}.FreeRegister(T)"/>
        public void FreeRegister(PTXRegister register)
        {
            var freeRegs = freeRegisters[(int)register.Kind];
            freeRegs.Push(register.RegisterValue);
        }

        /// <summary cref="RegisterAllocator{TKind, T, TBehavior}.Load(Value)"/>
        public PTXRegister Load(Value node) =>
            registerAllocator.Load(node);

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
            builder.Append(registerCounter);
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

            AppendRegisterDeclaration(builder, prefix, ".b8", "rb", PTXRegisterKind.Int8);
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

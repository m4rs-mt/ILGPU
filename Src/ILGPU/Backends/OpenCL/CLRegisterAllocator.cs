// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLRegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.PointerViews;
using ILGPU.IR.Types;
using System.Collections.Immutable;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Represents the register kind of an OpenCL register.
    /// </summary>
    public enum CLRegisterKind
    {
        /// <summary>
        /// A bool register.
        /// </summary>
        Bool,

        /// <summary>
        /// A char register.
        /// </summary>
        Char,

        /// <summary>
        /// An unsigned char register.
        /// </summary>
        UChar,

        /// <summary>
        /// A short register.
        /// </summary>
        Short,

        /// <summary>
        /// An unsigned short register.
        /// </summary>
        UShort,

        /// <summary>
        /// An integer register.
        /// </summary>
        Int,

        /// <summary>
        /// An unsigned integer register.
        /// </summary>
        UInt,

        /// <summary>
        /// A long register.
        /// </summary>
        Long,

        /// <summary>
        /// An unsigned long register.
        /// </summary>
        ULong,

        /// <summary>
        /// A float register.
        /// </summary>
        Float,

        /// <summary>
        /// A double register.
        /// </summary>
        Double,
    }

    /// <summary>
    /// Represents a specialized OpenCL register allocator.
    /// </summary>
    public class CLRegisterAllocator : ViewRegisterAllocator<CLRegisterKind>
    {
        #region Constants

        /// <summary>
        /// The number of possible register types.
        /// </summary>
        public const int NumRegisterTypes = (int)CLRegisterKind.Double + 1;

        /// <summary>
        /// Maps basic OpenCL register kinds to OpenCL language types.
        /// </summary>
        private static readonly ImmutableArray<string> LanguageTypeMapping = ImmutableArray.Create(
            "bool",
            "char",
            "uchar",
            "short",
            "ushort",
            "int",
            "uint",
            "long",
            "ulong",
            "float",
            "double");

        /// <summary>
        /// Maps basic types to OpenCL register kinds.
        /// </summary>
        private static readonly ImmutableArray<CLRegisterKind> RegisterTypeMapping = ImmutableArray.Create(
            default, CLRegisterKind.Bool,
            CLRegisterKind.Char, CLRegisterKind.Short, CLRegisterKind.Int, CLRegisterKind.Long,
            CLRegisterKind.Float, CLRegisterKind.Double);

        #endregion

        #region Static

        /// <summary>
        /// Returns the associated register kind.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <returns>The resolved register kind.</returns>
        public static CLRegisterKind GetRegisterKind(BasicValueType basicValueType) =>
            RegisterTypeMapping[(int)basicValueType];

        /// <summary>
        /// Returns the string representation of the given primitive register.
        /// </summary>
        /// <param name="register">The register.</param>
        /// <returns>The string representation.</returns>
        public static string GetStringRepresentation(PrimitiveRegister register) =>
            LanguageTypeMapping[(int)register.Kind];

        #endregion

        #region Instance

        private int registerIdCounter = 0;

        /// <summary>
        /// Constructs a new register allocator.
        /// </summary>
        /// <param name="abi">The current ABI.</param>
        public CLRegisterAllocator(ABI abi)
            : base(abi)
        { }

        #endregion

        #region Methods

        /// <summary cref="RegisterAllocator{TKind}.AllocateRegister(RegisterAllocator{TKind}.RegisterDescription)"/>
        public sealed override PrimitiveRegister AllocateRegister(RegisterDescription description) =>
            new PrimitiveRegister(description, registerIdCounter++);

        /// <summary cref="RegisterAllocator{TKind}.ResolveRegisterDescription(TypeNode)"/>
        protected sealed override RegisterDescription ResolveRegisterDescription(TypeNode type) =>
            new RegisterDescription(
                type.BasicValueType,
                GetRegisterKind(type.BasicValueType));

        /// <summary cref="RegisterAllocator{TKind}.FreeRegister(RegisterAllocator{TKind}.PrimitiveRegister)"/>
        public sealed override void FreeRegister(PrimitiveRegister primitiveRegister)
        {
            // Do nothing -> we will not reuse any variables
        }

        #endregion
    }
}

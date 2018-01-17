// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ABISpecification.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ILGPU.Backends.ABI
{
    /// <summary>
    /// Represents an ABI specification.
    /// </summary>
    abstract class ABISpecification : DisposeBase
    {
        #region Static

        /// <summary>
        /// Contains default .Net alignment information about built-in blittable types.
        /// </summary>
        public static readonly IDictionary<Type, int> ManagedAlignments = new Dictionary<Type, int>
        {
            {  typeof(sbyte), 1 },
            { typeof(short), 2 },
            { typeof(int), 4 },
            { typeof(long), 8 },

            { typeof(byte), 1 },
            { typeof(ushort), 2 },
            { typeof(uint), 4 },
            { typeof(ulong), 8 },

            { typeof(float), 4 },
            { typeof(double), 8 },
        };

        /// <summary>
        /// Contains default non-blittable .Net types that can be handled by ILGPU.
        /// </summary>
        public static readonly IReadOnlyCollection<Type> NonBlittableTypes = new List<Type>
        {
            typeof(bool),
            typeof(char),
        };

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new ABI specification.
        /// </summary>
        /// <param name="unit">The compile unit used for ABI generation.</param>
        public ABISpecification(CompileUnit unit)
        {
            CompileUnit = unit;
        }

        /// <summary>
        /// Registers default non-blittable types.
        /// </summary>
        protected void AddNonBlittableTypes()
        {
            foreach (var type in NonBlittableTypes)
            {
                if (Alignments.ContainsKey(type))
                    continue;
                Alignments.Add(type, GetAlignmentOf(type));
            }
        }

        /// <summary>
        /// Registers platform-dependent pointer types.
        /// </summary>
        /// <param name="alignment">The alignment of pointers.</param>
        protected void AddPtrAlignment(int alignment)
        {
            Alignments.Add(typeof(IntPtr), alignment);
            Alignments.Add(typeof(UIntPtr), alignment);
        }

        #endregion

        #region Properties

        protected Dictionary<Type, int> Alignments { get; } =
            new Dictionary<Type, int>();

        /// <summary>
        /// Returns the native LLVM context.
        /// </summary>
        public LLVMContextRef LLVMContext => CompileUnit.LLVMContext;

        /// <summary>
        /// Returns the assocated compile unit.
        /// </summary>
        public CompileUnit CompileUnit { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Computes the actual ABI size of the given type in bytes.
        /// </summary>
        /// <param name="type">The input type.</param>
        /// <returns>The ABI size of the given type.</returns>
        public abstract int GetSizeOf(Type type);

        /// <summary>
        /// Computes the actual ABI alignment of the given type in bytes.
        /// </summary>
        /// <param name="type">The input type.</param>
        /// <returns>The ABI alignment of the given type.</returns>
        public abstract int GetAlignmentOf(Type type);

        /// <summary>
        /// Returns the required alignment of the given type in bytes.
        /// </summary>
        /// <param name="type">The input type.</param>
        /// <returns>The required alignment of the given type in bytes.</returns>
        public int this[Type type]
        {
            get
            {
                if (type.IsPointer)
                    return Alignments[typeof(IntPtr)];
                if (!Alignments.TryGetValue(type, out int alignment))
                    Alignments.Add(type, alignment);
                return alignment;
            }
        }

        /// <summary>
        /// Aligns the given field according to the current ABI.
        /// </summary>
        /// <param name="fields">The fields of the current type.</param>
        /// <param name="fieldIndex">The index of the current field.</param>
        /// <param name="structElements">The target LLVM-structure elements.</param>
        public abstract void AlignField(
            FieldInfo[] fields,
            int fieldIndex,
            List<LLVMTypeRef> structElements);

        /// <summary>
        /// Adds or manipulates added structure elements to achieve a certain alignment.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="structElements">The target LLVM-structure elements.</param>
        public abstract void AlignType(
            Type type,
            List<LLVMTypeRef> structElements);

        #endregion
    }
}

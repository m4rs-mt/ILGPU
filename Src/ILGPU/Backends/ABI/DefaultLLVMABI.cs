// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: DefaultLLVMABI.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using static ILGPU.LLVM.LLVMMethods;

namespace ILGPU.Backends.ABI
{
    /// <summary>
    /// Represents a default LLVM-based ABI specification.
    /// It uses LLVM data-layout information to resolve alignment
    /// and size information of types.
    /// </summary>
    abstract class DefaultLLVMABI : ABISpecification
    {
        #region Instance

        /// <summary>
        /// Constructs a default LLVM-based ABI specification.
        /// </summary>
        /// <param name="unit">The compile unit used for ABI generation.</param>
        public DefaultLLVMABI(CompileUnit unit)
            : base(unit)
        {
            var backend = unit.Backend as LLVMBackend;
            if (backend == null)
                throw new NotSupportedException(ErrorMessages.NotSupportedBackend);
            LLVMTargetData = CreateTargetDataLayout(backend.LLVMTargetMachine);
            foreach (var managedAlignment in ManagedAlignments)
            {
                var managedType = managedAlignment.Key;
                var llvmType = unit.GetType(managedType);
                var alignment = ABIAlignmentOfType(LLVMTargetData, llvmType);
                // We need a special case for the builtin mapping of 64bit floats
                // to 32bit floats since this mapping changes the alignment logic.
                if (unit.Force32BitFloats && managedType == typeof(double))
                    managedType = typeof(float);
                if (ManagedAlignments[managedType] != alignment)
                    throw new NotSupportedException(string.Format(
                        ErrorMessages.CustomABIImplementationRequired, managedAlignment.Key));
                Alignments.Add(managedAlignment.Key, alignment);
            }
            AddNonBlittableTypes();
            AddPtrAlignment(ABIAlignmentOfType(
                LLVMTargetData,
                unit.LLVMContext.VoidPtrType));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the LLVM target data for this ABI specification.
        /// </summary>
        public LLVMTargetDataRef LLVMTargetData { get; private set; }

        #endregion

        #region Methods

        /// <summary cref="ABISpecification.GetSizeOf(Type)"/>
        public override int GetSizeOf(Type type)
        {
            return GetSizeOf(CompileUnit.GetType(type));
        }

        /// <summary>
        /// Computes the actual ABI size of the given type in bytes.
        /// </summary>
        /// <param name="type">The input type.</param>
        /// <returns>The ABI size of the given type.</returns>
        public int GetSizeOf(LLVMTypeRef type)
        {
            return (int)ABISizeOfType(LLVMTargetData, type);
        }

        /// <summary cref="ABISpecification.GetAlignmentOf(Type)"/>
        public override int GetAlignmentOf(Type type)
        {
            return ABIAlignmentOfType(
                LLVMTargetData,
                CompileUnit.GetType(type));
        }

        /// <summary cref="ABISpecification.AlignField(FieldInfo[], int, List{LLVMTypeRef})"/>
        public override void AlignField(
            FieldInfo[] fields,
            int fieldIndex,
            List<LLVMTypeRef> structElements)
        {
            var field = fields[fieldIndex];
            var type = field.FieldType;
            if (field.GetCustomAttribute<FieldOffsetAttribute>() != null)
                throw new NotSupportedException(string.Format(
                    ErrorMessages.ExplicitMemoryOffsetRequired,
                    field.DeclaringType,
                    field));

            var typeAlignment = this[type];
            // Caution: a custom ABI implementation requires an adaption of the required kernel launch code
            // -> Dont allow unsupported ABIs here
            if (ManagedAlignments.TryGetValue(type, out int managedAlignment) &&
                typeAlignment != managedAlignment)
                throw new NotSupportedException(string.Format(
                    ErrorMessages.CustomABIImplementationRequired, type));
        }

        /// <summary cref="ABISpecification.AlignType(Type, List{LLVMTypeRef})"/>
        public override void AlignType(Type type, List<LLVMTypeRef> structElements)
        {
            // Compute the managed type size (which implictly contains required alignment information)
            var managedTypeSize = type.SizeOf();

            // Compute the LLVM ABI-dependent size of structure
            var tempType = StructTypeInContext(
                LLVMContext,
                structElements.ToArray());
            var abiSize = (int)ABISizeOfType(LLVMTargetData, tempType);

            // Caution: a custom ABI implementation requires an adaption of the required kernel launch code
            // -> Dont allow unsupported ABIs here
            if (abiSize > managedTypeSize)
                throw new NotSupportedException(string.Format(
                    ErrorMessages.CustomABIImplementationRequired, type));

            // Check custom packing
            var packing = type.StructLayoutAttribute?.Pack;
            if (packing.HasValue && (packing % 4) != 0)
                throw new NotSupportedException(string.Format(
                    ErrorMessages.NotSupportedStructDueToExplicitPacking, type));

            // Do we require a specific size to match the .Net ABI?
            if (abiSize < managedTypeSize)
            {
                AlignSmallType(type, structElements, managedTypeSize, abiSize);

                tempType = StructTypeInContext(
                    LLVMContext,
                    structElements.ToArray());
                abiSize = (int)ABISizeOfType(LLVMTargetData, tempType);
                if (abiSize != managedTypeSize)
                    throw new NotSupportedException(string.Format(
                        ErrorMessages.CustomABIImplementationRequired, type));
            }
        }

        /// <summary>
        /// Aligns a type with a size that is less than the required .Net size.
        /// </summary>
        /// <param name="type">The type to align.</param>
        /// <param name="structElements">The LLVM struct elements for adjustment.</param>
        /// <param name="managedSize">The .Net size of the given type in bytes.</param>
        /// <param name="abiSize">The current ABI size of the given type in bytes.</param>
        protected abstract void AlignSmallType(
            Type type,
            List<LLVMTypeRef> structElements,
            int managedSize,
            int abiSize);

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (LLVMTargetData.Pointer != IntPtr.Zero)
            {
                DisposeTargetData(LLVMTargetData);
                LLVMTargetData = default(LLVMTargetDataRef);
            }
        }

        #endregion
    }
}

// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXABI.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ILGPU.Backends.ABI
{
    /// <summary>
    /// Represents a PTX ABI specification.
    /// </summary>
    sealed class PTXABI : DefaultLLVMABI
    {
        #region Instance

        /// <summary>
        /// Constructs a new PTX ABI specification.
        /// </summary>
        /// <param name="unit">The compile unit used for ABI generation.</param>
        public PTXABI(CompileUnit unit)
            : base(unit)
        { }

        #endregion

        #region Methods

        /// <summary cref="ABISpecification.AlignField(FieldInfo[], int, List{LLVMTypeRef})"/>
        public override void AlignField(
            FieldInfo[] fields,
            int fieldIndex,
            List<LLVMTypeRef> structElements)
        {
            // Note: We do not have to adjust anything here.
            // We even do not have to handle special cases for types like bool.
            // They will be automatically aligned by the PTX ABI.
            base.AlignField(fields, fieldIndex, structElements);
        }

        /// <summary cref="DefaultLLVMABI.AlignSmallType(Type, List{LLVMTypeRef}, int, int)"/>
        protected override void AlignSmallType(
            Type type,
            List<LLVMTypeRef> structElements,
            int managedSize,
            int abiSize)
        {
            // There can only be one special case here:
            // struct S { } -> Managed Size: 1
            // struct S { } -> Native Size: 0
            if (managedSize != abiSize + 1)
                throw new NotSupportedException();
            // We have to add an additional byte to match the .Net
            // We have to add an additional bytes to match the .Net
            // type alignment of an empty struct.
            structElements.Add(LLVMContext.Int8Type);
        }

        #endregion
    }
}

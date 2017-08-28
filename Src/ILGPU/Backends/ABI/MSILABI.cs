// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: MSILABI.cs
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

namespace ILGPU.Backends.ABI
{
    /// <summary>
    /// Represents a MSIL-internal ABI speficiation.
    /// </summary>
    sealed class MSILABI : ABISpecification
    {
        #region Instance

        /// <summary>
        /// Constructs a new MSIL ABI Specification.
        /// </summary>
        /// <param name="unit">The compile unit used for ABI generation.</param>
        public MSILABI(CompileUnit unit)
            : base(unit)
        {
            foreach (var entry in ManagedAlignments)
                Alignments.Add(entry.Key, entry.Value);
            switch (CompileUnit.Backend.Platform)
            {
                case TargetPlatform.X64:
                    AddPtrAlignment(8);
                    break;
                case TargetPlatform.X86:
                    AddPtrAlignment(4);
                    break;
                default:
                    throw new NotSupportedException(
                        ErrorMessages.NotSupportedTargetPlatform);
            }
        }

        #endregion

        #region Methods

        /// <summary cref="ABISpecification.GetSizeOf(Type)"/>
        public override int GetSizeOf(Type type)
        {
            return type.SizeOf();
        }

        /// <summary cref="ABISpecification.GetAlignmentOf(Type)"/>
        public override int GetAlignmentOf(Type type)
        {
            return GetSizeOf(type);
        }

        /// <summary cref="ABISpecification.AlignField(FieldInfo[], int, List{LLVMTypeRef})"/>
        public override void AlignField(
            FieldInfo[] fields,
            int fieldIndex,
            List<LLVMTypeRef> structElements)
        {
            // We do not have to adjust anything here...
        }

        /// <summary cref="ABISpecification.AlignType(Type, List{LLVMTypeRef})"/>
        public override void AlignType(
            Type type,
            List<LLVMTypeRef> structElements)
        {
            // We do not have to adjust anything here...
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        { }

        #endregion
    }
}

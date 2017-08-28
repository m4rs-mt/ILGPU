// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: MappedType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.LLVM;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace ILGPU.Compiler
{
    /// <summary>
    /// Represents a .Net type that was mapped to a LLVM type.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class MappedType
    {
        #region Instance

        /// <summary>
        /// Maps field information to offsets.
        /// </summary>
        private readonly Dictionary<FieldInfo, int> fieldOffsets;

        /// <summary>
        /// Constructs a new mapped type.
        /// </summary>
        /// <param name="type">The .Net type.</param>
        /// <param name="llvmType">The LLVM type.</param>
        /// <param name="numLLVMTypeElements">The number of LLVM-struct-type elements.</param>
        /// <param name="fieldOffsets">The individual field offsets.</param>
        [CLSCompliant(false)]
        public MappedType(
            Type type,
            LLVMTypeRef llvmType,
            int numLLVMTypeElements,
            Dictionary<FieldInfo, int> fieldOffsets)
        {
            this.fieldOffsets = fieldOffsets;
            ManagedType = type;
            LLVMType = llvmType;
            NumLLVMTypeElements = numLLVMTypeElements;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the .Net type.
        /// </summary>
        public Type ManagedType { get; }

        /// <summary>
        /// Returns the corresponding LLVM type.
        /// </summary>
        [CLSCompliant(false)]
        public LLVMTypeRef LLVMType { get; }

        /// <summary>
        /// Returns the number of fields.
        /// </summary>
        public int NumFields => fieldOffsets == null ? 0 : fieldOffsets.Count;

        /// <summary>
        /// Returns the stored fields.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "Matches the enumerable type of the wrapped dictionary")]
        public IEnumerable<KeyValuePair<FieldInfo, int>> Fields
        {
            get { return fieldOffsets ?? Enumerable.Empty<KeyValuePair<FieldInfo, int>>(); }
        }

        /// <summary>
        /// Resolves the offset of the given field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>The target offset.</returns>
        [SuppressMessage("Microsoft.Design", "CA1043:UseIntegralOrStringArgumentForIndexers", Justification = "Allows direct mapping of fields to their offsets")]
        public int this[FieldInfo field] => fieldOffsets[field];

        /// <summary>
        /// Returns the total number of LLVM struct elements.
        /// </summary>
        public int NumLLVMTypeElements { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve an offset for the given field.
        /// </summary>
        /// <param name="info">The field.</param>
        /// <param name="offset">The target offset.</param>
        /// <returns>True, if this field can be resolved to an offset.</returns>
        public bool TryResolveOffset(FieldInfo info, out int offset)
        {
            if (fieldOffsets == null)
            {
                offset = -1;
                return false;
            }
            return fieldOffsets.TryGetValue(info, out offset);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of this type.
        /// </summary>
        /// <returns>The string representation of this type.</returns>
        public override string ToString()
        {
            return ManagedType.Name;
        }

        #endregion
    }
}

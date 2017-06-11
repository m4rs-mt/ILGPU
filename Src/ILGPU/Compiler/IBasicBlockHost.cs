// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: IBasicBlockHost.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using LLVMSharp;
using System;

namespace ILGPU.Compiler
{
    /// <summary>
    /// Represents a host for basic blocks.
    /// </summary>
    interface IBasicBlockHost
    {
        /// <summary>
        /// Returns the associated compile unit.
        /// </summary>
        CompileUnit Unit { get; }

        /// <summary>
        /// Returns the current instruction builder.
        /// </summary>
        IRBuilder InstructionBuilder { get; }

        /// <summary>
        /// Returns the current compilation context.
        /// </summary>
        CompilationContext CompilationContext { get; }

        /// <summary>
        /// Conerts the given class value to the target type.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="targetType">The target type.</param>
        Value CreateCastClass(Value value, Type targetType);

        /// <summary>
        /// Conerts the given value to the target type.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="forceUnsigned">True if the comparison should be forced to be unsigned.</param>
        Value CreateConversion(Value value, Type targetType, bool forceUnsigned = false);

        /// <summary>
        /// Returns true iff the given block was already preprocessed.
        /// </summary>
        /// <param name="block">The block to check.</param>
        /// <returns>True, iff the given block was already preprocessed.</returns>
        bool IsProcessed(BasicBlock block);
    }
}

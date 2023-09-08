// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityEntryPointHandler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents a single velocity kernel processing delegate.
    /// </summary>
    /// <param name="groupContext">The main group context.</param>
    /// <param name="groupDim">The main group dimension.</param>
    /// <param name="gridDim">The main grid dimension.</param>
    /// <param name="startIndex">The global start index (inclusive).</param>
    /// <param name="endIndex">The global end index (exclusive).</param>
    /// <param name="parameters">The current parameters.</param>
    delegate void VelocityEntryPointHandler(
        VelocityGroupExecutionContext groupContext,
        int groupDim,
        int gridDim,
        long startIndex,
        long endIndex,
        VelocityParameters parameters);

    /// <summary>
    /// Static helper class to support dealing with
    /// <see cref="VelocityEntryPointHandler"/>  instances.
    /// </summary>
    static class VelocityEntryPointHandlerHelper
    {
        /// <summary>
        /// Represents all entry point parameters expected by a Velocity kernel entry
        /// point function.
        /// </summary>
        public static readonly Type[] EntryPointParameterTypes = new Type[]
        {
            typeof(VelocityGroupExecutionContext),
            typeof(int),
            typeof(int),
            typeof(long),
            typeof(long),
            typeof(VelocityParameters),
        };
    }
}

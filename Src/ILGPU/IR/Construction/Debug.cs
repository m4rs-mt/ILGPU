// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Debug.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new failed debug assertion.
        /// </summary>
        /// <param name="parentMemoryValue">The parent memory operation.</param>
        /// <param name="message">The assertion message.</param>
        /// <returns>A node that represents the debug assertion.</returns>
        public ValueReference CreateDebugAssertFailed(
            MemoryRef parentMemoryValue,
            Value message)
        {
            Debug.Assert(parentMemoryValue != null, "Invalid parent memory value");
            Debug.Assert(message != null, "Invalid message value");

            return Context.CreateInstantiated(new DebugAssertFailed(
                Generation,
                parentMemoryValue,
                message,
                VoidType));
        }

        /// <summary>
        /// Creates a new debug trace.
        /// </summary>
        /// <param name="parentMemoryValue">The parent memory operation.</param>
        /// <param name="message">The assertion message.</param>
        /// <returns>A node that represents the debug trace event.</returns>
        public ValueReference CreateDebugTrace(
            MemoryRef parentMemoryValue,
            Value message)
        {
            Debug.Assert(parentMemoryValue != null, "Invalid parent memory value");
            Debug.Assert(message != null, "Invalid message value");

            return Context.CreateInstantiated(new DebugTrace(
                Generation,
                parentMemoryValue,
                message,
                VoidType));
        }
    }
}

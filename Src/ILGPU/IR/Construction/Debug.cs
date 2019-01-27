// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
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
        /// <param name="message">The assertion message.</param>
        /// <returns>A node that represents the debug assertion.</returns>
        public ValueReference CreateDebugAssertFailed(
            Value message)
        {
            Debug.Assert(message != null, "Invalid message value");

            return Append(new DebugAssertFailed(
                Context,
                BasicBlock,
                message));
        }

        /// <summary>
        /// Creates a new debug trace.
        /// </summary>
        /// <param name="message">The assertion message.</param>
        /// <returns>A node that represents the debug trace event.</returns>
        public ValueReference CreateDebugTrace(
            Value message)
        {
            Debug.Assert(message != null, "Invalid message value");

            return Append(new DebugTrace(
                Context,
                BasicBlock,
                message));
        }
    }
}

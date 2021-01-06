// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Debug.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a new failed debug assertion.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="condition">The debug assert condition.</param>
        /// <param name="message">The assertion message.</param>
        /// <returns>A node that represents the debug assertion.</returns>
        public ValueReference CreateDebugAssert(
            Location location,
            Value condition,
            Value message) =>
            Append(new DebugAssertOperation(
                GetInitializer(location),
                condition,
                message));
    }
}

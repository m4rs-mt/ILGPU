// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Debug.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System.Diagnostics.CodeAnalysis;

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
        [SuppressMessage(
            "Maintainability",
            "CA1508:Avoid dead conditional code",
            Justification = "Check is required")]
        public ValueReference CreateDebugAssert(
            Location location,
            Value condition,
            Value message)
        {
            location.Assert(message is StringValue);

            // Try to simplify debug assertions
            if (condition is PrimitiveValue primitiveValue &&
                primitiveValue.RawValue != 0L)
            {
                return CreateUndefined();
            }

            return Append(new DebugAssertOperation(
                GetInitializer(location),
                condition,
                message));
        }
    }
}

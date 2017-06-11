// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: DeviceFunctions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.Compiler.Intrinsic
{
    /// <summary>
    /// Represents the base interface for custom device functions.
    /// </summary>
    public interface IDeviceFunctions
    {
        /// <summary>
        /// Tries to remap the given invocation context to another context.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>The remapped context or null, iff the remapping operation was not successful.</returns>
        InvocationContext? Remap(InvocationContext context);

        /// <summary>
        /// Tries to handle a specific invocation context. This method
        /// can generate custom code instead of the default method-invocation
        /// functionality.
        /// </summary>
        /// <param name="invocationContext">The current invocation context.</param>
        /// <param name="result">The resulting value of the intrinsic call.</param>
        /// <returns>True, iff this class could handle the call.</returns>
        bool Handle(InvocationContext invocationContext, out Value? result);
    }
}

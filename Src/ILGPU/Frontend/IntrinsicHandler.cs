// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IntrinsicHandler.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;

namespace ILGPU.Frontend
{
    /// <summary>
    /// Represents the base interface for custom intrinsic functions.
    /// </summary>
    public interface IIntrinsicHandler
    {
        /// <summary>
        /// Tries to remap the given invocation context to another context.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <returns>True, iff the context could be mapped.</returns>
        bool Remap(ref InvocationContext context);

        /// <summary>
        /// Tries to handle a specific invocation context. This method
        /// can generate custom code instead of the default method-invocation
        /// functionality.
        /// </summary>
        /// <param name="context">The current invocation context.</param>
        /// <param name="result">The resulting value of the intrinsic call.</param>
        /// <returns>True, iff this class could handle the call.</returns>
        bool Handle(in InvocationContext context, ref ValueReference result);
    }
}

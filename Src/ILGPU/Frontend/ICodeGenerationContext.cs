// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ICodeGenerationContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Resources;
using System;

namespace ILGPU.Frontend
{
    /// <summary>
    /// An abstract code-generation context.
    /// </summary>
    public interface ICodeGenerationContext
    {
        /// <summary>
        /// Constructs a new exception of the given type based on the given
        /// message, the formatting arguments and the current general compilation information.
        /// </summary>
        /// <typeparam name="TException">The exception type.</typeparam>
        /// <param name="message">The main content of the error message.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>A new exception of type <typeparamref name="TException"/>.</returns>
        TException GetException<TException>(
            string message,
            params object[] args)
            where TException : Exception;
    }

    /// <summary>
    /// Contains extensions methods for abstract code-generation contexts.
    /// </summary>
    public static class CodeGenerationContextExtensions
    {
        /// <summary>
        /// Constructs a new <see cref="ArgumentException"/> based on the given
        /// message, the formatting arguments and the current general compilation information.
        /// </summary>
        /// <param name="context">The code-generation context.</param>
        /// <param name="message">The main content of the error message.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>A new <see cref="ArgumentException"/>.</returns>
        public static ArgumentException GetArgumentException<TGenerationContext>(
            this TGenerationContext context,
            string message,
            params object[] args)
            where TGenerationContext : ICodeGenerationContext
        {
            return context.GetException<ArgumentException>(message, args);
        }

        /// <summary>
        /// Constructs a new <see cref="NotSupportedException"/> based on the given
        /// message, the formatting arguments and the current general compilation information.
        /// </summary>
        /// <param name="context">The code-generation context.</param>
        /// <param name="message">The main content of the error message.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>A new <see cref="NotSupportedException"/>.</returns>
        public static NotSupportedException GetNotSupportedException<TGenerationContext>(
            this TGenerationContext context,
            string message,
            params object[] args)
            where TGenerationContext : ICodeGenerationContext
        {
            return context.GetException<NotSupportedException>(message, args);
        }

        /// <summary>
        /// Constructs a new <see cref="InvalidOperationException"/> based on the given
        /// message, the formatting arguments and the current general compilation information.
        /// </summary>
        /// <param name="context">The code-generation context.</param>
        /// <param name="message">The main content of the error message.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>A new <see cref="InvalidOperationException"/>.</returns>
        public static InvalidOperationException GetInvalidOperationException<TGenerationContext>(
            this TGenerationContext context,
            string message,
            params object[] args)
            where TGenerationContext : ICodeGenerationContext
        {
            return context.GetException<InvalidOperationException>(message, args);
        }

        /// <summary>
        /// Constructs a new <see cref="InvalidOperationException"/> that refers to an
        /// invalid IL code.
        /// </summary>
        /// <param name="context">The code-generation context.</param>
        /// <returns>A new <see cref="InvalidOperationException"/>.</returns>
        public static InvalidOperationException GetInvalidILCodeException<TGenerationContext>(
            this TGenerationContext context)
            where TGenerationContext : ICodeGenerationContext
        {
            return context.GetException<InvalidOperationException>(ErrorMessages.InvalidILCode);
        }
    }
}

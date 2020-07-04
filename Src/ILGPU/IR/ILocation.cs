// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ILocation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents an abstract location.
    /// </summary>
    public interface ILocation
    {
        /// <summary>
        /// Formats an error message to include specific location information.
        /// </summary>
        /// <param name="message">The source error message.</param>
        /// <returns>The formatted error message.</returns>
        string FormatErrorMessage(string message);
    }

    /// <summary>
    /// Extension methods for <see cref="ILocation"/> implementations.
    /// </summary>
    public static class Locations
    {
        /// <summary>
        /// Constructs a new exception of the given type based on the given
        /// message, the formatting arguments and the current sequence point.
        /// information.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="message">The error message.</param>
        /// <returns>
        /// A new exception of type <typeparamref name="TException"/> with detailed
        /// origin information about the current source location at which this exception
        /// has been created.
        /// </returns>
        public static TException GetException<TException>(
            this ILocation location,
            string message)
            where TException : Exception
        {
            message = (location?.FormatErrorMessage(
                message ?? ErrorMessages.InternalCompilerError))
                ?? ErrorMessages.InternalCompilerError;
            return Activator.CreateInstance(typeof(TException), message) as TException;
        }

        /// <summary>
        /// Constructs a new <see cref="ArgumentOutOfRangeException"/> based on the given
        /// message, the formatting arguments and the current sequence point.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="paramName">The parameter name.</param>
        /// <returns>A new <see cref="ArgumentOutOfRangeException"/>.</returns>
        public static ArgumentOutOfRangeException GetArgumentException(
            this ILocation location,
            string paramName) =>
            location.GetException<ArgumentOutOfRangeException>(paramName);

        /// <summary>
        /// Constructs a new <see cref="ArgumentException"/> based on the given
        /// message, the formatting arguments and the current sequence point.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="paramName">The parameter name.</param>
        /// <returns>A new <see cref="ArgumentException"/>.</returns>
        public static ArgumentNullException GetArgumentNullException(
            this ILocation location,
            string paramName) =>
            location.GetException<ArgumentNullException>(paramName);

        /// <summary>
        /// Constructs a new <see cref="NotSupportedException"/> based on the given
        /// message, the formatting arguments and the current sequence point.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="message">The main contents of the error message.</param>
        /// <param name="args">The formatting arguments.</param>
        /// <returns>A new <see cref="NotSupportedException"/>.</returns>
        public static NotSupportedException GetNotSupportedException(
            this ILocation location,
            string message,
            params object[] args) =>
            location.GetException<NotSupportedException>(
                string.Format(message, args));

        /// <summary>
        /// Constructs a new <see cref="InvalidOperationException"/> that refers to an
        /// invalid compiler state.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <returns>A new <see cref="InvalidOperationException"/>.</returns>
        public static InvalidOperationException GetInvalidOperationException(
            this ILocation location) =>
            location.GetException<InvalidOperationException>(
                ErrorMessages.InternalCompilerError);

        /// <summary>
        /// Constructs a new <see cref="InvalidOperationException"/> that refers to an
        /// invalid compiler state.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="message">The main content of the error message.</param>
        /// <returns>A new <see cref="InvalidOperationException"/>.</returns>
        public static InvalidOperationException GetInvalidOperationException(
            this ILocation location,
            string message) =>
            location.GetException<InvalidOperationException>(message);

        /// <summary>
        /// Ensures that a certain reference value is not null.
        /// </summary>
        /// <remarks>
        /// This assertion method implementation will not be present in release modes.
        /// </remarks>
        /// <param name="location">The current location.</param>
        /// <param name="value">The value to be not null.</param>
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AssertNotNull<TValue>(this ILocation location, TValue value)
            where TValue : class
        {
            if (value == null)
            {
                throw location.GetException<InvalidOperationException>(
                    ErrorMessages.InternalCompilerErrorNull);
            }
        }

        /// <summary>
        /// Ensures a certain compiler-internal assertion to hold. In contrast to
        /// <see cref="Debug.Assert(bool, string)"/>, this method creates an exception
        /// that is easy to capture and recognize in the scope of test suites.
        /// </summary>
        /// <remarks>
        /// This assertion method implementation will not be present in release modes.
        /// </remarks>
        /// <param name="location">The current location.</param>
        /// <param name="condition">The condition to hold.</param>
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Assert(this ILocation location, bool condition)
        {
            if (!condition)
            {
                throw location.GetException<InvalidOperationException>(
                    ErrorMessages.InternalCompilerError);
            }
        }
    }
}

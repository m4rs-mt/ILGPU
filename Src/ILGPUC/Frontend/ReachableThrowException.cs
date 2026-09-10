// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ReachableThrowException.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;

namespace ILGPUC.Frontend;

/// <summary>
/// Reports that code which throws an exception is reachable for the type arguments a
/// method is compiled with. Kernels cannot throw exceptions, but a throw is fine in a
/// branch that is removed at compile time, such as the fallback of a chain of
/// <c>typeof(T)</c> tests.
/// </summary>
/// <remarks>
/// A dedicated type lets the build skip kernel instantiations that were only found
/// through generic wrappers and fail for this reason alone: launching them throws at
/// run time, as the same code would on the CPU.
/// </remarks>
sealed class ReachableThrowException : NotSupportedException
{
    /// <summary>
    /// Creates a new exception.
    /// </summary>
    public ReachableThrowException() { }

    /// <summary>
    /// Creates a new exception with the given message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ReachableThrowException(string message)
        : base(message)
    { }

    /// <summary>
    /// Creates a new exception with the given message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ReachableThrowException(string message, Exception innerException)
        : base(message, innerException)
    { }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="exception"/> or one of its
    /// inner exceptions is a <see cref="ReachableThrowException"/>.
    /// </summary>
    public static bool IsCausedBy(Exception? exception)
    {
        for (var current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is ReachableThrowException)
                return true;
        }
        return false;
    }
}

/// <summary>
/// Formats types and methods for frontend diagnostics.
/// </summary>
static class FrontendDisplay
{
    /// <summary>
    /// Formats a type with its generic arguments, for example
    /// <c>Pair&lt;Single&gt;</c>.
    /// </summary>
    public static string Format(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;
        var name = type.Name;
        int tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
            name = name[..tick];
        var arguments = string.Join(", ", type.GetGenericArguments().Select(Format));
        return $"{name}<{arguments}>";
    }

    /// <summary>
    /// Formats a method with its declaring type and generic arguments, for example
    /// <c>Kernels.GenericSin&lt;Int32&gt;</c>.
    /// </summary>
    public static string Format(MethodBase method)
    {
        var name = method.Name;
        if (method.IsGenericMethod)
        {
            var arguments = string.Join(
                ", ",
                method.GetGenericArguments().Select(Format));
            name = $"{name}<{arguments}>";
        }
        return method.DeclaringType is { } declaringType
            ? $"{Format(declaringType)}.{name}"
            : name;
    }
}

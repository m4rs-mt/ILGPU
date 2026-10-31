// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FailedMethodVerifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

/// <summary>
/// Reports calls to methods whose code generation failed, and <c>typeof(T)</c>
/// values that were not folded away.
/// </summary>
/// <remarks>
/// The frontend marks a helper method external when its code generation fails, and
/// keeps compiling: optimization may still remove every call to it, for example on
/// an overflow path of a BCL helper. Any call that survives optimization would end
/// up as a call to an undefined function in the generated code, so this check turns
/// it into an error that names the caller, the failed method and the reason, or
/// reports the diagnostic the failed method was rejected with.
/// </remarks>
static class FailedMethodVerifier
{
    /// <summary>
    /// Throws if <paramref name="module"/> still calls a method from
    /// <paramref name="failedMethods"/>.
    /// </summary>
    /// <param name="module">The optimized module.</param>
    /// <param name="failedMethods">
    /// The methods whose code generation failed, with the reason
    /// (<see cref="ILFrontend.FailedMethods"/>).
    /// </param>
    public static void Verify(
        IR.ModuleValues.Module module,
        IReadOnlyDictionary<MethodBase, Exception> failedMethods)
    {
        // Report errors other than reachable throws first: an instantiation may
        // only be skipped if every failure is a throw
        Exception? throwFailure = null;
        foreach (var method in module.Methods)
        {
            foreach (var block in method.Blocks)
            {
                // typeof(T) values must be folded away; no backend can lower them
                block.ForEachValue<HandleValue>(handle =>
                {
                    if (handle.Handle is not Type)
                        return;
                    throw handle.Location.GetNotSupportedException(
                        ErrorMessages.NotSupportedRuntimeTypeValue,
                        method.Source is { } source
                            ? FrontendDisplay.Format(source)
                            : method.Name);
                });

                if (failedMethods.Count == 0)
                    continue;

                foreach (var (value, _) in block.BasicBlockValues)
                {
                    if (value is not MethodCall call ||
                        call.Target.Source is not { } target ||
                        !failedMethods.TryGetValue(target, out var failure))
                    {
                        continue;
                    }

                    // Diagnostics of the failed method, such as static fields
                    // that kernels cannot load, keep their identifiers and
                    // locations in the failed method. They point at the call
                    // instead if the failed method has no source location, such
                    // as a library method without debug information, or if it
                    // is an accessor the compiler generates for an
                    // auto-implemented property, whose location is the property
                    // declaration
                    if (failure is KernelCompilationException compilationFailure)
                    {
                        bool generatedAccessor = target.IsSpecialName &&
                            target.IsDefined(typeof(CompilerGeneratedAttribute));
                        if (compilationFailure.Location is FileLocation &&
                            !generatedAccessor)
                        {
                            throw failure;
                        }
                        throw new KernelCompilationException(
                            compilationFailure.Id,
                            compilationFailure.Message,
                            call.Location);
                    }

                    var caller = method.Source is { } source
                        ? FrontendDisplay.Format(source)
                        : method.Name;
                    var message = string.Format(
                        ErrorMessages.NotSupportedCallToFailedMethod,
                        caller,
                        FrontendDisplay.Format(target),
                        GetReason(failure));

                    // Keep the classification of a reachable throw, so that the
                    // build can skip instantiations that fail only because of it
                    if (!ReachableThrowException.IsCausedBy(failure))
                    {
                        throw call.Location.GetException(
                            new NotSupportedException(message));
                    }
                    throwFailure ??= call.Location.GetException(
                        new ReachableThrowException(message));
                }
            }
        }

        // A kernel whose own code throws was generated in full to find other
        // errors; report its throw now
        foreach (var method in module.Methods)
        {
            if (method.Source is { } source &&
                !method.HasFlags(MethodFlags.External) &&
                failedMethods.TryGetValue(source, out var failure))
            {
                throw failure;
            }
        }
        if (throwFailure is not null)
            throw throwFailure;
    }

    /// <summary>
    /// Returns the reason of a failure: the messages of the exception and its inner
    /// exceptions, which carry the actual problem. A located failure wraps its
    /// problem in an internal compiler error, whose message reports nothing but
    /// the location: the reason starts from the problem and keeps the location.
    /// </summary>
    private static string GetReason(Exception failure)
    {
        Location? location = null;
        if (failure is InternalCompilerException { InnerException: { } problem } error)
        {
            location = error.SourceLocation;
            failure = problem;
        }
        var reason = failure.Message;
        for (var inner = failure.InnerException;
             inner is not null;
             inner = inner.InnerException)
        {
            reason += $" {inner.Message}";
        }
        reason = reason.TrimEnd();
        return location?.FormatErrorMessage(reason) ?? reason;
    }
}

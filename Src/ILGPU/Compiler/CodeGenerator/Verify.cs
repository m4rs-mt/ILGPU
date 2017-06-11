// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: Verify.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Compiler
{
    sealed partial class CodeGenerator
    {
        /// <summary>
        /// Verifies that the given method is not a .Net-runtime-dependent method.
        /// If it depends on the runtime, this method will throw a <see cref="NotSupportedException"/>.
        /// </summary>
        /// <param name="compilationContext">The current compilation context.</param>
        /// <param name="method">The method to verify.</param>
        public static void VerifyNotRuntimeMethod(
            CompilationContext compilationContext,
            MethodBase method)
        {
            Debug.Assert(compilationContext != null, "Invalid compilation context");
            Debug.Assert(method != null, "Invalid method");
            var @namespace = method.DeclaringType.FullName;
            // Internal unsafe intrinsic methods
            if (@namespace == "System.Runtime.CompilerServices.Unsafe")
                return;
            if (@namespace.StartsWith("System.Runtime", StringComparison.OrdinalIgnoreCase) ||
                @namespace.StartsWith("System.Reflection", StringComparison.OrdinalIgnoreCase))
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedRuntimeMethod, method.Name);
        }

        /// <summary>
        /// Verifies that the given method is not a warp-shuffle instruction iff the entry point
        /// is not a grouped-index kernel. Note that all other (non-shuffle) methods will be accepted.
        /// </summary>
        /// <param name="compilationContext">The current compilation context.</param>
        /// <param name="method">The method to verify.</param>
        /// <param name="entryPoint">The kernel entry point.</param>
        public static void VerifyAccessToWarpShuffle(
            CompilationContext compilationContext,
            MethodBase method,
            EntryPoint entryPoint)
        {
            Debug.Assert(compilationContext != null, "Invalid compilation context");
            Debug.Assert(method != null, "Invalid method");
            Debug.Assert(entryPoint != null, "Invalid entry point");
            if (method.DeclaringType != typeof(Warp) ||
                !method.Name.StartsWith(nameof(Warp.Shuffle), StringComparison.OrdinalIgnoreCase))
                return;
            if (!entryPoint.IsGroupedIndexEntry)
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedWarpShuffle, method.Name);
        }

        /// <summary>
        /// Verifies a static-field load operation.
        /// </summary>
        /// <param name="compilationContext">The current compilation context.</param>
        /// <param name="flags">The current compile unit flags.</param>
        /// <param name="field">The static field to load.</param>
        public static void VerifyStaticFieldLoad(
            CompilationContext compilationContext,
            CompileUnitFlags flags,
            FieldInfo field)
        {
            Debug.Assert(compilationContext != null, "Invalid compilation context");
            Debug.Assert(field != null || !field.IsStatic, "Invalid field");

            if ((field.Attributes & FieldAttributes.InitOnly) != FieldAttributes.InitOnly &&
                (flags & CompileUnitFlags.InlineMutableStaticFieldValues) != CompileUnitFlags.InlineMutableStaticFieldValues)
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedLoadOfStaticField, field);
        }

        /// <summary>
        /// Verifies a static-field store operation.
        /// </summary>
        /// <param name="compilationContext">The current compilation context.</param>
        /// <param name="flags">The current compile unit flags.</param>
        /// <param name="field">The static field to store to.</param>
        public static void VerifyStaticFieldStore(
            CompilationContext compilationContext,
            CompileUnitFlags flags,
            FieldInfo field)
        {
            Debug.Assert(compilationContext != null, "Invalid compilation context");
            Debug.Assert(field != null || !field.IsStatic, "Invalid field");

            if ((flags & CompileUnitFlags.IgnoreStaticFieldStores) != CompileUnitFlags.IgnoreStaticFieldStores)
                throw compilationContext.GetNotSupportedException(
                    ErrorMessages.NotSupportedStoreToStaticField, field);
        }
    }
}

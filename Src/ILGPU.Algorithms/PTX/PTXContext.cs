// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.PTX;
using ILGPU.IR.Intrinsics;
using ILGPU.Runtime.Cuda;
using ILGPU.Util;
using System;
using System.Reflection;

namespace ILGPU.Algorithms.PTX
{
    /// <summary>
    /// Manages custom PTX-specific intrinsics.
    /// </summary>
    static partial class PTXContext
    {
        /// <summary>
        /// The <see cref="PTXMath"/> type.
        /// </summary>
        private static readonly Type PTXMathType = typeof(PTXMath);

        /// <summary>
        /// Represents the <see cref="PTXMath.GenerateMathIntrinsic(PTXBackend,
        /// PTXCodeGenerator, IR.Value)"/>
        /// methods.
        /// </summary>
        private static readonly MethodInfo MathCodeGenerator =
            PTXMathType.GetMethod(
                nameof(PTXMath.GenerateMathIntrinsic),
                AlgorithmContext.IntrinsicBindingFlags)
            .ThrowIfNull();

        /// <summary>
        /// Represents the intrinsic representation of the
        /// <see cref="MathCodeGenerator"/>.
        /// </summary>
        private static readonly PTXIntrinsic MathCodeGeneratorIntrinsic =
            new PTXIntrinsic(
                MathCodeGenerator,
                IntrinsicImplementationMode.GenerateCode)
            .ThrowIfNull();

        /// <summary>
        /// The <see cref="PTXGroupExtensions"/> type.
        /// </summary>
        internal static readonly Type PTXGroupExtensionsType = typeof(PTXGroupExtensions);

        /// <summary>
        /// The <see cref="PTXWarpExtensions"/> type.
        /// </summary>
        internal static readonly Type PTXWarpExtensionsType = typeof(PTXWarpExtensions);

        /// <summary>
        /// Resolves a PTX code generator for the given math-function configuration.
        /// </summary>
        /// <param name="minArchitecture">The target/minimum architecture.</param>
        /// <returns>The resolved intrinsic representation.</returns>
        private static PTXIntrinsic GetMathCodeGeneratorIntrinsic(
            CudaArchitecture minArchitecture) =>
            new PTXIntrinsic(
                PTXMathType,
                nameof(PTXMath.GenerateMathIntrinsic),
                IntrinsicImplementationMode.GenerateCode,
                minArchitecture);

        /// <summary>
        /// Resolves a PTX intrinsic for the given math-function configuration.
        /// </summary>
        /// <param name="name">The intrinsic name.</param>
        /// <param name="types">The parameter types.</param>
        /// <returns>The resolved intrinsic representation.</returns>
        private static PTXIntrinsic GetMathIntrinsic(string name, params Type[] types)
        {
            var targetMethod = PTXMathType.GetMethod(
                name,
                AlgorithmContext.IntrinsicBindingFlags,
                null,
                types,
                null)
                .ThrowIfNull();
            return new PTXIntrinsic(targetMethod, IntrinsicImplementationMode.Redirect);
        }

        /// <summary>
        /// Registers an intrinsic mapping.
        /// </summary>
        /// <param name="manager">The current manager.</param>
        /// <param name="sourceType">The source type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="name">The method name to register.</param>
        private static void RegisterIntrinsicMapping(
            IntrinsicImplementationManager manager,
            Type sourceType,
            Type targetType,
            string name)
        {
            var sourceMethod = sourceType.GetMethod(
                name,
                AlgorithmContext.IntrinsicBindingFlags)
                .ThrowIfNull();
            manager.RegisterMethod(
                sourceMethod,
                new PTXIntrinsic(targetType, name, IntrinsicImplementationMode.Redirect));
        }

        /// <summary>
        /// Registers an XMath replacement mapping using a redirect.
        /// </summary>
        /// <param name="manager">The current manager.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="functionName">The method name to register.</param>
        /// <param name="replacementName">
        /// The name of the replacement method to register.
        /// </param>
        /// <param name="types">The argument types for the target method.</param>
        private static void RegisterXMathRedirect(
            IntrinsicImplementationManager manager,
            Type targetType,
            string functionName,
            string replacementName,
            params Type[] types)
        {
            manager.RegisterMethod(
                AlgorithmContext.XMathType.GetMethod(
                    functionName,
                    AlgorithmContext.IntrinsicBindingFlags,
                    null,
                    types,
                    null)
                    .ThrowIfNull(),
                new PTXIntrinsic(
                    targetType.GetMethod(
                        replacementName,
                        AlgorithmContext.IntrinsicBindingFlags,
                        null,
                        types,
                        null)
                        .ThrowIfNull(),
                    IntrinsicImplementationMode.Redirect));
        }
    }
}

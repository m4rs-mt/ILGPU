// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                     Copyright(c) 2016-2018 ILGPU Lightning Project
//                                    www.ilgpu.net
//
// File: AlgorithmContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms;
using ILGPU.Algorithms.CL;
using ILGPU.Algorithms.IL;
using ILGPU.Algorithms.PTX;
using ILGPU.IR;
using System;
using System.Reflection;

namespace ILGPU
{
    /// <summary>
    /// Represents the main driver class for all algorithms.
    /// </summary>
    public static partial class AlgorithmContext
    {
        #region Fields

        /// <summary>
        /// The default intrinsic binding flags.
        /// </summary>
        internal const BindingFlags IntrinsicBindingFlags =
            BindingFlags.Public | BindingFlags.Static;

        /// <summary>
        /// The global <see cref="XMath"/> type.
        /// </summary>
        internal static readonly Type XMathType = typeof(XMath);

        /// <summary>
        /// The global <see cref="GroupExtensions"/> type.
        /// </summary>
        internal static readonly Type GroupExtensionsType = typeof(GroupExtensions);

        /// <summary>
        /// The global <see cref="WarpExtensions"/> type.
        /// </summary>
        internal static readonly Type WarpExtensionsType = typeof(WarpExtensions);

        #endregion

        #region Static Instance

        /// <summary>
        /// Initializes a static instance.
        /// </summary>
        static AlgorithmContext()
        {
            RegisterMathRemappings();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Enables algorithm extensions in the scope of the given context builder.
        /// </summary>
        /// <param name="builder">The builder to enable algorithms for.</param>
        public static void EnableAlgorithms(this Context.Builder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var intrinsicManager = builder.GetIntrinsicManager();
            CLContext.EnableCLAlgorithms(intrinsicManager);
            ILContext.EnableILAlgorithms(intrinsicManager);
            PTXContext.EnablePTXAlgorithms(intrinsicManager);
        }

        #endregion
    }
}

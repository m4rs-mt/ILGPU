// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                Copyright(c) 2016-2018 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: AlgorithmContext.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Algorithms;
using ILGPU.Algorithms.IL;
using ILGPU.Algorithms.PTX;
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
        internal static readonly BindingFlags IntrinsicBindingFlags =
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
        /// Enables algorithm extensions in the scope of the given context.
        /// </summary>
        /// <param name="context">The context to enable algorithms for.</param>
        public static void EnableAlgorithms(this Context context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var intrinsicManager = context.IntrinsicManager;
            ILContext.EnableILAlgorithms(intrinsicManager);
            PTXContext.EnablePTXAlgorithms(intrinsicManager);
        }

        #endregion
    }
}

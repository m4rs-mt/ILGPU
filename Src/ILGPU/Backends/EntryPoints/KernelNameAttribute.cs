// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: KernelNameAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Reflection;

namespace ILGPU.Backends.EntryPoints
{
    /// <summary>
    /// Specifies a custom kernel name used in OpenCL or PTX kernels.
    /// </summary>
    /// <remarks>
    /// Kernel names have to consist of ASCII characters only.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class KernelNameAttribute : Attribute
    {
        #region Constants

        /// <summary>
        /// The internally used kernel prefix to avoid clashes with other utility/local
        /// functions in the finally emitted assembly.
        /// </summary>
        private const string KernelNamePrefix = "Kernel_";

        #endregion

        #region Static

        /// <summary>
        /// Gets the kernel name for the given entry point function.
        /// </summary>
        /// <param name="methodInfo">The entry point function.</param>
        /// <returns>The kernel name.</returns>
        public static string GetKernelName(MethodInfo methodInfo)
        {
            if (methodInfo is null)
                throw new ArgumentNullException(nameof(methodInfo));
            var attribute = methodInfo.GetCustomAttribute<KernelNameAttribute>();
            var kernelName = GetCompatibleName(attribute?.KernelName ?? methodInfo.Name);
            return KernelNamePrefix + kernelName;
        }

        /// <summary>
        /// Returns a compatible function name for all runtime backends.
        /// </summary>
        /// <param name="name">The source name.</param>
        internal static string GetCompatibleName(string name)
        {
            var chars = name.ToCharArray();
            for (int i = 0, e = chars.Length; i < e; ++i)
            {
                ref var charValue = ref chars[i];
                // Map to ASCII and letter/digit characters only
                if (charValue >= 128 || !char.IsLetterOrDigit(charValue))
                    charValue = '_';
            }

            return new string(chars);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new kernel name attribute.
        /// </summary>
        /// <param name="kernelName">The kernel name to use.</param>
        public KernelNameAttribute(string kernelName)
        {
            if (string.IsNullOrWhiteSpace(kernelName))
                throw new ArgumentNullException(nameof(kernelName));
            KernelName = GetCompatibleName(kernelName);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the kernel name to use.
        /// </summary>
        public string KernelName { get; }

        #endregion
    }
}

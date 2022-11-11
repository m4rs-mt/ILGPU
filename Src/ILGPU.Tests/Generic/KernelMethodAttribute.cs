// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelMethodAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Tests
{
    /// <summary>
    /// Links test methods to kernels.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class KernelMethodAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new kernel attribute.
        /// </summary>
        /// <param name="methodName">The associated method name.</param>
        public KernelMethodAttribute(string methodName)
        {
            MethodName = methodName
                ?? throw new ArgumentNullException(nameof(methodName));
        }

        /// <summary>
        /// Constructs a new kernel attribute.
        /// </summary>
        /// <param name="methodName">The associated method name.</param>
        /// <param name="type">The source type.</param>
        public KernelMethodAttribute(string methodName, Type type)
            : this(methodName)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        /// <summary>
        /// Returns the kernel name.
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Returns the type in which the kernel method could be found (if any).
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Resolves the kernel method using the current configuration.
        /// </summary>
        /// <param name="typeArguments">The kernel type arguments.</param>
        /// <returns>The resolved kernel method.</returns>
        public static MethodInfo GetKernelMethod(
            Type[] typeArguments = null,
            int offset = 1)
        {
            // TODO: create a nicer way ;)
            var stackTrace = new StackTrace();
            for (int i = offset; i < stackTrace.FrameCount; ++i)
            {
                var frame = stackTrace.GetFrame(i);
                var callingMethod = frame.GetMethod();
                var attribute = callingMethod.GetCustomAttribute<
                    KernelMethodAttribute>();
                if (attribute == null)
                    continue;
                var type = attribute.Type ?? callingMethod.DeclaringType;
                return TestBase.GetKernelMethod(
                    type,
                    attribute.MethodName,
                    typeArguments);
            }
            throw new NotSupportedException(
                "Not supported kernel attribute. Missing attribute?");
        }
    }
}

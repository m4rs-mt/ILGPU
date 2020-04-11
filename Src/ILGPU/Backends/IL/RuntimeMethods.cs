// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: RuntimeMethods.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime.CPU;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// A container for CPU-based runtime methods.
    /// </summary>
    static class RuntimeMethods
    {
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1810:InitializeReferenceTypeStaticFieldsInline",
            Justification = "Caching of compiler-known functions")]
        static RuntimeMethods()
        {
            var contextType = typeof(CPURuntimeGroupContext);

            WaitForNextThreadIndex = contextType.GetMethod(
                nameof(CPURuntimeGroupContext.WaitForNextThreadIndex));
            BarrierMethod = contextType.GetMethod(nameof(CPURuntimeGroupContext.Barrier));
            var interlockedType = typeof(System.Threading.Interlocked);
            MemoryBarrierMethod = interlockedType.GetMethod(
                nameof(System.Threading.Interlocked.MemoryBarrier),
                BindingFlags.Public | BindingFlags.Static);
        }

        #region Properties

        /// <summary>
        /// Returns the main runtime wait and initialize method.
        /// </summary>
        public static MethodInfo WaitForNextThreadIndex { get; }

        /// <summary>
        /// Returns the main barrier method.
        /// </summary>
        public static MethodInfo BarrierMethod { get; }

        /// <summary>
        /// Returns the memory barrier method.
        /// </summary>
        public static MethodInfo MemoryBarrierMethod { get; }

        #endregion
    }
}

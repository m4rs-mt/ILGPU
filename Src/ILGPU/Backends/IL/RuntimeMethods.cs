// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: RuntimeMethods.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using ILGPU.IR.Values;
using ILGPU.Runtime.CPU;
using ILGPU.Util;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// A container for CPU-based runtime methods.
    /// </summary>
    static class RuntimeMethods
    {
        private static readonly MethodInfo[] predicateBarrierMethods;

        private static readonly Dictionary<(AtomicKind, ArithmeticBasicValueType), MethodInfo> atomicMethods =
            new Dictionary<(AtomicKind, ArithmeticBasicValueType), MethodInfo>();
        private static readonly Dictionary<ArithmeticBasicValueType, MethodInfo> atomicCASMethods =
            new Dictionary<ArithmeticBasicValueType, MethodInfo>();

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Caching of compiler-known functions")]
        static RuntimeMethods()
        {
            var contextType = typeof(CPURuntimeGroupContext);

            predicateBarrierMethods = new MethodInfo[]
            {
                contextType.GetMethod(nameof(CPURuntimeGroupContext.BarrierPopCount)),
                contextType.GetMethod(nameof(CPURuntimeGroupContext.BarrierAnd)),
                contextType.GetMethod(nameof(CPURuntimeGroupContext.BarrierOr))
            };
            WaitForNextThreadIndex = contextType.GetMethod(nameof(CPURuntimeGroupContext.WaitForNextThreadIndex));
            BarrierMethod = contextType.GetMethod(nameof(CPURuntimeGroupContext.Barrier));
            GetSharedMemoryViewMethod = contextType.GetProperty(
                nameof(CPURuntimeGroupContext.SharedMemory)).GetMethod;
            var interlockedType = typeof(System.Threading.Interlocked);
            MemoryBarrierMethod = interlockedType.GetMethod(
                nameof(System.Threading.Interlocked.MemoryBarrier),
                BindingFlags.Public | BindingFlags.Static);

            InitAtomicFunctions();
        }

        private static void InitAtomicFunctions()
        {
            var atomicType = typeof(Atomic);
            var atomicFunctions = atomicType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var atomicMethod in atomicFunctions)
            {
                var intrinsicAttribute = atomicMethod.GetCustomAttribute<AtomicIntrinsicAttribute>();
                if (intrinsicAttribute == null)
                    continue;
                var parameters = atomicMethod.GetParameters();
                var parameterType = parameters[1].ParameterType.GetArithmeticBasicValueType();
                if (intrinsicAttribute.IntrinsicKind == AtomicIntrinsicKind.CompareExchange)
                    atomicCASMethods.Add(parameterType, atomicMethod);
                else
                {
                    atomicMethods.Add(
                        ((AtomicKind)intrinsicAttribute.IntrinsicKind, parameterType),
                        atomicMethod);
                }
            }
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

        /// <summary>
        /// Returns a method to get a shared memory view.
        /// </summary>
        public static MethodInfo GetSharedMemoryViewMethod { get; }

        public static MethodInfo GetPredicateBarrierMethod(PredicateBarrierKind kind) =>
            predicateBarrierMethods[(int)kind];

        public static MethodInfo GetAtomicMethod(AtomicKind kind, ArithmeticBasicValueType valueType) =>
            atomicMethods[(kind, valueType)];

        public static MethodInfo GetAtomicCASMethod(ArithmeticBasicValueType valueType) =>
            atomicCASMethods[valueType];

        #endregion
    }
}

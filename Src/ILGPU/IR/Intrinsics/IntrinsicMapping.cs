// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IntrinsicMapping.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Intrinsics
{
    /// <summary>
    /// Represents an abstract intrinsic implementation mapping.
    /// </summary>
    public abstract class IntrinsicMapping : IIntrinsicImplementation
    {
        #region Nested Types

        /// <summary>
        /// Resolves generic arguments for redirection/code-generation purposes.
        /// </summary>
        public interface IGenericArgumentResolver
        {
            /// <summary>
            /// Resolves generic arguments for redirection/code-generation purposes.
            /// </summary>
            /// <returns>The resolved generic arguments.</returns>
            Type[] ResolveGenericArguments();
        }

        /// <summary>
        /// Resolves generic arguments from <see cref="System.Reflection.MethodInfo"/>
        /// objects.
        /// </summary>
        public readonly struct MethodInfoArgumentResolver : IGenericArgumentResolver
        {
            /// <summary>
            /// Constructs a new method-info argument resolver.
            /// </summary>
            /// <param name="methodInfo">The associated method information.</param>
            public MethodInfoArgumentResolver(MethodInfo methodInfo)
            {
                Debug.Assert(methodInfo != null, "Invalid method information");
                MethodInfo = methodInfo;
            }

            /// <summary>
            /// Returns the associated method information.
            /// </summary>
            public MethodInfo MethodInfo { get; }

            /// <summary cref="IGenericArgumentResolver.ResolveGenericArguments"/>
            public Type[] ResolveGenericArguments() => MethodInfo.GetGenericArguments();
        }

        /// <summary>
        /// Resolves generic arguments from <see cref="Value"/> objects.
        /// </summary>
        public readonly struct ValueArgumentResolver : IGenericArgumentResolver
        {
            /// <summary>
            /// Constructs a new value argument resolver.
            /// </summary>
            /// <param name="value">The associated value.</param>
            public ValueArgumentResolver(Value value)
            {
                Debug.Assert(value != null, "Invalid value");
                Value = value;
            }

            /// <summary>
            /// Returns the associated value.
            /// </summary>
            public Value Value { get; }

            /// <summary cref="IGenericArgumentResolver.ResolveGenericArguments"/>
            public Type[] ResolveGenericArguments() =>
                !Value.Type.TryResolveManagedType(out Type managedType)
                ? null
                : new Type[] { managedType };
        }

        /// <summary>
        /// Represents a cached mapping key.
        /// </summary>
        public readonly struct MappingKey : IEquatable<MappingKey>
        {
            #region Instance

            private readonly Type[] genericArguments;

            /// <summary>
            /// Constructs a new mapping key.
            /// </summary>
            /// <param name="arguments">The type arguments.</param>
            public MappingKey(Type[] arguments)
            {
                genericArguments = arguments;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the number of mapped generic arguments.
            /// </summary>
            public int Length => genericArguments?.Length ?? 0;

            #endregion

            #region IEquatable

            /// <summary>
            /// Returns true if the given object is equal to this mapping key.
            /// </summary>
            /// <param name="other">The other object.</param>
            /// <returns>
            /// True, if the given object is equal to this mapping key.
            /// </returns>
            public bool Equals(MappingKey other)
            {
                if (Length != other.Length)
                    return false;

                for (int i = 0, e = Length; i < e; ++i)
                {
                    if (genericArguments[i] != other.genericArguments[i])
                        return false;
                }
                return true;
            }

            #endregion

            #region Object

            /// <summary>
            /// Returns true if the given object is equal to this mapping key.
            /// </summary>
            /// <param name="obj">The object.</param>
            /// <returns>
            /// True, if the given object is equal to this mapping key.
            /// </returns>
            public override bool Equals(object obj) =>
                obj is MappingKey entry && Equals(entry);

            /// <summary>
            /// Returns the hash code of this instance.
            /// </summary>
            /// <returns>The hash code of this instance.</returns>
            public override int GetHashCode()
            {
                int result = Length;
                if (genericArguments != null)
                {
                    foreach (var type in genericArguments)
                        result ^= type.GetHashCode();
                }
                return result;
            }

            /// <summary>
            /// Returns the string representation of this mapping key.
            /// </summary>
            /// <returns>The string representation of this mapping key.</returns>
            public override string ToString() => nameof(MappingKey);

            #endregion

            #region Operators

            /// <summary>
            /// Returns true if both mapping keys are identical.
            /// </summary>
            /// <param name="first">The first mapping key.</param>
            /// <param name="second">The second mapping key.</param>
            /// <returns>True, if both mapping keys are identical.</returns>
            public static bool operator ==(MappingKey first, MappingKey second) =>
                first.Equals(second);

            /// <summary>
            /// Returns true if both mapping keys are not identical.
            /// </summary>
            /// <param name="first">The first mapping key.</param>
            /// <param name="second">The second mapping key.</param>
            /// <returns>True, if both mapping keys are not identical.</returns>
            public static bool operator !=(MappingKey first, MappingKey second) =>
                !first.Equals(second);

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new intrinsic implementation.
        /// </summary>
        /// <param name="implementation">The intrinsic implementation.</param>
        internal IntrinsicMapping(IntrinsicImplementation implementation)
        {
            Debug.Assert(implementation != null, "Invalid implementation");

            Implementation = implementation;
            TargetMethod = implementation.TargetMethod;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated intrinsic implementation.
        /// </summary>
        public IntrinsicImplementation Implementation { get; }

        /// <summary>
        /// Returns the associated backend type.
        /// </summary>
        public BackendType BackendType => Implementation.BackendType;

        /// <summary>
        /// Returns the associated implementation mode.
        /// </summary>
        public IntrinsicImplementationMode Mode => Implementation.Mode;

        /// <summary>
        /// Returns the associated target method.
        /// </summary>
        protected MethodInfo TargetMethod { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Resolves the target method (if any).
        /// </summary>
        /// <typeparam name="TResolver">The generic argument resolver type.</typeparam>
        /// <param name="resolver">The argument resolver.</param>
        /// <param name="genericArguments">
        /// The resolved generic arguments (if any).
        /// </param>
        /// <returns>The resolved target method (if any).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected MethodInfo ResolveTarget<TResolver>(
            TResolver resolver,
            out Type[] genericArguments)
            where TResolver : struct, IGenericArgumentResolver
        {
            genericArguments = resolver.ResolveGenericArguments();
            if (!TargetMethod.IsGenericMethod)
                return TargetMethod;
            Debug.Assert(TargetMethod.IsGenericMethod);
            return TargetMethod.MakeGenericMethod(genericArguments);
        }

        /// <summary>
        /// Resolves the redirection method (if any).
        /// </summary>
        /// <typeparam name="TResolver">The generic argument resolver type.</typeparam>
        /// <param name="resolver">The argument resolver.</param>
        /// <param name="genericMapping">The resolved generic mapping key.</param>
        /// <returns>The resolved redirection method (if any).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodInfo ResolveRedirect<TResolver>(
            TResolver resolver,
            out MappingKey genericMapping)
            where TResolver : struct, IGenericArgumentResolver
        {
            Debug.Assert(Mode == IntrinsicImplementationMode.Redirect);
            var result = ResolveTarget(resolver, out var args);
            genericMapping = new MappingKey(args);
            return result;
        }

        #endregion
    }

    /// <summary>
    /// Represents a single intrinsic implementation mapping.
    /// </summary>
    /// <typeparam name="TDelegate">The implementation delegate type.</typeparam>
    public sealed class IntrinsicMapping<TDelegate> : IntrinsicMapping
        where TDelegate : Delegate
    {
        #region Instance

        private readonly Dictionary<MappingKey, Method> implementationMapping;
        private readonly Dictionary<MappingKey, TDelegate> delegateMapping;

        /// <summary>
        /// Constructs a new intrinsic implementation.
        /// </summary>
        /// <param name="implementation">The intrinsic implementation.</param>
        internal IntrinsicMapping(IntrinsicImplementation implementation)
            : base(implementation)
        {
            switch (implementation.Mode)
            {
                case IntrinsicImplementationMode.Redirect:
                    implementationMapping = new Dictionary<MappingKey, Method>();
                    break;
                case IntrinsicImplementationMode.GenerateCode:
                    if (!TargetMethod.IsGenericMethod)
                    {
                        CodeGenerator = TargetMethod.CreateDelegate(typeof(TDelegate))
                            as TDelegate;
                    }
                    else
                    {
                        delegateMapping = new Dictionary<MappingKey, TDelegate>();
                    }
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated default code generator (if any).
        /// </summary>
        private TDelegate CodeGenerator { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Provides the given implementation.
        /// </summary>
        /// <param name="genericMapping">The generic mapping key.</param>
        /// <param name="implementation">The implementation to provide.</param>
        internal void ProvideImplementation(
            MappingKey genericMapping,
            Method implementation)
        {
            Debug.Assert(
                Mode == IntrinsicImplementationMode.Redirect,
                "Invalid redirection");
            Debug.Assert(
                implementation != null,
                "Invalid implementation");

            implementationMapping[genericMapping] = implementation;
        }

        /// <summary>
        /// Resolves the redirection method (if any).
        /// </summary>
        /// <typeparam name="TResolver">The generic argument resolver type.</typeparam>
        /// <param name="resolver">The argument resolver.</param>
        /// <returns>The resolved redirection method (if any).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Method ResolveImplementation<TResolver>(TResolver resolver)
            where TResolver : struct, IGenericArgumentResolver
        {
            Debug.Assert(Mode == IntrinsicImplementationMode.Redirect);
            var genericArguments = resolver.ResolveGenericArguments();
            var key = new MappingKey(genericArguments);
            return implementationMapping[key];
        }

        /// <summary>
        /// Resolves the code-generation method (if any).
        /// </summary>
        /// <typeparam name="TResolver">The generic argument resolver type.</typeparam>
        /// <param name="resolver">The argument resolver.</param>
        /// <returns>The resolved code-generation method (if any).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TDelegate ResolveCodeGenerator<TResolver>(TResolver resolver)
            where TResolver : struct, IGenericArgumentResolver
        {
            Debug.Assert(Mode == IntrinsicImplementationMode.GenerateCode);
            if (CodeGenerator != null)
                return CodeGenerator;

            var resolvedMethod = ResolveTarget(resolver, out var genericArguments);
            Debug.Assert(genericArguments != null, "Invalid generic arguments");
            lock (delegateMapping)
            {
                var key = new MappingKey(genericArguments);
                if (!delegateMapping.TryGetValue(key, out var codeGenerator))
                {
                    codeGenerator = resolvedMethod.CreateDelegate(typeof(TDelegate))
                        as TDelegate;
                    delegateMapping.Add(key, codeGenerator);
                }
                return codeGenerator;
            }
        }

        #endregion
    }
}

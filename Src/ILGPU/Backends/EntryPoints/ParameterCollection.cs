// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ParameterCollection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPU.Backends.EntryPoints
{
    /// <summary>
    /// The parameter specification of an entry point.
    /// </summary>
    public readonly struct ParameterCollection : IReadOnlyList<Type>
    {
        #region Nested Types

        /// <summary>
        /// Returns an enumerator to enumerate all types in the collection.
        /// </summary>
        public struct Enumerator : IEnumerator<Type>
        {
            private ImmutableArray<Type>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new parameter type enumerator.
            /// </summary>
            /// <param name="source">The source array.</param>
            internal Enumerator(ImmutableArray<Type> source)
            {
                enumerator = source.GetEnumerator();
            }

            /// <summary>
            /// Returns the current type.
            /// </summary>
            public Type Current
            {
                get
                {
                    var type = enumerator.Current;
                    return type.IsByRef ? type.GetElementType() : type;
                }
            }

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            void IDisposable.Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => enumerator.MoveNext();

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        /// <summary>
        /// Represents a parameter that is annotated with the help of the
        /// <see cref="SpecializedValue{T}"/> structure to enable dynamic specialization
        /// of kernels.
        /// </summary>
        public readonly struct SpecializedParameter
        {
            #region Instance

            /// <summary>
            /// Constructs a new specialized parameter.
            /// </summary>
            /// <param name="index">The referenced parameter index.</param>
            /// <param name="parameterType">The raw parameter type.</param>
            /// <param name="specializedType">The specialized parameter type.</param>
            internal SpecializedParameter(
                int index,
                Type parameterType,
                Type specializedType)
            {
                Index = index;
                ParameterType = parameterType;
                SpecializedType = specializedType;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parameter index.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Returns the actual parameter type.
            /// </summary>
            public Type ParameterType { get; }

            /// <summary>
            /// Returns the specialized parameter type.
            /// </summary>
            public Type SpecializedType { get; }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new parameter type collection.
        /// </summary>
        /// <param name="parameterTypes">The parameter types.</param>
        internal ParameterCollection(ImmutableArray<Type> parameterTypes)
        {
            ParameterTypes = parameterTypes;

            var specializedParameters = ImmutableArray.CreateBuilder<SpecializedParameter>(
                parameterTypes.Length);
            for (int i = 0, e = Count; i < e; ++i)
            {
                var paramType = this[i];
                if (paramType.IsSpecializedType(out var nestedType))
                {
                    specializedParameters.Add(new SpecializedParameter(
                        i,
                        nestedType,
                        paramType));
                }
            }
            SpecializedParameters = specializedParameters.ToImmutable();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of parameter types.
        /// </summary>
        public int Count => ParameterTypes.Length;

        /// <summary>
        /// Returns the desired kernel launcher parameter types (including references).
        /// </summary>
        public ImmutableArray<Type> ParameterTypes { get; }

        /// <summary>
        /// Returns the desired kernel launcher parameter types (including references).
        /// </summary>
        public ImmutableArray<SpecializedParameter> SpecializedParameters { get; }

        /// <summary>
        /// Returns true if this collection has specialized parameters.
        /// </summary>
        public bool HasSpecializedParameters => SpecializedParameters.Length > 0;

        /// <summary>
        /// Returns the underlying parameter type (without references).
        /// </summary>
        /// <param name="index">The parameter index.</param>
        /// <returns>The desired parameter type.</returns>
        public Type this[int index]
        {
            get
            {
                var type = ParameterTypes[index];
                return type.IsByRef ? type.GetElementType() : type;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the specified parameter is passed by reference.
        /// </summary>
        /// <param name="parameterIndex">The parameter index.</param>
        /// <returns>True, if the specified parameter is passed by reference.</returns>
        public bool IsByRef(int parameterIndex) => ParameterTypes[parameterIndex].IsByRef;

        /// <summary>
        /// Copies the parameter types to the given array.
        /// </summary>
        /// <param name="target">The target array.</param>
        /// <param name="offset">The target offset to copy to.</param>
        public void CopyTo(Type[] target, int offset) =>
            ParameterTypes.CopyTo(target, offset);

        #endregion

        #region IEnumerable

        /// <summary>
        /// Returns an enumerator to enumerate all types in the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(ParameterTypes);

        /// <summary>
        /// Returns an enumerator to enumerate all types in the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator<Type> IEnumerable<Type>.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns an enumerator to enumerate all types in the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}

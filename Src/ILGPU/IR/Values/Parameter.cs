// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Parameter.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a function parameter.
    /// </summary>
    public sealed class Parameter : InstantiatedValue
    {
        #region Instance

        /// <summary>
        /// Constructs a new parameter.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="type">The parameter type.</param>
        /// <param name="name">The parameter name (for debugging purposes).</param>
        internal Parameter(
            ValueGeneration generation,
            TypeNode type,
            string name)
            : base(generation)
        {
            Name = name ?? "param";
            Seal(ImmutableArray<ValueReference>.Empty, type);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parameter name (for debugging purposes).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the parameter index.
        /// </summary>
        public int Index { get; internal set; } = -1;

        #endregion

        #region Methods

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder)
        {
            // Params have already been mapped in the beginning
            return rebuilder.Rebuild(this);
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        /// <summary>
        /// Tries to resolve the parent function.
        /// </summary>
        /// <param name="function">The resolved function (or null).</param>
        /// <returns>True, iff the parent function could be resolved.</returns>
        public bool TryResolveParentFunction(out FunctionValue function)
        {
            function = null;
            foreach (var use in Uses)
            {
                function = use.Resolve() as FunctionValue;
                if (function == null)
                    continue;
                if (function.AttachedParameters[Index] == this)
                    return true;
            }
            return false;
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => Name;

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var result = Type.ToString();
            if (TryResolveParentFunction(out FunctionValue function))
                result += " @ " + function.ToReferenceString();
            return result;
        }

        /// <summary>
        /// Return the parameter string.
        /// </summary>
        /// <returns>The parameter string.</returns>
        internal string ToParameterString() =>
            $"{Type.ToString()} {ToReferenceString()}";

        #endregion
    }

    /// <summary>
    /// Represents an enumerable of actual parameters (that were not replaced).
    /// </summary>
    public readonly struct ParameterCollection : IEnumerable<Parameter>
    {
        #region Nested Types

        /// <summary>
        /// Enumerates all actual (not replaced) parameters.
        /// </summary>
        public struct Enumerator : IEnumerator<Parameter>
        {
            private readonly ImmutableArray<ValueReference> parameters;
            private ImmutableArray<ValueReference>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new parameter enumerator.
            /// </summary>
            /// <param name="arguments">The parent source array.</param>
            internal Enumerator(ImmutableArray<ValueReference> arguments)
            {
                parameters = arguments;
                enumerator = parameters.GetEnumerator();
            }

            /// <summary>
            /// Returns the current parameter.
            /// </summary>
            public Parameter Current => enumerator.Current.DirectTarget as Parameter;

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext()
            {
                while(enumerator.MoveNext())
                {
                    var target = enumerator.Current.DirectTarget;
                    Debug.Assert(target is Parameter, "Invalid parameter reference");
                    if (target.IsReplaced)
                        continue;
                    return true;
                }
                return false;
            }

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        #endregion

        private readonly ImmutableArray<ValueReference> parameters;

        /// <summary>
        /// Constructs a new parameter collection.
        /// </summary>
        /// <param name="nodeReferences">The source parameters.</param>
        internal ParameterCollection(ImmutableArray<ValueReference> nodeReferences)
        {
            parameters = nodeReferences;
        }

        /// <summary>
        /// Tries to resolve an active memory parameter.
        /// </summary>
        /// <param name="memoryParameter">The resolved memory parameter.</param>
        /// <returns>True, iff an active memory parameter could be resolved.</returns>
        public bool TryResolveMemoryParameter(out Parameter memoryParameter)
        {
            foreach (var param in this)
            {
                memoryParameter = param;
                if (param.Type.IsMemoryType)
                    return true;
            }
            memoryParameter = null;
            return false;
        }

        /// <summary>
        /// Returns an enumerator to enumerate all actual (not replaced) parameters.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(parameters);

        /// <summary>
        /// Returns an enumerator to enumerator all actual (not replaced) parameters.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator<Parameter> IEnumerable<Parameter>.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns an enumerator to enumerator all actual (not replaced) parameters.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PhiValue.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single control-flow dependend phi node.
    /// </summary>
    public sealed class PhiValue : Value
    {
        #region Nested Types

        /// <summary>
        /// Represents a single phi-value reference.
        /// </summary>
        public readonly struct Argument
        {
            /// <summary>
            /// Constructs a new argument.
            /// </summary>
            /// <param name="value">The source value.</param>
            /// <param name="predecessor">The associated predecessor.</param>
            internal Argument(
                Value value,
                int predecessor)
            {
                Value = value;
                Predecessor = predecessor;
            }

            /// <summary>
            /// Returns the associated value.
            /// </summary>
            public Value Value { get; }

            /// <summary>
            /// Returns the associated predecessor.
            /// </summary>
            public int Predecessor { get; }

            /// <summary>
            /// Implicitly converts the current argument to its associated value.
            /// </summary>
            public Value ToValue() => Value;

            /// <summary>
            /// Returns the string representation of the underlying value.
            /// </summary>
            /// <returns>The string representation of the underlying value.</returns>
            public override string ToString() => Value.ToString();

            /// <summary>
            /// Implicitly converts the given argument to its associated value.
            /// </summary>
            /// <param name="argument">The argument to convert.</param>
            public static implicit operator Value(Argument argument) => argument.Value;
        }

        /// <summary>
        /// An enumerator for arguments.
        /// </summary>
        public struct ArgumentEnumerator : IEnumerator<Argument>
        {
            #region Instance

            private int index;

            /// <summary>
            /// Constructs a new argument enumerator.
            /// </summary>
            /// <param name="phiValue">The phi value to iterate over.</param>
            internal ArgumentEnumerator(PhiValue phiValue)
            {
                Debug.Assert(phiValue != null, "Invalid phi value");

                PhiValue = phiValue;
                index = -1;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated phi value.
            /// </summary>
            public PhiValue PhiValue { get; }

            /// <summary>
            /// Returns the current argument.
            /// </summary>
            public Argument Current => new Argument(
                PhiValue.Nodes[index],
                PhiValue.Predecessors[index]);

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => ++index < PhiValue.Nodes.Length;

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            #endregion
        }

        /// <summary>
        /// Represents a read-only collection of phi <see cref="Argument"/> values.
        /// </summary>
        public readonly struct ArgumentCollection : IReadOnlyCollection<Argument>
        {
            #region Instance

            internal ArgumentCollection(PhiValue phiValue)
            {
                Debug.Assert(phiValue != null, "Invalid phi value");
                PhiValue = phiValue;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated phi value.
            /// </summary>
            public PhiValue PhiValue { get; }

            /// <summary>
            /// Returns all associated values.
            /// </summary>
            public ImmutableArray<ValueReference> Values => PhiValue.Nodes;

            /// <summary>
            /// Returns an array of all referenced predecessors.
            /// </summary>
            public ImmutableArray<int> Predecessors => PhiValue.Predecessors;

            /// <summary>
            /// Returns the number of arguments.
            /// </summary>
            public int Count => Values.Length;

            /// <summary>
            /// Returns the i-th phi argument.
            /// </summary>
            /// <param name="index">The argument index.</param>
            /// <returns>The i-th phi argument.</returns>
            public Argument this[int index] => new Argument(
                Values[index],
                Predecessors[index]);

            #endregion

            #region Methods

            /// <summary>
            /// Returns a argument enumerator.
            /// </summary>
            /// <returns>The resolved enumerator.</returns>
            public ArgumentEnumerator GetEnumerator() => new ArgumentEnumerator(PhiValue);

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            IEnumerator<Argument> IEnumerable<Argument>.GetEnumerator() => GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        /// <summary>
        /// A phi builder.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1710: IdentifiersShouldHaveCorrectSuffix",
            Justification = "This is the correct name of the current entity")]
        public sealed class Builder : IReadOnlyCollection<ValueReference>
        {
            #region Instance

            private readonly ImmutableArray<ValueReference>.Builder arguments;
            private readonly ImmutableArray<int>.Builder predecessors;
            private readonly HashSet<Value> argumentSet;

            /// <summary>
            /// Constructs a new phi builder.
            /// </summary>
            /// <param name="phiValue">The phi value.</param>
            internal Builder(PhiValue phiValue)
            {
                Debug.Assert(phiValue != null, "Invalid phi value");
                PhiValue = phiValue;

                arguments = ImmutableArray.CreateBuilder<ValueReference>();
                predecessors = ImmutableArray.CreateBuilder<int>();
                argumentSet = new HashSet<Value>
                {
                    phiValue
                };
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated phi value.
            /// </summary>
            public PhiValue PhiValue { get; }

            /// <summary>
            /// Returns the node type.
            /// </summary>
            public TypeNode Type => PhiValue.PhiType;

            /// <summary>
            /// Returns the number of attached arguments.
            /// </summary>
            public int Count => arguments.Count;

            /// <summary>
            /// Returns the i-th argument.
            /// </summary>
            /// <param name="index">The argument index.</param>
            /// <returns>The resolved argument.</returns>
            public ValueReference this[int index] => arguments[index];

            /// <summary>
            /// Returns the parent basic block.
            /// </summary>
            public BasicBlock BasicBlock => PhiValue.BasicBlock;

            #endregion

            #region Methods

            /// <summary>
            /// Adds the given argument.
            /// </summary>
            /// <param name="value">The argument value to add.</param>
            /// <param name="predecessor">
            /// The associated predecessor from which to resolve the value from.
            /// </param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddArgument(
                Value value,
                int predecessor)
            {
                Debug.Assert(value != null, "Invalid phi argument");
                Debug.Assert(value.Type == Type, "Incompatible phi argument");
                Debug.Assert(predecessor >= 0, "Invalid predecessor value");

                if (argumentSet.Add(value))
                {
                    arguments.Add(value);
                    predecessors.Add(predecessor);
                }
            }

            /// <summary>
            /// Seals this phi node.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public PhiValue Seal()
            {
                PhiValue.SealPhiArguments(
                    arguments.ToImmutable(),
                    predecessors.ToImmutable());
                return PhiValue;
            }

            #endregion

            #region IEnumerable

            /// <summary cref="IEnumerable{T}.GetEnumerator"/>
            public IEnumerator<ValueReference> GetEnumerator() => arguments.GetEnumerator();

            /// <summary cref="IEnumerable.GetEnumerator"/>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new phi node.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="type">The phi type.</param>
        internal PhiValue(BasicBlock basicBlock, TypeNode type)
            : base(basicBlock, type)
        {
            Debug.Assert(type != null, "Invalid type");
            Debug.Assert(!type.IsVoidType, "Invalid void type");

            Predecessors = ImmutableArray<int>.Empty;
            PhiType = type;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the basic phi type.
        /// </summary>
        public TypeNode PhiType { get; }

        /// <summary>
        /// Returns an array of all referenced predecessors.
        /// </summary>
        /// <remarks>
        /// Note that the i-th value is associated with the i-th predecessor.
        /// </remarks>
        public ImmutableArray<int> Predecessors { get; private set; }

        /// <summary>
        /// Returns all argument sources.
        /// </summary>
        public ArgumentCollection Arguments => new ArgumentCollection(this);

        #endregion

        #region Methods

        /// <summary>
        /// Seals the given phi arguments.
        /// </summary>
        /// <param name="phiArguments">The phi arguments.</param>
        /// <param name="predecessors">The associated predecessors.</param>
        internal void SealPhiArguments(
            ImmutableArray<ValueReference> phiArguments,
            ImmutableArray<int> predecessors)
        {
            Debug.Assert(phiArguments.Length == predecessors.Length);
            Seal(phiArguments);
            Predecessors = predecessors;
        }

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) => PhiType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder)
        {
            // Phi values have already been mapped in the beginning
            return rebuilder.Rebuild(this);
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "phi";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var result = new StringBuilder();
            result.Append(this[0]);
            result.Append(" [");
            result.Append(Predecessors[0]);
            result.Append("]");
            for (int i = 1, e = Nodes.Length; i < e; ++i)
            {
                result.Append(", ");
                result.Append(this[i]);
                result.Append(" [");
                result.Append(Predecessors[i]);
                result.Append("]");
            }
            return result.ToString();
        }

        #endregion
    }
}

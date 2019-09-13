﻿// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PhiValue.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single control-flow dependend phi node.
    /// </summary>
    public sealed class PhiValue : Value
    {
        #region Nested Types

        /// <summary>
        /// A phi builder.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1710: IdentifiersShouldHaveCorrectSuffix",
            Justification = "This is the correct name of the current entity")]
        public sealed class Builder : IReadOnlyCollection<ValueReference>
        {
            #region Instance

            private readonly ImmutableArray<ValueReference>.Builder arguments;
            private readonly ImmutableArray<NodeId>.Builder argumentBlockIds;

            /// <summary>
            /// Constructs a new phi builder.
            /// </summary>
            /// <param name="phiValue">The phi value.</param>
            internal Builder(PhiValue phiValue)
            {
                Debug.Assert(phiValue != null, "Invalid phi value");
                PhiValue = phiValue;

                arguments = ImmutableArray.CreateBuilder<ValueReference>();
                argumentBlockIds = ImmutableArray.CreateBuilder<NodeId>();
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
            /// <param name="blockId">The input block associated with the argument value.</param>
            /// <param name="value">The argument value to add.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void AddArgument(NodeId blockId, Value value)
            {
                Debug.Assert(value != null, "Invalid phi argument");
                Debug.Assert(value.Type == Type, "Incompatible phi argument");

                arguments.Add(value);
                argumentBlockIds.Add(blockId);
            }

            /// <summary>
            /// Seals this phi node.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public PhiValue Seal()
            {
                PhiValue.SealPhiArguments(argumentBlockIds.ToImmutable(), arguments.ToImmutable());
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
            : base(ValueKind.Phi, basicBlock, type)
        {
            Debug.Assert(type != null, "Invalid type");
            Debug.Assert(!type.IsVoidType, "Invalid void type");

            PhiType = type;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the basic phi type.
        /// </summary>
        public TypeNode PhiType { get; }

        /// <summary>
        /// Returns all associated block ids.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        public ImmutableArray<NodeId> NodeBlockIds { get; private set; }

        #endregion

        #region Methods

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

        /// <summary>
        /// Seals the given phi arguments.
        /// </summary>
        /// <param name="blockIds">The associated block ids</param>
        /// <param name="arguments">The phi arguments</param>
        internal void SealPhiArguments(
            ImmutableArray<NodeId> blockIds,
            ImmutableArray<ValueReference> arguments)
        {
            Debug.Assert(arguments.Length == blockIds.Length);
            Seal(arguments);
            NodeBlockIds = blockIds;
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "phi";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            if (Nodes.IsDefaultOrEmpty)
                return string.Empty;
            return string.Join(", ", Nodes);
        }

        #endregion
    }
}

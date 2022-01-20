// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Use.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UseList = ILGPU.Util.InlineList<ILGPU.IR.Values.Use>;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents the use of a single node.
    /// </summary>
    public readonly struct Use : IEquatable<Use>
    {
        #region Instance

        /// <summary>
        /// Constructs a new use.
        /// </summary>
        /// <param name="target">The target reference.</param>
        /// <param name="index">The argument index.</param>
        internal Use(Value target, int index)
        {
            Debug.Assert(target != null, "Invalid target");
            Debug.Assert(index >= 0, "Invalid index");

            Target = target;
            Index = index;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target reference.
        /// </summary>
        public Value Target { get; }

        /// <summary>
        /// Returns the argument index.
        /// </summary>
        public int Index { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Refreshes the use with up-to-date replacement information.
        /// </summary>
        /// <returns>The refreshed use.</returns>
        public Use Refresh() => new Use(Resolve(), Index);

        /// <summary>
        /// Resolves the actual node with respect to replacement information.
        /// </summary>
        /// <returns>The actual node.</returns>
        public Value Resolve() => Target.Resolve();

        /// <summary>
        /// Resolves the actual value with respect to replacement information.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>The actual value.</returns>
        public T ResolveAs<T>() where T : Value => Resolve() as T;

        #endregion

        #region IEquatable

        /// <summary>
        /// Returns true if the given use is equal to this use.
        /// </summary>
        /// <param name="other">The other use.</param>
        /// <returns>True, if the given id is equal to this use.</returns>
        public bool Equals(Use other) => other == this;

        #endregion

        #region Objects

        /// <summary>
        /// Returns true if the given object is equal to this use.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to this use.</returns>
        public override bool Equals(object obj) => obj is Use use && use == this;

        /// <summary>
        /// Returns the hash code of this use.
        /// </summary>
        /// <returns>The hash code of this use.</returns>
        public override int GetHashCode() => Target.GetHashCode() ^ Index;

        /// <summary>
        /// Returns the string representation of this use.
        /// </summary>
        /// <returns>The string representation of this use.</returns>
        public override string ToString() => "Target: " + Target + "[" + Index + "]";

        #endregion

        #region Operators

        /// <summary>
        /// Implicitly converts the given use to the associated node reference.
        /// </summary>
        /// <param name="use">The use to convert.</param>
        public static implicit operator ValueReference(Use use) => use.Target;

        /// <summary>
        /// Implicitly converts the given use to the associated node.
        /// </summary>
        /// <param name="use">The use to convert.</param>
        public static implicit operator Value(Use use) => use.Resolve();

        /// <summary>
        /// Returns true if the first and the second use are the same.
        /// </summary>
        /// <param name="first">The first use.</param>
        /// <param name="second">The second use.</param>
        /// <returns>True, if the first and the second use are the same.</returns>
        public static bool operator ==(Use first, Use second) =>
            first.Index == second.Index && first.Target == second.Target;

        /// <summary>
        /// Returns true if the first and the second use are not the same.
        /// </summary>
        /// <param name="first">The first use.</param>
        /// <param name="second">The second use.</param>
        /// <returns>True, if the first and the second use are not the same.</returns>
        public static bool operator !=(Use first, Use second) =>
            !(first == second);

        #endregion
    }

    /// <summary>
    /// Represents an enumerable of uses that point to non-replaced nodes.
    /// </summary>
    public readonly ref struct UseCollection
    {
        #region Nested Types

        /// <summary>
        /// Checks whether a given use references a phi value.
        /// </summary>
        public readonly struct HasPhiUsesPredicate : InlineList.IPredicate<Use>
        {
            /// <inheritdoc cref="InlineList.IPredicate{T}.Apply(T)"/>
            public readonly bool Apply(Use item) => item.Resolve() is PhiValue;
        }

        /// <summary>
        /// Checks whether a given use references a method call or memory value.
        /// </summary>
        public readonly struct HasSideEffectUses : InlineList.IPredicate<Use>
        {
            /// <inheritdoc cref="InlineList.IPredicate{T}.Apply(T)"/>
            public readonly bool Apply(Use item) => item.Resolve() switch
            {
                MethodCall _ => true,
                MemoryValue _ => true,
                _ => false,
            };
        }

        /// <summary>
        /// Returns an enumerator to enumerate all uses in the context
        /// of the parent scope.
        /// </summary>
        public ref struct Enumerator
        {
            private ReadOnlySpan<Use>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new use enumerator.
            /// </summary>
            /// <param name="node">The node.</param>
            /// <param name="uses">The list of all uses.</param>
            internal Enumerator(Value node, in ReadOnlySpan<Use> uses)
            {
                Node = node;
                enumerator = uses.GetEnumerator();
                Current = default;
            }

            /// <summary>
            /// Returns the node.
            /// </summary>
            public Value Node { get; }

            /// <summary>
            /// Returns the current use.
            /// </summary>
            public Use Current { get; private set; }

            /// <summary cref="IEnumerator.MoveNext"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                while (enumerator.MoveNext())
                {
                    Current = enumerator.Current;
                    if (Current.Target.IsReplaced)
                        continue;
                    return true;
                }
                return false;
            }
        }

        #endregion

        /// <summary>
        /// Constructs a new uses collection.
        /// </summary>
        /// <param name="node">The associated node.</param>
        /// <param name="uses">The set of associated uses.</param>
        internal UseCollection(Value node, in ReadOnlySpan<Use> uses)
        {
            Node = node;
            Uses = uses;
        }

        /// <summary>
        /// Returns the associated node.
        /// </summary>
        public Value Node { get; }

        /// <summary>
        /// Returns all associated uses.
        /// </summary>
        public ReadOnlySpan<Use> Uses { get; }

        /// <summary>
        /// Returns true, if the collection contains at least one use.
        /// </summary>
        public readonly bool HasAny
        {
            get
            {
                var enumerator = GetEnumerator();
                return enumerator.MoveNext();
            }
        }

        /// <summary>
        /// Returns true, if the collection contains exactly one use.
        /// </summary>
        public readonly bool HasExactlyOne
        {
            get
            {
                var enumerator = GetEnumerator();
                return enumerator.MoveNext() && !enumerator.MoveNext();
            }
        }

        /// <summary>
        /// Tries to resolve a single use.
        /// </summary>
        /// <param name="use">The resolved use reference.</param>
        /// <returns>True, if the collection contains exactly one use.</returns>
        public readonly bool TryGetSingleUse(out Use use)
        {
            use = default;
            var enumerator = GetEnumerator();
            if (!enumerator.MoveNext())
                return false;
            use = enumerator.Current;
            return !enumerator.MoveNext();
        }

        /// <summary>
        /// Returns true if any of the uses fulfills the given predicate.
        /// </summary>
        /// <typeparam name="TPredicate">The predicate type.</typeparam>
        /// <param name="predicate">The predicate to use.</param>
        /// <returns>True, if any use fulfills the given predicate.</returns>
        public readonly bool Any<TPredicate>(TPredicate predicate)
            where TPredicate : InlineList.IPredicate<Use>
        {
            foreach (Use use in this)
            {
                if (predicate.Apply(use))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if all uses reference values in the given block set.
        /// </summary>
        /// <param name="blocks">The block set to which all uses must refer to.</param>
        /// <returns>True, if all uses reference values in the given block set.</returns>
        public readonly bool AllIn(HashSet<BasicBlock> blocks)
        {
            foreach (Value use in this)
            {
                if (!blocks.Contains(use.BasicBlock))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Clones this use collection into a new one.
        /// </summary>
        /// <returns>The cloned use collection.</returns>
        public readonly UseList Clone() => Uses.ToInlineList();

        /// <summary>
        /// Returns an enumerator to enumerate all uses in the context
        /// of the parent scope.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public readonly Enumerator GetEnumerator() => new Enumerator(Node, Uses);
    }
}

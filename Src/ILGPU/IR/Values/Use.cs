// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Use.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
        /// Returns true iff the given use is equal to this use.
        /// </summary>
        /// <param name="other">The other use.</param>
        /// <returns>True, iff the given id is equal to this use.</returns>
        public bool Equals(Use other) => other == this;

        #endregion

        #region Objects

        /// <summary>
        /// Returns true iff the given object is equal to this use.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, iff the given object is equal to this use.</returns>
        public override bool Equals(object obj)
        {
            if (obj is Use use)
                return use == this;
            return false;
        }

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
        /// Returns true iff the first and the second use are the same.
        /// </summary>
        /// <param name="first">The first use.</param>
        /// <param name="second">The second use.</param>
        /// <returns>True, iff the first and the second use are the same.</returns>
        public static bool operator ==(Use first, Use second)
        {
            return first.Index == second.Index && first.Target == second.Target;
        }

        /// <summary>
        /// Returns true iff the first and the second use are not the same.
        /// </summary>
        /// <param name="first">The first use.</param>
        /// <param name="second">The second use.</param>
        /// <returns>True, iff the first and the second use are not the same.</returns>
        public static bool operator !=(Use first, Use second)
        {
            return first.Index != second.Index || first.Target != second.Target;
        }

        #endregion
    }

    /// <summary>
    /// Represents an enumerable of uses that point to non-replaced nodes.
    /// </summary>
    public readonly struct UseCollection : IEnumerable<Use>
    {
        #region Nested Types

        /// <summary>
        /// Returns an enumerator to enumerate all uses in the context
        /// of the parent scope.
        /// </summary>
        public struct Enumerator : IEnumerator<Use>
        {
            private readonly HashSet<Use> uses;
            private HashSet<Use>.Enumerator enumerator;

            /// <summary>
            /// Constructs a new use enumerator.
            /// </summary>
            /// <param name="node">The node.</param>
            /// <param name="useSet">The source set of uses.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(Value node, HashSet<Use> useSet)
            {
                Node = node;
                uses = useSet;
                enumerator = useSet.GetEnumerator();
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

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            /// <summary cref="IDisposable.Dispose"/>
            public void Dispose()
            {
                enumerator.Dispose();
            }

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

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();
        }

        #endregion

        /// <summary>
        /// Constructs a new uses collection.
        /// </summary>
        /// <param name="node">The associated node.</param>
        /// <param name="allUses">The set of associated uses.</param>
        internal UseCollection(Value node, HashSet<Use> allUses)
        {
            Node = node;
            AllUses = allUses;
        }

        /// <summary>
        /// Returns the associated node.
        /// </summary>
        public Value Node { get; }

        /// <summary>
        /// Returns all associated uses.
        /// </summary>
        private HashSet<Use> AllUses { get; }

        /// <summary>
        /// Returns true, iff the collection contains at least one use.
        /// </summary>
        public bool HasAny
        {
            get
            {
                using (var enumerator = GetEnumerator())
                {
                    return enumerator.MoveNext();
                }
            }
        }

        /// <summary>
        /// Returns true, iff the collection contains exactly one use.
        /// </summary>
        public bool HasExactlyOne
        {
            get
            {
                using (var enumerator = GetEnumerator())
                {
                    if (!enumerator.MoveNext())
                        return false;
                    return !enumerator.MoveNext();
                }
            }
        }

        /// <summary>
        /// Tries to resolve a single use.
        /// </summary>
        /// <param name="use">The resolved use reference.</param>
        /// <returns>True, iff the collection contains exactly one use.</returns>
        public bool TryGetSingleUse(out Use use)
        {
            use = default;
            using (var enumerator = GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return false;
                use = enumerator.Current;
                return !enumerator.MoveNext();
            }
        }

        /// <summary>
        /// Clones this use collection into a new one.
        /// </summary>
        /// <returns>The cloned use collection.</returns>
        public UseCollection Clone()
        {
            var otherUses = new HashSet<Use>(AllUses);
            return new UseCollection(Node, otherUses);
        }

        /// <summary>
        /// Returns an enumerator to enumerate all uses in the context
        /// of the parent scope.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(Node, AllUses);

        /// <summary>
        /// Returns an enumerator to enumerate all uses in the context
        /// of the parent scope.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator<Use> IEnumerable<Use>.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns an enumerator to enumerate all uses in the context
        /// of the parent scope.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

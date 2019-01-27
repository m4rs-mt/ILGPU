// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ContainerType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Text;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a type node that contains children.
    /// </summary>
    public abstract class ContainerType : TypeNode
    {
        #region Instance

        /// <summary>
        /// Caches the internal hash code of all child nodes.
        /// </summary>
        private readonly int hashCode;

        /// <summary>
        /// Constructs a new type.
        /// </summary>
        /// <param name="children">The attached child nodes.</param>
        /// <param name="names">The attached child names.</param>
        /// <param name="source">The original source type (or null).</param>
        protected ContainerType(
            ImmutableArray<TypeNode> children,
            ImmutableArray<string> names,
            Type source)
        {
            Children = children;
            Names = names;
            Source = source;

            hashCode = 0;
            foreach (var child in children)
                hashCode ^= child.GetHashCode();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the original source type (may be null).
        /// </summary>
        public Type Source { get; }

        /// <summary>
        /// Returns the associated child nodes.
        /// </summary>
        public ImmutableArray<TypeNode> Children { get; }

        /// <summary>
        /// Returns the number of associated child nodes.
        /// </summary>
        public int NumChildren => Children.Length;

        /// <summary>
        /// Returns the associated name information.
        /// </summary>
        internal ImmutableArray<string> Names { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns the name of the specified child.
        /// </summary>
        /// <param name="childIndex">The child index.</param>
        /// <returns>The name of the specified child.</returns>
        public string GetName(int childIndex)
        {
            if (childIndex < 0 || childIndex >= Children.Length)
                throw new ArgumentOutOfRangeException(nameof(childIndex));
            if (childIndex < Names.Length)
                return Names[childIndex];
            return string.Empty;
        }

        #endregion

        #region Object

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() => hashCode;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj)
        {
            if (obj is ContainerType type &&
                Children.Length == type.Children.Length)
            {
                for (int i = 0, e = Children.Length; i < e; ++i)
                {
                    if (Children[i] != type.Children[i])
                        return false;
                }
                return true;
            }
            return false;
        }

        /// <summary cref="TypeNode.ToString()"/>
        public override string ToString()
        {
            if (Source != null)
                return Source.GetStringRepresentation();

            var result = new StringBuilder();
            result.Append(ToPrefixString());
            result.Append('<');

            if (Children.Length > 0)
            {
                for (int i = 0, e = Children.Length; i < e; ++i)
                {
                    result.Append(Children[i].ToString());
                    var name = GetName(i);
                    if (!string.IsNullOrEmpty(name))
                    {
                        result.Append(' ');
                        result.Append(name);
                    }
                    if (i + 1 < e)
                        result.Append(", ");
                }
            }
            result.Append('>');
            return result.ToString();
        }

        #endregion
    }
}

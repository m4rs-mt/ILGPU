// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRTypeRebuilder.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// Represents an IR rebuilder to rebuild parts of the IR.
    /// </summary>
    public class IRTypeRebuilder
    {
        #region Instance

        /// <summary>
        /// Maps old type nodes to new type nodes.
        /// </summary>
        private readonly Dictionary<TypeNode, TypeNode> toNewTypeMapping;

        /// <summary>
        /// Constructs a new IR type rebuilder.
        /// </summary>
        /// <param name="builder">The parent builder.</param>
        /// <param name="keepTypes">True, if the types should be kept.</param>
        internal IRTypeRebuilder(IRBuilder builder, bool keepTypes)
        {
            Debug.Assert(builder != null, "Invalid builder");
            Builder = builder;
            if (!keepTypes)
                toNewTypeMapping = new Dictionary<TypeNode, TypeNode>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated builder.
        /// </summary>
        public IRBuilder Builder { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Rebuilds to given source type using lookup tables.
        /// </summary>
        /// <param name="source">The source node.</param>
        /// <returns>The new type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypeNode Rebuild(TypeNode source)
        {
            Debug.Assert(source != null, "Invalid type to rebuild");

            if (toNewTypeMapping == null)
                return source;

            if (!toNewTypeMapping.TryGetValue(source, out TypeNode type))
            {
                type = source.Rebuild(Builder, this);
                toNewTypeMapping.Add(source, type);
            }
            return type;
        }

        #endregion
    }
}

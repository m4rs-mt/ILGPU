// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRMappingContext.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ILGPU.IR.Serialization
{
    /// <summary>
    /// Wraps several tables containing the information
    /// necessary to deserialize an <see cref="IRContext"/> instance.
    /// </summary>
    public interface IIRMappingContext
    {
        /// <summary>
        /// Returns a static mapping of all discovered IR methods.
        /// </summary>
        IReadOnlyDictionary<long, Method> Methods { get; }

        /// <summary>
        /// Returns a static mapping of all discovered IR blocks.
        /// </summary>
        IReadOnlyDictionary<long, BasicBlock> Blocks { get; }

        /// <summary>
        /// Returns a static mapping of all discovered IR types.
        /// </summary>
        IReadOnlyDictionary<long, TypeNode> Types { get; }

        /// <summary>
        /// Returns a dynamic mapping of all currently deserialized IR values.
        /// </summary>
        IDictionary<long, Value> Values { get; }
    }

    /// <summary>
    /// Concrete default implementation of <see cref="IIRMappingContext"/>.
    /// </summary>
    public sealed class IRMappingContext : IIRMappingContext
    {
        private readonly ConcurrentDictionary<long, Method> methods;

        private readonly ConcurrentDictionary<long, BasicBlock> blocks;

        private readonly ConcurrentDictionary<long, TypeNode> types;

        private readonly ConcurrentDictionary<long, Value> values;

        /// <inheritdoc/>
        public IReadOnlyDictionary<long, Method> Methods => methods;

        /// <inheritdoc/>
        public IReadOnlyDictionary<long, BasicBlock> Blocks => blocks;

        /// <inheritdoc/>
        public IReadOnlyDictionary<long, TypeNode> Types => types;

        /// <inheritdoc/>
        public IDictionary<long, Value> Values => values;

        /// <summary>
        /// Initializes a new, empty mapping context.
        /// </summary>
        /// <param name="methods">
        /// The mapping to use for IR method instances.
        /// </param>
        /// <param name="blocks">
        /// The mapping to use for IR block instances.
        /// </param>
        /// <param name="types">
        /// The mapping to use for IR type instances.
        /// </param>
        public IRMappingContext(
            IReadOnlyDictionary<long, Method> methods,
            IReadOnlyDictionary<long, BasicBlock> blocks,
            IReadOnlyDictionary<long, TypeNode> types)
        {
            this.methods = new ConcurrentDictionary<long, Method>(methods);
            this.blocks = new ConcurrentDictionary<long, BasicBlock>(blocks);
            this.types = new ConcurrentDictionary<long, TypeNode>(types);
            values = new ConcurrentDictionary<long, Value>();
        }
    }
}

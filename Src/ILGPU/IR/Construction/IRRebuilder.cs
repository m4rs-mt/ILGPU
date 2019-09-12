// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: IRRebuilder.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// Represents an IR rebuilder to rebuild parts of the IR.
    /// </summary>
    public sealed class IRRebuilder
    {
        #region Instance

        /// <summary>
        /// Maps source methods to target methods.
        /// </summary>
        private readonly Method.MethodMapping methodMapping;

        /// <summary>
        /// Maps old blocks to new block builders.
        /// </summary>
        private readonly Dictionary<BasicBlock, BasicBlock.Builder> blockMapping =
            new Dictionary<BasicBlock, BasicBlock.Builder>();

        /// <summary>
        /// Maps old block is to new block ids.
        /// </summary>
        private readonly Dictionary<NodeId, NodeId> blockIdMapping =
            new Dictionary<NodeId, NodeId>();

        /// <summary>
        /// Maps old phi nodes to new phi builders.
        /// </summary>
        private readonly List<(PhiValue, PhiValue.Builder)> phiMapping =
            new List<(PhiValue, PhiValue.Builder)>();

        /// <summary>
        /// Maps old nodes to new nodes.
        /// </summary>
        private readonly Dictionary<Value, Value> valueMapping =
            new Dictionary<Value, Value>();

        /// <summary>
        /// Constructs a new IR rebuilder.
        /// </summary>
        /// <param name="builder">The parent builder.</param>
        /// <param name="parameterMapping">The used parameter remapping.</param>
        /// <param name="scope">The parent scope.</param>
        /// <param name="methodRemapping">The used method remapping.</param>
        internal IRRebuilder(
            Method.Builder builder,
            Method.ParameterMapping parameterMapping,
            Scope scope,
            Method.MethodMapping methodRemapping)
        {
            Debug.Assert(builder != null, "Invalid method builder");
            Debug.Assert(scope != null, "Invalid scope");

            methodMapping = methodRemapping;
            Builder = builder;
            Scope = scope;

            // Insert parameters into local mapping
            foreach (var param in scope.Method.Parameters)
                valueMapping.Add(param, parameterMapping[param]);

            // Create blocks and prepare phi nodes
            foreach (var block in scope)
            {
                // Setup debug information for the current block
                Builder.SequencePoint = block.SequencePoint;

                var newBlock = builder.CreateBasicBlock(block.Name);
                blockMapping.Add(block, newBlock);
                blockIdMapping.Add(block.Id, newBlock.BasicBlock.Id);

                foreach (Value value in block)
                {
                    if (value is PhiValue phiValue)
                    {
                        Debug.Assert(!valueMapping.ContainsKey(value), "Phi already found");

                        // Setup debug information for the current value
                        Builder.SequencePoint = value.SequencePoint;
                        var phiBuilder = newBlock.CreatePhi(phiValue.Type);

                        phiMapping.Add((phiValue, phiBuilder));
                        Map(phiValue, phiBuilder.PhiValue);
                    }
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated method builder.
        /// </summary>
        public Method.Builder Builder { get; }

        /// <summary>
        /// Returns the associated scope.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Returns the target entry block.
        /// </summary>
        public BasicBlock.Builder EntryBlock => blockMapping[Scope.EntryBlock];

        /// <summary>
        /// Gets or sets the current block builder.
        /// </summary>
        private BasicBlock.Builder CurrentBlock { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Rebuilds all values.
        /// </summary>
        /// <returns>An array of exit blocks and their return values.</returns>
        public ImmutableArray<(BasicBlock.Builder, Value)> Rebuild()
        {
            var exitBlocks = ImmutableArray.CreateBuilder<(BasicBlock.Builder, Value)>(Scope.Count);

            // Rebuild all instructions
            foreach (var block in Scope)
            {
                var newBlock = blockMapping[block];
                CurrentBlock = newBlock;

                foreach (Value value in block)
                    Rebuild(value);

                var terminator = block.Terminator.Resolve();
                if (terminator is ReturnTerminator returnValue)
                {
                    var newReturnValue = Rebuild(returnValue.ReturnValue);
                    exitBlocks.Add((newBlock, newReturnValue));
                }
                else
                    Rebuild(terminator);
            }

            // Seal all phi nodes
            foreach (var (sourcePhi, targetPhiBuilder) in phiMapping)
            {
                // Append all phi arguments
                for (int i = 0, e = sourcePhi.Nodes.Length; i < e; ++i)
                {
                    var argument = sourcePhi.Nodes[i];
                    var newBlockId = blockIdMapping[sourcePhi.NodeBlockIds[i]];
                    targetPhiBuilder.AddArgument(newBlockId, valueMapping[argument]);
                }
                targetPhiBuilder.Seal();
            }

            return exitBlocks.ToImmutable();
        }

        /// <summary>
        /// Tries to lookup the new node representation of the given old node.
        /// </summary>
        /// <param name="oldNode">The old node.</param>
        /// <param name="newNode">The new node.</param>
        /// <returns>True, iff a corresponding new node could be found.</returns>
        public bool TryGetNewNode(Value oldNode, out Value newNode) =>
            valueMapping.TryGetValue(oldNode, out newNode);

        /// <summary>
        /// Maps the old node to the new node.
        /// </summary>
        /// <param name="oldNode">The old node.</param>
        /// <param name="newNode">The new node.</param>
        public void Map(Value oldNode, Value newNode)
        {
            Debug.Assert(oldNode != null, "Invalid old node");
            Debug.Assert(newNode != null, "Invalid new node");

            valueMapping[oldNode] = newNode;
        }

        /// <summary>
        /// Exports the internal node mapping to the given target dictionary.
        /// </summary>
        /// <typeparam name="TDictionary">The dictionary type.</typeparam>
        /// <param name="target">The target dictionary.</param>
        public void ExportNodeMapping<TDictionary>(TDictionary target)
            where TDictionary : IDictionary<Value, Value>
        {
            foreach (var node in valueMapping)
                target[node.Key] = node.Value;
        }

        /// <summary>
        /// Resolves a method for the given old method
        /// </summary>
        /// <param name="oldTarget">The old method.</param>
        /// <returns>The resolved method.</returns>
        public Method LookupCallTarget(Method oldTarget) =>
            methodMapping[oldTarget];

        /// <summary>
        /// Resolves a basic block builder for the given old block.
        /// </summary>
        /// <param name="oldTarget">The old basic block.</param>
        /// <returns>The resolved block builder.</returns>
        public BasicBlock LookupTarget(BasicBlock oldTarget) =>
            blockMapping[oldTarget].BasicBlock;

        /// <summary>
        /// Rebuilds to given source node using lookup tables.
        /// </summary>
        /// <param name="source">The source node.</param>
        /// <returns>The new node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Value Rebuild(Value source)
        {
            Debug.Assert(source != null, "Invalid source node");
            Debug.Assert(!source.IsReplaced, "Trying to rebuild a replaced node");

            if (TryGetNewNode(source, out Value node))
                return node;

            // Setup debug information for the source value
            Builder.SequencePoint = source.SequencePoint;

            Debug.Assert(!(source is Parameter), "Invalid recursive parameter rebuilding process");
            node = source.Rebuild(CurrentBlock, this);
            Map(source, node);
            return node;
        }

        /// <summary>
        /// Rebuilds to given source node using lookup tables and
        /// returns the resolved casted to a specific type.
        /// </summary>
        /// <typeparam name="T">The target type to cast the new node to.</typeparam>
        /// <param name="source">The source node.</param>
        /// <returns>The new node.</returns>
        public T RebuildAs<T>(Value source)
            where T : Value =>
            Rebuild(source) as T;

        #endregion
    }
}

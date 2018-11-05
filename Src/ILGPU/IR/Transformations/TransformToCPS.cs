// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: TransformToCPS.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static ILGPU.IR.Types.StructureType;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a CPS transformation.
    /// </summary>
    public sealed class TransformToCPS : UnorderedTransformation
    {
        /// <summary>
        /// The desired transformations that should run after
        /// applying this transformation.
        /// </summary>
        private const TransformationFlags FollowUpFlags =
            TransformationFlags.OptimizeParameters |
            TransformationFlags.MergeCallChains |
            TransformationFlags.InferAddressSpaces |
            TransformationFlags.DestroyStructures;

        /// <summary>
        /// Constructs a new CPS transformer.
        /// </summary>
        public TransformToCPS()
            : base(TransformationFlags.TransformToCPS, FollowUpFlags, true, false)
        {
            RequiredTransformationFlags =
                TransformationFlags.Inlining |
                TransformationFlags.InferAddressSpaces;
        }

        /// <summary>
        /// Visits a single memory chain and tries to convert load and store
        /// operations into CPS form.
        /// </summary>
        readonly struct LoadStoreHandler : Scope.IMemoryChainVisitor
        {
            /// <summary>
            /// Creates a new load-store handler.
            /// </summary>
            /// <param name="scope">The current scope.</param>
            /// <param name="allocas">The set of alloca nodes.</param>
            public LoadStoreHandler(
                Scope scope,
                HashSet<Alloca> allocas)
            {
                Scope = scope;
                Allocas = allocas;
            }

            /// <summary>
            /// Returns the current scope.
            /// </summary>
            public Scope Scope { get; }

            /// <summary>
            /// Returns the set of allocas that can be converted into CPS form.
            /// </summary>
            public HashSet<Alloca> Allocas { get; }

            /// <summary cref="Scope.IMemoryChainVisitor.Visit(MemoryRef)"/>
            public bool Visit(MemoryRef memoryRef) => true;

            /// <summary cref="Scope.IMemoryChainVisitor.Visit(MemoryValue)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Visit(MemoryValue memoryValue)
            {
                if (memoryValue is Alloca alloca)
                {
                    if (!RequiresAddress(memoryValue))
                        Allocas.Add(alloca);

                }
                return true;
            }

            /// <summary>
            /// Returns false iff the given node cannot be transformed
            /// into a SSA variable.
            /// </summary>
            /// <param name="node">The current node.</param>
            /// <returns>
            /// False, iff the given node cannot be transformed into a SSA variable.
            /// </returns>
            private bool RequiresAddress(Value node)
            {
                var uses = Scope.GetUses(node);
                foreach (var use in uses)
                {
                    switch (use.Resolve())
                    {
                        case MemoryRef _:
                        case Load _:
                        case Store _:
                            continue;
                        case LoadFieldAddress loadFieldAddress:
                            if (RequiresAddress(loadFieldAddress))
                                return true;
                            break;
                        default:
                            return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Visits all memory chains using a <see cref="LoadStoreHandler"/>.
        /// </summary>
        readonly struct ChainVisitor : Scope.IMultiMemoryChainVisitor<LoadStoreHandler>
        {
            /// <summary>
            /// Creates a new chain visitor.
            /// </summary>
            /// <param name="scope">The current scope.</param>
            /// <param name="allocas">The set of alloca nodes.</param>
            public ChainVisitor(Scope scope, HashSet<Alloca> allocas)
            {
                Scope = scope;
                Allocas = allocas;
            }

            /// <summary>
            /// Returns the current scope.
            /// </summary>
            public Scope Scope { get; }

            /// <summary>
            /// Returns the set of allocas that can be converted into CPS form.
            /// </summary>
            public HashSet<Alloca> Allocas { get; }

            /// <summary cref="Scope.IMultiMemoryChainVisitor{TVisitor}.VisitMemoryChain(FunctionValue, Parameter)"/>
            public LoadStoreHandler VisitMemoryChain(FunctionValue functionValue, Parameter memoryParameter)
            {
                return new LoadStoreHandler(Scope, Allocas);
            }
        }

        /// <summary cref="UnorderedTransformation.PerformTransformation(IRBuilder, TopLevelFunction)"/>
        protected override bool PerformTransformation(
            IRBuilder builder,
            TopLevelFunction topLevelFunction)
        {
            var scope = Scope.Create(builder, topLevelFunction);

            var allocas = new HashSet<Alloca>();
            var visitor = new ChainVisitor(scope, allocas);
            scope.VisitMemoryChains<ChainVisitor, LoadStoreHandler>(ref visitor);

            if (allocas.Count < 1)
                return false;

            var cfg = CFG.Create(scope);
            var placement = Placement.CreateCSEPlacement(cfg);
            var cpsBuilder = CPSBuilder<CFGNode, CFGNode.Enumerator, Value>.Create(
                builder,
                cfg.EntryNode);

            // Convert to SSA loads and stores
            var convertedNodes = new Dictionary<Value, FieldRef>();
            var reversePostOrder = cpsBuilder.ComputeReversePostOrder();
            foreach (var cfgNode in reversePostOrder)
            {
                if (!cpsBuilder.ProcessAndSeal(cfgNode))
                    continue;

                using (var placementEnumerator = placement[cfgNode])
                {
                    while (placementEnumerator.MoveNext())
                    {
                        var current = placementEnumerator.Current;
                        switch (current)
                        {
                            case Alloca alloca:
                                if (allocas.Contains(alloca))
                                {
                                    cpsBuilder.SetValue(
                                        cfgNode,
                                        alloca,
                                        builder.CreateNull(alloca.AllocaType));
                                    convertedNodes.Add(alloca, new FieldRef(alloca));
                                }
                                break;
                            case Load load:
                                if (convertedNodes.TryGetValue(load.Source, out FieldRef loadRef))
                                {
                                    var value = cpsBuilder.GetValue(cfgNode, loadRef.Source);
                                    value = builder.CreateGetField(value, loadRef.AccessChain);

                                    MemoryRef.Unlink(load);
                                    load.Replace(value);
                                }
                                else
                                {
                                    var uses = scope.GetUses(load);
                                    if (uses.HasExactlyOneMemoryRef)
                                        MemoryRef.Unlink(load);
                                }
                                break;
                            case Store store:
                                if (convertedNodes.TryGetValue(store.Target, out FieldRef storeRef))
                                {
                                    var value = cpsBuilder.GetValue(cfgNode, storeRef.Source);
                                    value = builder.CreateSetField(value, storeRef.AccessChain, store.Value);

                                    cpsBuilder.SetValue(cfgNode, storeRef.Source, value);
                                    MemoryRef.Unlink(store);
                                }
                                break;
                            case LoadFieldAddress loadFieldAddress:
                                if (convertedNodes.TryGetValue(loadFieldAddress.Source, out FieldRef fieldRef))
                                    convertedNodes.Add(loadFieldAddress, fieldRef.Access(loadFieldAddress.FieldIndex));
                                break;
                        }
                    }
                }
            }

            // Remove allocas
            foreach (var node in allocas)
                MemoryRef.Unlink(node as Alloca);

            // Finish the building process
            cpsBuilder.Finish();

            return true;
        }
    }
}

// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: SSAConstruction.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static ILGPU.IR.Types.StructureType;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Performs ah SSA construction transformation.
    /// </summary>
    public sealed class SSAConstruction : UnorderedTransformation
    {
        /// <summary>
        /// Constructs a new SSA construction pass.
        /// </summary>
        public SSAConstruction() { }

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            // Create scope and try to find SSA-convertible alloca nodes
            var scope = builder.CreateScope(ScopeFlags.AddAlreadyVisitedNodes);
            var allocas = FindConvertibleAllocas(scope);
            if (allocas.Count < 1)
                return false;

            // Perform SSA construction
            var cfg = scope.CreateCFG();
            var ssaBuilder = SSABuilder<Value>.Create(builder, cfg);

            var convertedNodes = new Dictionary<Value, FieldRef>();
            foreach (var block in scope)
            {
                if (!ssaBuilder.ProcessAndSeal(block))
                    continue;

                var blockBuilder = builder[block];
                foreach (var valueEntry in blockBuilder)
                {
                    // Move insert position to the current instruction
                    blockBuilder.SetupInsertPosition(valueEntry);

                    Value value = valueEntry;
                    switch (value)
                    {
                        case Alloca alloca:
                            if (allocas.Contains(alloca))
                            {
                                ssaBuilder.SetValue(
                                    block,
                                    alloca,
                                    blockBuilder.CreateNull(alloca.AllocaType));

                                convertedNodes.Add(alloca, new FieldRef(alloca));
                                blockBuilder.Remove(alloca);
                            }
                            break;
                        case Load load:
                            if (convertedNodes.TryGetValue(load.Source, out FieldRef loadRef))
                            {
                                var ssaValue = ssaBuilder.GetValue(block, loadRef.Source);
                                ssaValue = blockBuilder.CreateGetField(ssaValue, loadRef.AccessChain);

                                load.Replace(ssaValue);
                                blockBuilder.Remove(load);
                            }
                            else if (!load.Uses.HasAny)
                            {
                                blockBuilder.Remove(load);
                            }
                            break;
                        case Store store:
                            if (convertedNodes.TryGetValue(store.Target, out FieldRef storeRef))
                            {
                                var ssaValue = ssaBuilder.GetValue(block, storeRef.Source);
                                ssaValue = blockBuilder.CreateSetField(
                                    ssaValue,
                                    storeRef.AccessChain,
                                    store.Value);

                                ssaBuilder.SetValue(block, storeRef.Source, ssaValue);
                                blockBuilder.Remove(store);
                            }
                            break;
                        case LoadFieldAddress loadFieldAddress:
                            if (convertedNodes.TryGetValue(loadFieldAddress.Source, out FieldRef fieldRef))
                            {
                                convertedNodes.Add(
                                    loadFieldAddress,
                                    fieldRef.Access(loadFieldAddress.FieldIndex));
                                blockBuilder.Remove(loadFieldAddress);
                            }
                            break;
                        case AddressSpaceCast addressSpaceCast:
                            if (convertedNodes.TryGetValue(addressSpaceCast.Value, out FieldRef castRef))
                            {
                                convertedNodes.Add(
                                    addressSpaceCast,
                                    castRef);
                                blockBuilder.Remove(addressSpaceCast);
                            }
                            break;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Finds all SSA-convertible alloca nodes.
        /// </summary>
        /// <param name="scope">The scope in which to search for allocas.</param>
        /// <returns>A set containing all detected alloca nodes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static HashSet<Alloca> FindConvertibleAllocas(Scope scope)
        {
            var result = new HashSet<Alloca>();
            foreach (Value value in scope.Values)
            {
                if (value is Alloca alloca && !RequiresAddress(alloca))
                    result.Add(alloca);
            }

            return result;
        }

        /// <summary>
        /// Returns false iff the given node cannot be transformed
        /// into a SSA variable.
        /// </summary>
        /// <param name="node">The current node.</param>
        /// <returns>
        /// False, iff the given node cannot be transformed into a SSA variable.
        /// </returns>
        private static bool RequiresAddress(Value node)
        {
            foreach (var use in node.Uses)
            {
                switch (use.Resolve())
                {
                    case Load _:
                    case Store _:
                        continue;
                    case LoadFieldAddress loadFieldAddress:
                        if (RequiresAddress(loadFieldAddress))
                            return true;
                        break;
                    case AddressSpaceCast addressSpaceCast:
                        if (RequiresAddress(addressSpaceCast))
                            return true;
                        break;
                    default:
                        return true;
                }
            }
            return false;
        }
    }
}

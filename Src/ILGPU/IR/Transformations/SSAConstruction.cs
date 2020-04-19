// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: SSAConstruction.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Construction;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Performs ah SSA construction transformation.
    /// </summary>
    public sealed class SSAConstruction : UnorderedTransformation
    {
        #region Utility Methods

        /// <summary>
        /// Returns false if the given node cannot be transformed into an SSA value.
        /// </summary>
        /// <param name="node">The current node.</param>
        /// <returns>
        /// False, if the given node cannot be transformed into an SSA value.
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
                    case LoadFieldAddress lfa:
                        if (RequiresAddress(lfa))
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

        #endregion

        #region Nested Types

        /// <summary>
        /// Data that is using during SSA construction.
        /// </summary>
        private readonly struct ConstructionData
        {
            /// <summary>
            /// Initializes the data structure.
            /// </summary>
            public ConstructionData(HashSet<Alloca> allocas)
            {
                Allocas = allocas;
                ConvertedValues = new Dictionary<Value, FieldRef>();
            }

            /// <summary>
            /// The set of all allocas to be converted into SSA value.
            /// </summary>
            private HashSet<Alloca> Allocas { get; }

            /// <summary>
            /// Maps converted values to their associated field references.
            /// </summary>
            private Dictionary<Value, FieldRef> ConvertedValues { get; }

            /// <summary>
            /// Returns true if the given alloca should be converted.
            /// </summary>
            /// <param name="alloca">The alloca to check.</param>
            public bool ContainsAlloca(Alloca alloca) => Allocas.Contains(alloca);

            /// <summary>
            /// Tries to get a converted value entry.
            /// </summary>
            /// <param name="value">The value to lookup.</param>
            /// <param name="fieldRef">The resolved field reference (if any).</param>
            public bool TryGetConverted(Value value, out FieldRef fieldRef) =>
                ConvertedValues.TryGetValue(value, out fieldRef);

            /// <summary>
            /// Adds the given value and the field reference to the mapping of
            /// converted values.
            /// </summary>
            /// <param name="value">The value to register.</param>
            /// <param name="fieldRef">The field reference.</param>
            public void AddConverted(Value value, FieldRef fieldRef) =>
                ConvertedValues.Add(value, fieldRef);
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Converts an alloca node to its initial SSA value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ConstructionData data,
            Alloca alloca)
        {
            if (!data.ContainsAlloca(alloca))
                return;

            var initValue = context.Builder.CreateNull(alloca.AllocaType);
            context.SetValue(context.Block, alloca, initValue);

            data.AddConverted(alloca, new FieldRef(alloca));
            context.Remove(alloca);
        }

        /// <summary>
        /// Converts a load node into an SSA value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ConstructionData data,
            Load load)
        {
            if (data.TryGetConverted(load.Source, out var loadRef))
            {
                var ssaValue = context.GetValue(context.Block, loadRef.Source);
                if (!loadRef.IsDirect)
                {
                    ssaValue = context.Builder.CreateGetField(
                         ssaValue,
                         loadRef.FieldSpan);
                }

                context.ReplaceAndRemove(load, ssaValue);
            }
            else if (!load.Uses.HasAny)
            {
                context.Remove(load);
            }
        }

        /// <summary>
        /// Converts a store node into an SSA value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ConstructionData data,
            Store store)
        {
            if (!data.TryGetConverted(store.Target, out var storeRef))
                return;

            Value ssaValue = store.Value;
            if (!storeRef.IsDirect)
            {
                ssaValue = context.GetValue(context.Block, storeRef.Source);
                ssaValue = context.Builder.CreateSetField(
                    ssaValue,
                    storeRef.FieldSpan,
                    store.Value);
            }

            context.SetValue(context.Block, storeRef.Source, ssaValue);
            context.Remove(store);
        }

        /// <summary>
        /// Converts a field-address operation into an SSA binding.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ConstructionData data,
            LoadFieldAddress loadFieldAddress)
        {
            if (!data.TryGetConverted(loadFieldAddress.Source, out var fieldRef))
                return;

            data.AddConverted(
                loadFieldAddress,
                fieldRef.Access(loadFieldAddress.FieldSpan));
            context.Remove(loadFieldAddress);
        }

        /// <summary>
        /// Converts an address-space cast into an SSA binding.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ConstructionData data,
            AddressSpaceCast addressSpaceCast)
        {
            if (!data.TryGetConverted(addressSpaceCast.Value, out var castRef))
                return;

            data.AddConverted(addressSpaceCast, castRef);
            context.Remove(addressSpaceCast);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly SSARewriter<Value, ConstructionData> Rewriter =
            new SSARewriter<Value, ConstructionData>();

        /// <summary>
        /// Registers all rewriting patterns.
        /// </summary>
        static SSAConstruction()
        {
            Rewriter.Add<Alloca>(Convert);
            Rewriter.Add<Load>(Convert);
            Rewriter.Add<Store>(Convert);
            Rewriter.Add<LoadFieldAddress>(Convert);
            Rewriter.Add<AddressSpaceCast>(Convert);
        }

        #endregion

        /// <summary>
        /// Constructs a new SSA construction pass.
        /// </summary>
        public SSAConstruction() { }

        /// <summary>
        /// Applies the SSA construction transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            // Create scope and try to find SSA-convertible alloca nodes
            var scope = builder.CreateScope(ScopeFlags.AddAlreadyVisitedNodes);

            // Search for convertible allocas
            var allocas = new HashSet<Alloca>();
            scope.ForEachValue<Alloca>(alloca =>
            {
                if (!alloca.IsSimpleAllocation ||
                    alloca.AddressSpace != MemoryAddressSpace.Local ||
                    RequiresAddress(alloca))
                {
                    return;
                }

                allocas.Add(alloca);
            });
            if (allocas.Count < 1)
                return false;

            // Perform SSA construction
            var ssaBuilder = SSABuilder<Value>.Create(builder, scope);
            return Rewriter.Rewrite(ssaBuilder, new ConstructionData(allocas));
        }
    }
}

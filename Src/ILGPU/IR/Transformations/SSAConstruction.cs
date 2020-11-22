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

using ILGPU.IR.Construction;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// An abstract SSA transformation base class.
    /// </summary>
    public abstract class SSATransformationBase : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// An abstract interface that contains required methods to perform the SSA
        /// construction.
        /// </summary>
        protected interface IConstructionData
        {
            /// <summary>
            /// Returns true if the given alloca should be converted.
            /// </summary>
            /// <param name="alloca">The alloca to check.</param>
            bool ContainsAlloca(Alloca alloca);

            /// <summary>
            /// Tries to get a converted value entry.
            /// </summary>
            /// <param name="value">The value to lookup.</param>
            /// <param name="fieldRef">The resolved field reference (if any).</param>
            bool TryGetConverted(Value value, out FieldRef fieldRef);

            /// <summary>
            /// Adds the given value and the field reference to the mapping of
            /// converted values.
            /// </summary>
            /// <param name="value">The value to register.</param>
            /// <param name="fieldRef">The field reference.</param>
            void AddConverted(Value value, FieldRef fieldRef);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Returns true if the given use requires an explicit address in memory.
        /// See <see cref="RequiresAddress(Value)"/> for more information.
        /// </summary>
        /// <param name="use">The use value.</param>
        /// <returns>True, if this use requires an explicit address.</returns>
        protected static bool RequiresAddressForUse(Value use)
        {
            switch (use)
            {
                case Load _:
                case Store _:
                    break;
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
            return false;
        }

        /// <summary>
        /// Returns false if the given node cannot be transformed into an SSA value.
        /// </summary>
        /// <param name="node">The current node.</param>
        /// <returns>
        /// False, if the given node cannot be transformed into an SSA value.
        /// </returns>
        protected static bool RequiresAddress(Value node)
        {
            foreach (Value use in node.Uses)
            {
                if (RequiresAddressForUse(use))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Converts the given allocation value into its SSA representation using the
        /// initialization value provided.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void ConvertAlloca<TConstructionData>(
            SSARewriterContext<Value> context,
            TConstructionData data,
            Alloca alloca,
            Value initValue)
            where TConstructionData : IConstructionData
        {
            alloca.Assert(data.ContainsAlloca(alloca));

            // Bind the init value and remove the allocation from the block
            context.SetValue(context.Block, alloca, initValue);
            data.AddConverted(alloca, new FieldRef(alloca));
            context.Remove(alloca);
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Converts a load node into an SSA value.
        /// </summary>
        protected static void Convert<TConstructionData>(
            SSARewriterContext<Value> context,
            TConstructionData data,
            Load load)
            where TConstructionData : IConstructionData
        {
            if (data.TryGetConverted(load.Source, out var loadRef))
            {
                var ssaValue = context.GetValue(context.Block, loadRef.Source);
                if (!loadRef.IsDirect)
                {
                    ssaValue = context.Builder.CreateGetField(
                        load.Location,
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
        protected static void Convert<TConstructionData>(
            SSARewriterContext<Value> context,
            TConstructionData data,
            Store store)
            where TConstructionData : IConstructionData
        {
            if (!data.TryGetConverted(store.Target, out var storeRef))
                return;

            Value ssaValue = store.Value;
            if (!storeRef.IsDirect)
            {
                ssaValue = context.GetValue(context.Block, storeRef.Source);
                ssaValue = context.Builder.CreateSetField(
                    store.Location,
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
        protected static void Convert<TConstructionData>(
            SSARewriterContext<Value> context,
            TConstructionData data,
            LoadFieldAddress loadFieldAddress)
            where TConstructionData : IConstructionData
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
        protected static void Convert<TConstructionData>(
            SSARewriterContext<Value> context,
            TConstructionData data,
            AddressSpaceCast addressSpaceCast)
            where TConstructionData : IConstructionData
        {
            if (!data.TryGetConverted(addressSpaceCast.Value, out var castRef))
                return;

            data.AddConverted(addressSpaceCast, castRef);
            context.Remove(addressSpaceCast);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// Registers all base rewriting patterns.
        /// </summary>
        protected static void RegisterRewriters<TConstructionData>(
            SSARewriter<Value, TConstructionData> rewriter)
            where TConstructionData : IConstructionData
        {
            rewriter.Add<Load>(Convert);
            rewriter.Add<Store>(Convert);
            rewriter.Add<LoadFieldAddress>(Convert);
            rewriter.Add<AddressSpaceCast>(Convert);
        }

        #endregion
    }

    /// <summary>
    /// The base class for both the <see cref="SSAConstruction"/> and the
    /// <see cref="SSAStructureConstruction"/> classes.
    /// </summary>
    public abstract class SSAConstructionBase : SSATransformationBase
    {
        #region Nested Types

        /// <summary>
        /// A default implementation of the
        /// <see cref="SSATransformationBase.IConstructionData" /> interface.
        /// </summary>
        protected readonly struct ConstructionData : IConstructionData
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

        #region Instance

        /// <summary>
        /// Constructs a new SSA transformation pass.
        /// </summary>
        /// <param name="addressSpace">The target memory address space.</param>
        protected SSAConstructionBase(MemoryAddressSpace addressSpace)
        {
            AddressSpace = addressSpace;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the memory address space to handle.
        /// </summary>
        public MemoryAddressSpace AddressSpace { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given allocation can be transformed.
        /// </summary>
        protected virtual bool CanConvert(Method.Builder builder, Alloca alloca) =>
            alloca.AddressSpace == AddressSpace;

        /// <summary>
        /// Performs the internal SSA construction transformation.
        /// </summary>
        /// <param name="builder">The parent meethod builder.</param>
        /// <param name="rewriter">The SSA rewriter to use.</param>
        /// <returns>True, if the transformation could be applied.</returns>
        protected bool PerformTransformation(
            Method.Builder builder,
            SSARewriter<Value, ConstructionData> rewriter)
        {
            // Search for convertible allocas
            var allocas = new HashSet<Alloca>();
            builder.SourceBlocks.ForEachValue<Alloca>(alloca =>
            {
                if (!CanConvert(builder, alloca))
                    return;

                allocas.Add(alloca);
            });
            if (allocas.Count < 1)
                return false;

            // Perform SSA construction
            var ssaBuilder = SSABuilder<Value>.Create(builder);
            return rewriter.Rewrite(ssaBuilder, new ConstructionData(allocas));
        }

        #endregion
    }

    /// <summary>
    /// Performs an SSA construction transformation.
    /// </summary>
    public sealed class SSAConstruction : SSAConstructionBase
    {
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

            var initValue = context.Builder.CreateNull(
                alloca.Location,
                alloca.AllocaType);
            ConvertAlloca(context, data, alloca, initValue);
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
            RegisterRewriters(Rewriter);

            Rewriter.Add<Alloca>(Convert);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new SSA construction pass.
        /// </summary>
        public SSAConstruction() : this(MemoryAddressSpace.Local) { }

        /// <summary>
        /// Constructs a new SSA construction pass.
        /// </summary>
        /// <param name="addressSpace">The target memory address space.</param>
        public SSAConstruction(MemoryAddressSpace addressSpace)
            : base(addressSpace)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if the given allocation is a simple allocation and does not
        /// require explicit addresses.
        /// </summary>
        protected override bool CanConvert(Method.Builder builder, Alloca alloca) =>
            base.CanConvert(builder, alloca) &&
            alloca.IsSimpleAllocation &&
            !RequiresAddress(alloca);

        /// <summary>
        /// Applies the SSA construction transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            PerformTransformation(builder, Rewriter);

        #endregion
    }
}

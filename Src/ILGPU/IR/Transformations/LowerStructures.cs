// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LowerStructures.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Flags for the <see cref="LowerStructures"/> transformation.
    /// </summary>
    [Flags]
    public enum LowerStructureFlags : int
    {
        /// <summary>
        /// Default lowering flags.
        /// </summary>
        None,

        /// <summary>
        /// Lowers <see cref="Load"/> and <see cref="Store"/> instructions.
        /// </summary>
        LowerLoadStores = 1 << 0,
    }

    /// <summary>
    /// Converts structure values into separate values.
    /// </summary>
    /// <remarks>
    /// This transformation does not change function parameters and calls to other
    /// functions.
    /// </remarks>
    public sealed class LowerStructures : UnorderedTransformation
    {
        #region Utility Methods

        /// <summary>
        /// Builds a new structure value during lowering of a source value.
        /// </summary>
        private static Value AssembleStructure(
            SSARewriterContext<FieldRef> context,
            StructureType structureType,
            Value value) =>
            context.AssembleStructure(
                structureType,
                value,
                (ctx, source, fieldAccess) =>
                    // Load the currently registered SSA value
                    ctx.GetValue(ctx.Block, new FieldRef(source, fieldAccess)));

        /// <summary>
        /// Registers all structure values in the current SSA builder.
        /// </summary>
        private static void DisassembleStructure(
            SSARewriterContext<FieldRef> context,
            StructureType structureType,
            Value value) =>
            context.DisassembleStructure(
                structureType,
                value,
                (ctx, source, getField, fieldAccess) =>
                    ctx.SetValue(
                        ctx.Block,
                        new FieldRef(value, fieldAccess),
                        getField));

        /// <summary>
        /// Lowers a thread value.
        /// </summary>
        private static void LowerThreadValue<TValue, TLoweringImplementation>(
            SSARewriterContext<FieldRef> context,
            StructureType structureType,
            TValue value)
            where TValue : ThreadValue
            where TLoweringImplementation :
                LowerThreadIntrinsics.ILoweringImplementation<TValue>
        {
            // We require a single input
            var variable = AssembleStructure(
                context,
                structureType,
                value.Variable);

            // Build a new thread value using the assembled structure
            TLoweringImplementation implementation = default;
            var newValue = implementation.Lower(
                context.Builder,
                value,
                variable).ResolveAs<TValue>();

            // Disassemble the resulting structure value
            DisassembleStructure(context, structureType, newValue);

            // Replace old value with new value
            context.ReplaceAndRemove(value, newValue);
        }


        #endregion

        #region Nested Types

        /// <summary>
        /// A lowered phi that has to be sealed after all blocks have been processed.
        /// </summary>
        private readonly struct LoweredPhi
        {
            public LoweredPhi(
                PhiValue sourcePhi,
                FieldAccess fieldAccess,
                PhiValue.Builder phiBuilder)
            {
                SourcePhi = sourcePhi;
                FieldAccess = fieldAccess;
                PhiBuilder = phiBuilder;
            }

            /// <summary>
            /// Returns the source phi.
            /// </summary>
            public PhiValue SourcePhi { get; }

            /// <summary>
            /// Returns the source access chain.
            /// </summary>
            public FieldAccess FieldAccess { get; }

            /// <summary>
            /// Returns the new phi builder.
            /// </summary>
            public PhiValue.Builder PhiBuilder { get; }

            /// <summary>
            /// Seals this lowered phi.
            /// </summary>
            /// <param name="ssaBuilder">The parent SSA builder.</param>
            public PhiValue Seal(SSABuilder<FieldRef> ssaBuilder)
            {
                // Wire all phi arguments
                for (int i = 0, e = SourcePhi.Count; i < e; ++i)
                {
                    // Get the predecessor block and its associated value
                    var pred = SourcePhi.Sources[i];
                    var predValue = ssaBuilder.GetValue(
                        pred,
                        new FieldRef(SourcePhi.Nodes[i], FieldAccess));
                    PhiBuilder.AddArgument(pred, predValue);
                }

                // Seal the internal phi value
                return PhiBuilder.Seal();
            }
        }

        /// <summary>
        /// Internal temporary data structure.
        /// </summary>
        private readonly struct LoweringData
        {
            public LoweringData(List<LoweredPhi> loweredPhis)
            {
                LoweredPhis = loweredPhis;
            }

            /// <summary>
            /// The list of lowered phis.
            /// </summary>
            private List<LoweredPhi> LoweredPhis { get; }

            /// <summary>
            /// Adds the given phi to the list of lowered phis.
            /// </summary>
            /// <param name="loweredPhi">The lowered phi to add.</param>
            public void AddPhi(LoweredPhi loweredPhi) => LoweredPhis.Add(loweredPhi);
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Keeps structure load operations.
        /// </summary>
        private static void Keep(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            Load load) =>
            DisassembleStructure(
                context,
                load.Type as StructureType,
                load);

        /// <summary>
        /// Lowers structure load operations into distinct loads for each field.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            Load load)
        {
            var builder = context.Builder;
            foreach (var (_, fieldAccess) in load.Type as StructureType)
            {
                // Update source address
                var address = builder.CreateLoadFieldAddress(
                    load.Location,
                    load.Source,
                    new FieldSpan(fieldAccess));

                // Load value and store its reference in the current block
                var loweredLoad = builder.CreateLoad(load.Location, address);
                context.SetValue(
                    context.Block,
                    new FieldRef(load, fieldAccess),
                    loweredLoad);
                context.MarkConverted(loweredLoad);
            }
            context.Remove(load);
        }

        /// <summary>
        /// Keeps structure store operations.
        /// </summary>
        private static void Keep(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            Store store)
        {
            var newValue = AssembleStructure(
                context,
                store.Value.Type as StructureType,
                store.Value);
            var newStore = context.Builder.CreateStore(
                store.Location,
                store.Target,
                newValue);
            context.ReplaceAndRemove(store, newStore);
        }

        /// <summary>
        /// Lowers structure store operations into distinct stores for each field.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            Store store)
        {
            var builder = context.Builder;
            foreach (var (_, fieldAccess) in store.Value.Type as StructureType)
            {
                // Update target address
                var address = builder.CreateLoadFieldAddress(
                    store.Location,
                    store.Target,
                    new FieldSpan(fieldAccess));

                // Load the currently registered SSA value and store it
                var value = context.GetValue(
                    context.Block,
                    new FieldRef(store.Value, fieldAccess));
                var loweredStore = builder.CreateStore(
                    store.Location,
                    address,
                    value);
                context.MarkConverted(loweredStore);
            }
            context.Remove(store);
        }

        /// <summary>
        /// Lowers null values into separate SSA values.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            NullValue nullValue)
        {
            foreach (var (fieldType, fieldAccess) in nullValue.Type as StructureType)
            {
                // Build the new target value
                var value = context.Builder.CreateNull(
                    nullValue.Location,
                    fieldType);
                context.MarkConverted(value);

                // Bind the new SSA value
                context.SetValue(
                    context.Block,
                    new FieldRef(nullValue, fieldAccess),
                    value);
            }
        }

        /// <summary>
        /// Lowers structure values into separate SSA values.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            StructureValue structureValue)
        {
            foreach (var (_, fieldAccess) in structureValue.StructureType)
            {
                // Build the new target value
                var value = structureValue[fieldAccess.Index];

                // Bind the new SSA value
                context.SetValue(
                    context.Block,
                    new FieldRef(structureValue, fieldAccess),
                    value);
            }
        }

        /// <summary>
        /// Lowers get field operations into separate SSA values.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            GetField getField)
        {
            if (getField.Type is StructureType structureType)
            {
                foreach (var (_, fieldAccess) in structureType)
                {
                    // Get the source value
                    var value = context.GetValue(
                        context.Block,
                        new FieldRef(
                            getField.ObjectValue,
                            new FieldSpan(
                                getField.FieldSpan.Index + fieldAccess.Index)));

                    // Bind the mapped SSA value
                    context.SetValue(
                        context.Block,
                        new FieldRef(getField, fieldAccess),
                        value);
                }
                context.Remove(getField);
            }
            else
            {
                var getFieldValue = context.GetValue(
                    context.Block,
                    new FieldRef(getField.ObjectValue, getField.FieldSpan));
                context.ReplaceAndRemove(getField, getFieldValue);
            }
        }

        /// <summary>
        /// Lowers set field operations into separate SSA values.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            SetField setField)
        {
            foreach (var (_, fieldAccess) in setField.Type as StructureType)
            {
                Value value;
                if (setField.FieldSpan.Contains(fieldAccess))
                {
                    // Get value from the source value
                    value = setField.Value;
                    if (value.Type is StructureType)
                    {
                        var getFieldAccess = fieldAccess.Subtract(
                            setField.FieldSpan.Index);
                        value = context.GetValue(
                            context.Block,
                            new FieldRef(value, getFieldAccess));
                    }
                }
                else
                {
                    // Load the currently registered SSA value
                    value = context.GetValue(
                        context.Block,
                        new FieldRef(setField.ObjectValue, fieldAccess));
                }

                // Bind the mapped SSA value
                context.SetValue(
                    context.Block,
                    new FieldRef(setField, fieldAccess),
                    value);
            }
            context.Remove(setField);
        }

        /// <summary>
        /// Lowers phi values.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData data,
            PhiValue phi)
        {
            foreach (var (fieldType, fieldAccess) in phi.Type as StructureType)
            {
                // Build a new phi which might become dead in the future
                var phiBuilder = context.Builder.CreatePhi(
                    phi.Location,
                    fieldType);

                // Register the lowered phi
                data.AddPhi(new LoweredPhi(
                    phi,
                    fieldAccess,
                    phiBuilder));

                // Bind the new phi value
                context.SetValue(
                    context.Block,
                    new FieldRef(phi, fieldAccess),
                    phiBuilder.PhiValue);
            }
            context.Remove(phi);
        }

        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            Predicate predicate)
        {
            foreach (var (_, fieldAccess) in predicate.Type as StructureType)
            {
                // Build a new if predicate which might become dead in the future
                var trueValue = context.GetValue(
                    context.Block,
                    new FieldRef(predicate.TrueValue, fieldAccess));
                var falseValue = context.GetValue(
                    context.Block,
                    new FieldRef(predicate.FalseValue, fieldAccess));
                var newPredicate = context.Builder.CreatePredicate(
                    predicate.Location,
                    predicate.Condition,
                    trueValue,
                    falseValue);

                // Bind the new if predicate
                context.SetValue(
                    context.Block,
                    new FieldRef(predicate, fieldAccess),
                    newPredicate);
            }
        }

        /// <summary>
        /// Lowers method calls involving structure types.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            MethodCall call)
        {
            // Check for structure arguments that need to be rebuilt
            var callBuilder = context.Builder.CreateCall(call.Location, call.Target);
            foreach (Value argument in call)
            {
                Value newArgument = argument;
                if (argument.Type is StructureType argumentType)
                {
                    newArgument = AssembleStructure(
                        context,
                        argumentType,
                        argument);
                }
                callBuilder.Add(newArgument);
            }

            // Create new call node
            var newCall = callBuilder.Seal();
            context.ReplaceAndRemove(call, newCall);

            // Convert the return value
            if (call.Type is StructureType callType)
            {
                DisassembleStructure(
                    context,
                    callType,
                    newCall);
            }
        }

        /// <summary>
        /// Lowers warp shuffles.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            Broadcast value) =>
            LowerThreadValue<
                Broadcast,
                LowerThreadIntrinsics.BroadcastLowering>(
                context,
                value.Type as StructureType,
                value);

        /// <summary>
        /// Lowers warp shuffles.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            WarpShuffle value) =>
            LowerThreadValue<
                WarpShuffle,
                LowerThreadIntrinsics.WarpShuffleLowering>(
                context,
                value.Type as StructureType,
                value);

        /// <summary>
        /// Lowers sub-warp shuffles.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            SubWarpShuffle value) =>
            LowerThreadValue<
                SubWarpShuffle,
                LowerThreadIntrinsics.SubWarpShuffleLowering>(
                context,
                value.Type as StructureType,
                value);

        /// <summary>
        /// Lowers return terminators returning structure values.
        /// </summary>
        private static void Lower(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            ReturnTerminator value)
        {
            // Move the block builder to a valid insert position in the source block
            var blockBuilder = context.GetMethodBuilder()[value.BasicBlock];
            blockBuilder.SetupInsertPositionToEnd();

            // Assemble return value
            Value returnValue = value.ReturnValue;
            var returnType = returnValue.Type.As<StructureType>(value);
            var newReturnValue = AssembleStructure(
                context.SpecializeBuilder(blockBuilder),
                returnType,
                returnValue);

            // Replace return terminator with a new terminator
            context.Builder.Terminator = context.Builder.CreateReturn(
                value.Location,
                newReturnValue);
        }

        /// <summary>
        /// Lowers generic values containing structure types that cannot be rewritten.
        /// </summary>
        private static void Keep(
            SSARewriterContext<FieldRef> context,
            LoweringData _,
            Value value) =>
            DisassembleStructure(
                context,
                value.Type.As<StructureType>(value),
                value);

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter that keeps load/store values.
        /// </summary>
        private static readonly SSARewriter<FieldRef, LoweringData> Rewriter =
            new SSARewriter<FieldRef, LoweringData>();

        /// <summary>
        /// The internal rewriter that lowers load/store values.
        /// </summary>
        private static readonly SSARewriter<FieldRef, LoweringData> LoadStoreRewriter =
            new SSARewriter<FieldRef, LoweringData>();

        /// <summary>
        /// Adds the common rewriters to the given rewriter instance.
        /// </summary>
        /// <param name="rewriter">The rewriter to extend.</param>
        private static void AddRewriters(SSARewriter<FieldRef, LoweringData> rewriter)
        {
            // Keep particular values that cannot be rewritten by this pass
            rewriter.Add<Parameter>((_, value) => value.Type.IsStructureType, Keep);
            rewriter.Add<ArrayValue>((_, value) => value.Type.IsStructureType, Keep);
            rewriter.Add<GetArrayElement>(
                (_, value) => value.Type.IsStructureType, Keep);
            rewriter.Add<SetArrayElement>(
                (_, value) => value.Type.IsStructureType, Keep);
            rewriter.Add<AlignViewTo>(Keep);

            // Rewrite known values
            rewriter.Add<NullValue>((_, value) => value.Type.IsStructureType, Lower);
            rewriter.Add<StructureValue>((_, value) => value.Type.IsStructureType, Lower);
            rewriter.Add<PhiValue>((_, value) => value.Type.IsStructureType, Lower);
            rewriter.Add<Broadcast>((_, value) => value.Type.IsStructureType, Lower);
            rewriter.Add<WarpShuffle>((_, value) => value.Type.IsStructureType, Lower);
            rewriter.Add<SubWarpShuffle>((_, value) => value.Type.IsStructureType, Lower);

            rewriter.Add<Predicate>((_, value) => value.Type.IsStructureType, Lower);

            rewriter.Add<ReturnTerminator>(
                (_, value) => value.Method.ReturnType.IsStructureType, Lower);

            rewriter.Add<GetField>(Lower);
            rewriter.Add<SetField>(Lower);
            rewriter.Add<MethodCall>(Lower);
        }

        /// <summary>
        /// Initializes all rewriters.
        /// </summary>
        static LowerStructures()
        {
            AddRewriters(Rewriter);
            Rewriter.Add<Load>((_, value) => value.Type.IsStructureType, Keep);
            Rewriter.Add<Store>((_, value) => value.Value.Type.IsStructureType, Keep);

            AddRewriters(LoadStoreRewriter);
            LoadStoreRewriter.Add<Load>(
                (_, value) => value.Type.IsStructureType, Lower);
            LoadStoreRewriter.Add<Store>(
                (_, value) => value.Value.Type.IsStructureType, Lower);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new structure conversion pass.
        /// </summary>
        public LowerStructures() { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current flags.
        /// </summary>
        public LowerStructureFlags Flags { get; }

        /// <summary>
        /// Returns true if load/store operations should be lowered.
        /// </summary>
        public bool LowerLoadStores =>
            (Flags & LowerStructureFlags.LowerLoadStores) != LowerStructureFlags.None;

        #endregion

        #region Methods

        /// <summary>
        /// Applies the structure lowering transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var ssaBuilder = SSABuilder<FieldRef>.Create(builder);
            var loweredPhis = new List<LoweredPhi>();
            var loweringData = new LoweringData(loweredPhis);

            bool applied = LowerLoadStores
                ? LoadStoreRewriter.Rewrite(ssaBuilder, loweringData)
                : Rewriter.Rewrite(ssaBuilder, loweringData);

            // Seal all lowered phis
            foreach (var phi in loweredPhis)
            {
                var phiValue = phi.Seal(ssaBuilder);
                phiValue.TryRemoveTrivialPhi(builder);
            }

            return applied;
        }

        #endregion
    }
}

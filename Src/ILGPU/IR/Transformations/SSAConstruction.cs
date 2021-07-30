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
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// An abstract construction data element per value.
        /// </summary>
        /// <typeparam name="TData">
        /// The parent type implementing this interface.
        /// </typeparam>
        protected interface IConstructionDataType<TData>
            where TData : struct, IConstructionDataType<TData>
        {
            /// <summary>
            /// The internal field reference to access.
            /// </summary>
            FieldRef FieldRef { get; }

            /// <summary>
            /// Performs a virtual access to the given sub field-ref.
            /// </summary>
            /// <param name="fieldRef">The field ref to access.</param>
            /// <returns>
            /// The updated data element using the provided field ref.
            /// </returns>
            TData Access(FieldRef fieldRef);
        }

        /// <summary>
        /// An abstract interface that contains required methods to perform the SSA
        /// construction.
        /// </summary>
        protected interface IConstructionData<TData>
            where TData : struct, IConstructionDataType<TData>
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
            /// <param name="data">The resolved data reference (if any).</param>
            bool TryGetConverted(Value value, out TData data);

            /// <summary>
            /// Adds the given value and the field reference to the mapping of
            /// converted values.
            /// </summary>
            /// <param name="value">The value to register.</param>
            /// <param name="data">The data to associated with the value.</param>
            void AddConverted(Value value, in TData data);
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
        protected static void ConvertAlloca<TConstructionData, TData>(
            SSARewriterContext<Value> context,
            in TConstructionData data,
            Alloca alloca,
            Value initValue,
            in TData allocaData)
            where TConstructionData : IConstructionData<TData>
            where TData : struct, IConstructionDataType<TData>
        {
            alloca.Assert(data.ContainsAlloca(alloca));

            // Bind the init value and remove the allocation from the block
            context.SetValue(context.Block, alloca, initValue);
            data.AddConverted(alloca, allocaData);
            context.Remove(alloca);
        }

        /// <summary>
        /// Converts a store node into an SSA value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void ConvertStore(
            SSARewriterContext<Value> context,
            Store store,
            FieldRef storeRef)
        {
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
        /// Converts a load node into an SSA value.
        /// </summary>
        protected static void ConvertLoad(
            SSARewriterContext<Value> context,
            Load load,
            FieldRef loadRef)
        {
            if (!load.Uses.HasAny)
            {
                // Remove dead value loads
                context.Remove(load);
            }
            else
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
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Converts a field-address operation into an SSA binding.
        /// </summary>
        protected static void Convert<TConstructionData, TData>(
            SSARewriterContext<Value> context,
            TConstructionData data,
            LoadFieldAddress loadFieldAddress)
            where TConstructionData : IConstructionData<TData>
            where TData : struct, IConstructionDataType<TData>
        {
            if (!data.TryGetConverted(loadFieldAddress.Source, out var lfaData))
                return;

            var fieldRef = lfaData.FieldRef;
            var accessedRef = fieldRef.Access(loadFieldAddress.FieldSpan);
            data.AddConverted(loadFieldAddress, lfaData.Access(accessedRef));
            context.Remove(loadFieldAddress);
        }

        /// <summary>
        /// Converts an address-space cast into an SSA binding.
        /// </summary>
        protected static void Convert<TConstructionData, TData>(
            SSARewriterContext<Value> context,
            TConstructionData data,
            AddressSpaceCast addressSpaceCast)
            where TConstructionData : IConstructionData<TData>
            where TData : struct, IConstructionDataType<TData>
        {
            if (!data.TryGetConverted(addressSpaceCast.Value, out var castData))
                return;

            data.AddConverted(addressSpaceCast, castData);
            context.Remove(addressSpaceCast);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// Registers all base rewriting patterns.
        /// </summary>
        protected static void RegisterRewriters<TConstructionData, TData>(
            SSARewriter<Value, TConstructionData> rewriter)
            where TConstructionData : IConstructionData<TData>
            where TData : struct, IConstructionDataType<TData>
        {
            rewriter.Add<LoadFieldAddress>(Convert<TConstructionData, TData>);
            rewriter.Add<AddressSpaceCast>(Convert<TConstructionData, TData>);
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
        /// A single field reference in the scope of the <see cref="ConstructionData"/>
        /// container structure.
        /// </summary>
        protected readonly struct ConstructionFieldRef :
            IConstructionDataType<ConstructionFieldRef>
        {
            /// <summary>
            /// Constructs a new wrapper field reference.
            /// </summary>
            /// <param name="fieldRef">The field reference to wrap.</param>
            public ConstructionFieldRef(FieldRef fieldRef)
            {
                FieldRef = fieldRef;
            }

            /// <summary>
            /// Returns the internal field reference.
            /// </summary>
            public FieldRef FieldRef { get; }

            /// <summary>
            /// Returns an updated instance using the given field reference.
            /// </summary>
            public readonly ConstructionFieldRef Access(FieldRef fieldRef) =>
                new ConstructionFieldRef(fieldRef);

            /// <summary>
            /// Returns the string representation of the underyling field reference.
            /// </summary>
            public readonly override string ToString() => FieldRef.ToString();
        }

        /// <summary>
        /// A default implementation of the
        /// <see cref="SSATransformationBase.IConstructionData{TData}" /> interface.
        /// </summary>
        protected readonly struct ConstructionData :
            IConstructionData<ConstructionFieldRef>
        {
            /// <summary>
            /// Initializes the data structure.
            /// </summary>
            public ConstructionData(HashSet<Alloca> allocas)
            {
                Allocas = allocas;
                ConvertedValues = new Dictionary<Value, ConstructionFieldRef>();
            }

            /// <summary>
            /// The set of all allocas to be converted into SSA value.
            /// </summary>
            private HashSet<Alloca> Allocas { get; }

            /// <summary>
            /// Maps converted values to their associated field references.
            /// </summary>
            private Dictionary<Value, ConstructionFieldRef> ConvertedValues { get; }

            /// <summary>
            /// Returns true if the given alloca should be converted.
            /// </summary>
            /// <param name="alloca">The alloca to check.</param>
            public readonly bool ContainsAlloca(Alloca alloca) =>
                Allocas.Contains(alloca);

            /// <summary>
            /// Tries to get a converted value entry.
            /// </summary>
            public readonly bool TryGetConverted(
                Value value,
                out ConstructionFieldRef data) =>
                ConvertedValues.TryGetValue(value, out data);

            /// <summary>
            /// Adds the given value and the field reference to the mapping of
            /// converted values.
            /// </summary>
            public readonly void AddConverted(
                Value value,
                in ConstructionFieldRef data) =>
                ConvertedValues.Add(value, data);
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
        /// <param name="getConstructionData">
        /// A builder function to convert the internal construction data instance
        /// into the target data structure required for this transformation.
        /// </param>
        /// <returns>True, if the transformation could be applied.</returns>
        protected bool PerformTransformation<TConstructionData, TData>(
            Method.Builder builder,
            SSARewriter<Value, TConstructionData> rewriter,
            Func<ConstructionData, TConstructionData> getConstructionData)
            where TConstructionData : struct, IConstructionData<TData>
            where TData : struct, IConstructionDataType<TData>
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
            var constructionData = new ConstructionData(allocas);
            return rewriter.Rewrite(ssaBuilder, getConstructionData(constructionData));
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
            var fieldRef = new FieldRef(alloca);
            ConvertAlloca(
                context,
                data,
                alloca,
                initValue,
                new ConstructionFieldRef(fieldRef));
        }

        /// <summary>
        /// Converts a store node to its associated value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ConstructionData data,
            Store store)
        {
            if (!data.TryGetConverted(store.Target, out var storeData))
                return;

            ConvertStore(context, store, storeData.FieldRef);
        }

        /// <summary>
        /// Converts a load node to its associated value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ConstructionData data,
            Load load)
        {
            if (!data.TryGetConverted(load.Source, out var loadData))
                return;

            ConvertLoad(context, load, loadData.FieldRef);
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
            RegisterRewriters<ConstructionData, ConstructionFieldRef>(Rewriter);

            Rewriter.Add<Alloca>(Convert);
            Rewriter.Add<Store>(Convert);
            Rewriter.Add<Load>(Convert);
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
            PerformTransformation<ConstructionData, ConstructionFieldRef>(
                builder,
                Rewriter,
                data => data);

        #endregion
    }

    /// <summary>
    /// Performs an SSA structure construction from array allocations transformation.
    /// </summary>
    public sealed class SSAStructureConstruction : SSAConstructionBase
    {
        #region Utility Methods

        /// <summary>
        /// Returns true if the given <see cref="LoadElementAddress"/> value requires
        /// an explicit address in memory (e.g. the offset being accessed could not be
        /// resolved to a statically known index value).
        /// </summary>
        /// <param name="lea">The lea node.</param>
        /// <param name="arrayLength">The array length.</param>
        /// <returns>True, if an explicit address is required.</returns>
        private static bool RequiresAddress(
            LoadElementAddress lea,
            int arrayLength) =>
            !(lea.Offset.Resolve() is PrimitiveValue index &&
            index.Int32Value >= 0 && index.Int32Value < arrayLength) ||
            RequiresAddress(lea);

        /// <summary>
        /// Returns true if the given value requires an explicit address in memory.
        /// </summary>
        /// <param name="node">The node to test.</param>
        /// <param name="arrayLength">The array length.</param>
        /// <returns>True, if an explicit address is required.</returns>
        private static bool RequiresAddress(Value node, int arrayLength)
        {
            foreach (Value use in node.Uses)
            {
                switch (use)
                {
                    case NewView newView:
                        if (RequiresAddress(newView, arrayLength))
                            return true;
                        break;
                    case LoadElementAddress lea:
                        if (RequiresAddress(lea, arrayLength))
                            return true;
                        break;
                    case GetViewLength _:
                        break;
                    case AddressSpaceCast addressSpaceCast:
                        if (RequiresAddress(addressSpaceCast, arrayLength))
                            return true;
                        break;
                    default:
                        if (RequiresAddressForUse(use))
                            return true;
                        break;
                }
            }
            return false;
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// An internal array allocation field reference wrapper.
        /// </summary>
        private readonly struct ArrayData : IConstructionDataType<ArrayData>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ArrayData(
                int arrayLength,
                int numElementFields,
                ConstructionFieldRef internalFieldRef)
            {
                Debug.Assert(arrayLength > 0, "Invalid array length");
                Debug.Assert(numElementFields > 0, "Invalid number of element fields");

                ArrayLength = arrayLength;
                NumElementFields = numElementFields;
                InternalFieldRef = internalFieldRef;
            }

            /// <summary>
            /// Returns the array length.
            /// </summary>
            public int ArrayLength { get; }

            /// <summary>
            /// Returns the number of fields per array element.
            /// </summary>
            public int NumElementFields { get; }

            /// <summary>
            /// Returns the internal field ref.
            /// </summary>
            public ConstructionFieldRef InternalFieldRef { get; }

            /// <summary>
            /// Returns the field ref.
            /// </summary>
            public readonly FieldRef FieldRef => InternalFieldRef.FieldRef;

            /// <summary>
            /// Creates a new array data instance using the given field reference.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ArrayData Access(FieldRef fieldRef) =>
                new ArrayData(
                    ArrayLength,
                    NumElementFields,
                    InternalFieldRef.Access(fieldRef));
        }

        /// <summary>
        /// An array construction helper that stores intermediate data during the SSA
        /// construction of data arrays.
        /// </summary>
        private readonly struct ArrayConstructionData : IConstructionData<ArrayData>
        {
            public ArrayConstructionData(in ConstructionData data)
            {
                ConstructionData = data;
                ArrayData = new Dictionary<Value, (int, int)>();
            }

            /// <summary>
            /// The internal construction data.
            /// </summary>
            public ConstructionData ConstructionData { get; }

            /// <summary>
            /// The additional array data per allocation entry.
            /// </summary>
            private Dictionary<
                Value,
                (int ArrayLength, int NumElementFields)> ArrayData
            { get; }

            /// <summary>
            /// Returns true if the given alloca should be converted.
            /// </summary>
            /// <param name="alloca">The alloca to check.</param>
            public readonly bool ContainsAlloca(Alloca alloca) =>
                ConstructionData.ContainsAlloca(alloca);

            /// <summary>
            /// Tries to get a converted value entry.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool TryGetConverted(Value value, out ArrayData data)
            {
                if (!ConstructionData.TryGetConverted(value, out var internalData))
                {
                    data = default;
                    return false;
                }

                // Retrieve the array data
                var arrayData = ArrayData[value];
                data = new ArrayData(
                    arrayData.ArrayLength,
                    arrayData.NumElementFields,
                    internalData);
                return true;
            }

            /// <summary>
            /// Adds the given value and the field reference to the mapping of
            /// converted values.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void AddConverted(Value value, in ArrayData data)
            {
                ConstructionData.AddConverted(value, data.InternalFieldRef);
                ArrayData.Add(value, (data.ArrayLength, data.NumElementFields));
            }
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Converts an alloca node to its initial SSA value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ArrayConstructionData data,
            Alloca alloca)
        {
            if (!data.ContainsAlloca(alloca))
                return;
            alloca.Assert(!alloca.IsSimpleAllocation);

            // Get the builder and the associated array length value
            var builder = context.Builder;
            var arrayLengthValue = alloca.ArrayLength.ResolveAs<PrimitiveValue>();
            alloca.AssertNotNull(arrayLengthValue);
            int arrayLength = arrayLengthValue.Int32Value;

            // Create a structure with the appropriate number of fields that correspond
            // to the current array length
            int numFields = StructureType.GetNumFields(alloca.AllocaType);
            int numStructFields = arrayLength * numFields;
            var allocaTypeBuilder = builder.CreateStructureType(numStructFields);

            // Append all virtual fields
            for (int i = 0; i < arrayLength; ++i)
                allocaTypeBuilder.Add(alloca.AllocaType);
            var allocationType = allocaTypeBuilder.Seal();

            // Initialize the structure value
            var initValue = builder.CreateNull(alloca.Location, allocationType);

            // Prepare the internal data structure to refer to this array
            var initFieldRef = new FieldRef(alloca, new FieldSpan(0, numStructFields));
            var arrayData = new ArrayData(
                arrayLength,
                numFields,
                new ConstructionFieldRef(initFieldRef));

            // Convert the current allocation node
            ConvertAlloca(
                context,
                data,
                alloca,
                initValue,
                arrayData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FieldRef RemapToStructureElementAccess(in ArrayData data)
        {
            // Convert the field reference to an explicit access to the first structure
            // element fields (if any)
            var fieldSpan = new FieldSpan(
                new FieldAccess(0),
                data.NumElementFields);
            return data.FieldRef.Access(fieldSpan);
        }

        /// <summary>
        /// Converts a store node to its associated value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ArrayConstructionData data,
            Store store)
        {
            if (!data.TryGetConverted(store.Target, out var storeData))
                return;

            // Convert the store
            var fieldRef = RemapToStructureElementAccess(storeData);
            ConvertStore(context, store, fieldRef);
        }

        /// <summary>
        /// Converts a load node to its associated value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ArrayConstructionData data,
            Load load)
        {
            if (!data.TryGetConverted(load.Source, out var loadData))
                return;

            // Convert the load
            var fieldRef = RemapToStructureElementAccess(loadData);
            ConvertLoad(context, load, fieldRef);
        }

        /// <summary>
        /// Converts a load node into an SSA value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ArrayConstructionData data,
            LoadElementAddress loadElementAddress)
        {
            if (!data.TryGetConverted(loadElementAddress.Source, out var leaData))
                return;

            // Get the primitive constant field offset
            var fieldOffset = loadElementAddress.Offset.ResolveAs<PrimitiveValue>();
            loadElementAddress.AssertNotNull(fieldOffset);
            var fieldAccess = new FieldAccess(
                fieldOffset.Int32Value * leaData.NumElementFields);

            // Map the field index to the current data reference
            var fieldRef = leaData.FieldRef;
            var access = fieldRef.Access(new FieldSpan(
                fieldAccess,
                leaData.NumElementFields));
            data.AddConverted(loadElementAddress, leaData.Access(access));
            context.Remove(loadElementAddress);
        }

        /// <summary>
        /// Converts a new view into an SSA value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ArrayConstructionData data,
            NewView newView)
        {
            if (!data.TryGetConverted(newView.Pointer, out var newViewData))
                return;

            data.AddConverted(newView, newViewData);
            context.Remove(newView);
        }

        /// <summary>
        /// Converts a new get length node into an SSA value.
        /// </summary>
        private static void Convert(
            SSARewriterContext<Value> context,
            ArrayConstructionData data,
            GetViewLength getViewLength)
        {
            if (!data.TryGetConverted(getViewLength.View, out var getData))
                return;

            // Create a new primitive view length value
            var lengthValue = context.Builder.CreatePrimitiveValue(
                getViewLength.Location,
                getData.ArrayLength);
            context.ReplaceAndRemove(getViewLength, lengthValue);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly SSARewriter<Value, ArrayConstructionData> Rewriter =
            new SSARewriter<Value, ArrayConstructionData>();

        /// <summary>
        /// Registers all rewriting patterns.
        /// </summary>
        static SSAStructureConstruction()
        {
            RegisterRewriters<ArrayConstructionData, ArrayData>(Rewriter);

            Rewriter.Add<Alloca>(Convert);
            Rewriter.Add<Store>(Convert);
            Rewriter.Add<Load>(Convert);
            Rewriter.Add<LoadElementAddress>(Convert);

            Rewriter.Add<NewView>(Convert);
            Rewriter.Add<GetViewLength>(Convert);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new SSA structure construction pass.
        /// </summary>
        public SSAStructureConstruction() : this(MemoryAddressSpace.Local) { }

        /// <summary>
        /// Constructs a new SSA structure construction pass.
        /// </summary>
        /// <param name="addressSpace">The target memory address space.</param>
        public SSAStructureConstruction(MemoryAddressSpace addressSpace)
            : base(addressSpace)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Applies the SSA construction transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            PerformTransformation<ArrayConstructionData, ArrayData>(
                builder,
                Rewriter,
                data => new ArrayConstructionData(data));

        /// <summary>
        /// Returns true if the given allocation is a simple allocation and does not
        /// require explicit addresses.
        /// </summary>
        protected override bool CanConvert(Method.Builder builder, Alloca alloca) =>
            base.CanConvert(builder, alloca) &&
            // Check whether we require an address or there are array accesses
            // that cannot be converted to statically known field index values.
            alloca.IsArrayAllocation(out var length) &&
            !RequiresAddress(alloca, length.Int32Value);

        #endregion
    }
}

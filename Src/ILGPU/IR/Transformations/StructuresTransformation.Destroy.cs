// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: StructuresTransformation.Destroy.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static ILGPU.IR.Types.StructureType;
using FieldRef = ILGPU.IR.Types.StructureType.FieldRef;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// A custom mapping processor.
    /// </summary>
    public readonly struct DestroyStructuresMapping : IStructuresTransformationMapping
    {
        #region Instance

        private readonly StructuresTransformation.ParameterMapping parameterMapping;

        /// <summary>
        /// Constructs a new mapping processor.
        /// </summary>
        /// <param name="builder">The associated builder.</param>
        /// <param name="scope">The current scope.</param>
        /// <param name="parameters">The mapping of all parameters.</param>
        /// <param name="chains">The mapping of all chain entries.</param>
        internal DestroyStructuresMapping(
            IRBuilder builder,
            Scope scope,
            StructuresTransformation.ParameterMapping parameters,
            Dictionary<MemoryValue, (MemoryRef, MemoryRef)> chains)
        {
            Builder = builder;
            Scope = scope;
            parameterMapping = parameters;
            MemoryChains = chains;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated builder.
        /// </summary>
        public IRBuilder Builder { get; }

        /// <summary>
        /// Returns the current scope.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Returns the stored memory chains.
        /// </summary>
        internal Dictionary<MemoryValue, (MemoryRef, MemoryRef)> MemoryChains { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Registers transformed parameters of the given source function.
        /// </summary>
        /// <param name="sourceFunction">The source function.</param>
        /// <param name="parameterRef">The parameter access chain.</param>
        /// <param name="target">The target parameter.</param>
        internal void MapParameter(
            FunctionValue sourceFunction,
            FieldRef parameterRef,
            Parameter target) => parameterMapping.MapParameter(
                sourceFunction,
                parameterRef,
                target);

        /// <summary>
        /// Constructs a new memory chain for the given memory value.
        /// </summary>
        /// <param name="node">The memory value.</param>
        /// <returns>The new memory chain.</returns>
        internal MemoryRef NewChain(MemoryValue node)
        {
            var chain = Builder.CreateUndefMemoryReference();
            MemoryChains.Add(node, (chain, chain));
            return chain;
        }

        /// <summary>
        /// Finishes the given chain.
        /// </summary>
        /// <param name="node">The memory value.</param>
        /// <param name="chain">The last chain entry.</param>
        internal void FinishChain(MemoryValue node, MemoryRef chain)
        {
            MemoryChains[node] = (MemoryChains[node].Item1, chain);
        }

        #endregion
    }

    /// <summary>
    /// Flattens structures to scalar values.
    /// </summary>
    public sealed class DestroyStructures : StructuresTransformation<DestroyStructuresMapping>
    {
        #region Constants

        /// <summary>
        /// The desired transformations that should run after
        /// applying this transformation.
        /// </summary>
        private const TransformationFlags FollowUpFlags = TransformationFlags.OptimizeParameters;

        #endregion

        #region Static

        /// <summary>
        /// Tries to resolve a type to a structure info instance.
        /// </summary>
        /// <param name="typeNode">The type to resolve to a structure info object.</param>
        /// <param name="type">The resolved structure info.</param>
        /// <returns>True, if the type could be resolved to a structure type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetStructure(TypeNode typeNode, out StructureType type)
        {
            type = typeNode as StructureType;
            return type != null;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new structure flattener.
        /// </summary>
        public DestroyStructures()
            : base(TransformationFlags.DestroyStructures, FollowUpFlags)
        {
            RequiredTransformationFlags = TransformationFlags.Inlining;
        }

        #endregion

        #region Transformation

        /// <summary>
        /// Transforms structure loads into distinct scalar loads.
        /// </summary>
        /// <typeparam name="TBuilder">The underlying CPS builder type.</typeparam>
        struct MapLoad<TBuilder> : IFieldRefAction<Value>
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            public MapLoad(
                MemoryRef chain,
                in DestroyStructuresMapping mapping,
                in CPSBuilder<TBuilder> cpsBuilder)
            {
                Builder = mapping.Builder;
                CPSBuilder = cpsBuilder;
                Chain = chain;
            }

            public IRBuilder Builder { get; }

            public CPSBuilder<TBuilder> CPSBuilder { get; }

            public MemoryRef Chain { get; private set; }

            public Value GetFieldValue(
                in FieldRef fieldRef,
                Value address,
                StructureType structureType,
                int fieldIndex) => Builder.CreateLoadFieldAddress(address, fieldIndex);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Apply(
                in FieldRef fieldRef,
                Value fieldAddress,
                StructureType structureType,
                int fieldIndex)
            {
                var value = Builder.CreateLoad(Chain, fieldAddress);
                CPSBuilder.SetValue(fieldRef, value);
                Chain = Builder.CreateMemoryReference(value);
            }
        }

        /// <summary>
        /// Transforms <see cref="Load"/> nodes.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type.</typeparam>
        /// <param name="cpsBuilder">The current CPS builder.</param>
        /// <param name="load">The node.</param>
        /// <param name="mapping">The current transformation mapping.</param>
        /// <returns>True, iff the node could be transformed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TransformLoad<TBuilder>(in CPSBuilder<TBuilder> cpsBuilder, Load load, in DestroyStructuresMapping mapping)
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            if (!TryGetStructure(load.Type, out StructureType structureType))
                return false;

            var chain = mapping.NewChain(load);
            var action = new MapLoad<TBuilder>(chain, mapping, cpsBuilder);
            structureType.ForEachField(
                new FieldRef(load),
                load.Source.Resolve(),
                ref action);
            mapping.FinishChain(load, action.Chain);
            return true;
        }

        /// <summary>
        /// Transforms structure stores into distinct scalar stores.
        /// </summary>
        /// <typeparam name="TBuilder">The underlying CPS builder type.</typeparam>
        struct MapStore<TBuilder> : IFieldRefAction<Value>
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            public MapStore(
                MemoryRef chain,
                in DestroyStructuresMapping mapping,
                in CPSBuilder<TBuilder> cpsBuilder)
            {
                Builder = mapping.Builder;
                CPSBuilder = cpsBuilder;
                Chain = chain;
            }

            public IRBuilder Builder { get; }

            public CPSBuilder<TBuilder> CPSBuilder { get; }

            public MemoryRef Chain { get; private set; }

            public Value GetFieldValue(
                in FieldRef fieldRef,
                Value address,
                StructureType structureType,
                int fieldIndex) => Builder.CreateLoadFieldAddress(address, fieldIndex);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Apply(
                in FieldRef fieldRef,
                Value fieldAddress,
                StructureType structureType,
                int fieldIndex)
            {
                var value = CPSBuilder.GetValue(fieldRef);
                var store = Builder.CreateStore(Chain, fieldAddress, value);
                Chain = Builder.CreateMemoryReference(store);
            }
        }

        /// <summary>
        /// Transforms <see cref="Store"/> nodes.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type.</typeparam>
        /// <param name="cpsBuilder">The current CPS builder.</param>
        /// <param name="store">The node.</param>
        /// <param name="mapping">The current transformation mapping.</param>
        /// <returns>True, iff the node could be transformed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TransformStore<TBuilder>(in CPSBuilder<TBuilder> cpsBuilder, Store store, in DestroyStructuresMapping mapping)
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            if (!TryGetStructure(store.Value.Type, out StructureType structureType))
                return false;

            var chain = mapping.NewChain(store);
            var action = new MapStore<TBuilder>(chain, mapping, cpsBuilder);
            structureType.ForEachField(
                new FieldRef(store.Value),
                store.Target.Resolve(),
                ref action);
            mapping.FinishChain(store, action.Chain);
            return true;
        }

        /// <summary>
        /// Transforms structure values into distinct scalar values.
        /// </summary>
        /// <typeparam name="TBuilder">The underlying CPS builder type.</typeparam>
        readonly struct MapNull<TBuilder> : IFieldRefAction<object>
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            public MapNull(
                in DestroyStructuresMapping mapping,
                in CPSBuilder<TBuilder> cpsBuilder)
            {
                Builder = mapping.Builder;
                CPSBuilder = cpsBuilder;
            }

            public IRBuilder Builder { get; }

            public CPSBuilder<TBuilder> CPSBuilder { get; }

            [SuppressMessage("Microsoft.Performance", "CA1822: MarkMembersAsStatic", Justification = "Interface implementation")]
            public object GetFieldValue(
                in FieldRef fieldRef,
                object _,
                StructureType structureType,
                int fieldIndex) => null;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Apply(
                in FieldRef fieldRef,
                object _,
                StructureType structureType,
                int fieldIndex)
            {
                var childType = structureType.Children[fieldIndex];
                var value = Builder.CreateNull(childType);
                CPSBuilder.SetValue(fieldRef, value);
            }
        }

        /// <summary>
        /// Transforms <see cref="NullValue"/> nodes.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type.</typeparam>
        /// <param name="cpsBuilder">The current CPS builder.</param>
        /// <param name="value">The node.</param>
        /// <param name="mapping">The current transformation mapping.</param>
        /// <returns>True, iff the node could be transformed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TransformNull<TBuilder>(in CPSBuilder<TBuilder> cpsBuilder, NullValue value, in DestroyStructuresMapping mapping)
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            // We have to create a new mapping for all field values
            if (!TryGetStructure(value.Type, out StructureType structureType))
                return false;

            var action = new MapNull<TBuilder>(mapping, cpsBuilder);
            structureType.ForEachField(
                new FieldRef(value),
                (object)null,
                ref action);
            return true;
        }

        /// <summary>
        /// Transforms structure field values into distinct scalar values.
        /// </summary>
        /// <typeparam name="TBuilder">The underlying CPS builder type.</typeparam>
        readonly struct MapFieldValue<TBuilder> : IFieldRefAction<FieldRef>
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            public MapFieldValue(
                in CPSBuilder<TBuilder> cpsBuilder)
            {
                CPSBuilder = cpsBuilder;
            }

            public CPSBuilder<TBuilder> CPSBuilder { get; }

            [SuppressMessage("Microsoft.Performance", "CA1822: MarkMembersAsStatic", Justification = "Interface implementation")]
            public FieldRef GetFieldValue(
                in FieldRef fieldRef,
                FieldRef targetFieldRef,
                StructureType structureType,
                int fieldIndex) => targetFieldRef.Access(fieldIndex);

            public void Apply(
                in FieldRef fieldRef,
                FieldRef targetFieldRef,
                StructureType structureType,
                int fieldIndex)
            {
                var value = CPSBuilder.GetValue(fieldRef);
                CPSBuilder.SetValue(targetFieldRef, value);
            }
        }

        /// <summary>
        /// Transforms <see cref="GetField"/> nodes.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type.</typeparam>
        /// <param name="cpsBuilder">The current CPS builder.</param>
        /// <param name="getField">The node.</param>
        /// <param name="mapping">The current transformation mapping.</param>
        /// <returns>True, iff the node could be transformed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TransformGetField<TBuilder>(in CPSBuilder<TBuilder> cpsBuilder, GetField getField, in DestroyStructuresMapping mapping)
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            if (!TryGetStructure(getField.StructValue.Type, out StructureType info))
                return false;

            var fieldRef = new FieldRef(getField.StructValue, ImmutableArray.Create(getField.FieldIndex));
            if (TryGetStructure(getField.Type, out StructureType resultStructureType))
            {
                var action = new MapFieldValue<TBuilder>(cpsBuilder);
                resultStructureType.ForEachField(
                    fieldRef,
                    new FieldRef(getField),
                    ref action);
            }
            else
            {
                // This is a primitive type extraction -> replace node
                var fieldValue = cpsBuilder.GetValue(fieldRef);
                getField.Replace(fieldValue);
            }
            return true;
        }

        /// <summary>
        /// Transforms <see cref="SetField"/> nodes.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type.</typeparam>
        /// <param name="cpsBuilder">The current CPS builder.</param>
        /// <param name="setField">The node.</param>
        /// <param name="mapping">The current transformation mapping.</param>
        /// <returns>True, iff the node could be transformed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TransformSetField<TBuilder>(in CPSBuilder<TBuilder> cpsBuilder, SetField setField, in DestroyStructuresMapping mapping)
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            if (!TryGetStructure(setField.Type, out StructureType structureType))
                return false;

            var action = new MapFieldValue<TBuilder>(cpsBuilder);
            structureType.ForEachField(
                new FieldRef(setField.StructValue),
                new FieldRef(setField),
                ref action);

            var accessChain = ImmutableArray.Create(setField.FieldIndex);
            var targetFieldRef = new FieldRef(setField, accessChain);
            if (TryGetStructure(setField.FieldType, out StructureType resultStructureType))
            {
                resultStructureType.ForEachField(
                    new FieldRef(setField.Value),
                    targetFieldRef,
                    ref action);
            }
            else
            {
                cpsBuilder.SetValue(targetFieldRef, setField.Value);
            }

            return true;
        }

        /// <summary>
        /// Represents a parameter transformer that destroys structures by adding the required
        /// scalar parameters that represent the structure values.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type.</typeparam>
        private readonly struct ParameterTransformer<TBuilder> : IParameterTransformer<StructureType>
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            /// <summary>
            /// Constructs a new function provider.
            /// </summary>
            /// <param name="parent">The parent transformation.</param>
            /// <param name="scope">The current scope.</param>
            public ParameterTransformer(
                DestroyStructures parent,
                Scope scope)
            {
                Parent = parent;
                Scope = scope;
            }

            /// <summary>
            /// Returns the parent reference.
            /// </summary>
            public DestroyStructures Parent { get; }

            /// <summary>
            /// Returns the current scope.
            /// </summary>
            public Scope Scope { get; }

            /// <summary cref="StructuresTransformation{TMapping}.IParameterTransformer{T}.CanTransform(in TMapping, FunctionValue, Value, out T)"/>
            [SuppressMessage("Microsoft.Performance", "CA1822: MarkMembersAsStatic", Justification = "Interface implementation")]
            public bool CanTransform(
                in DestroyStructuresMapping mapping,
                FunctionValue sourceFunction,
                Value sourceValue,
                out StructureType structureType) =>
                TryGetStructure(sourceValue.Type, out structureType);

            /// <summary>
            /// Transforms structure field values into distinct scalar values.
            /// </summary>
            readonly struct TransformParams : IFieldRefAction<FunctionBuilder>
            {
                /// <summary>
                /// Constructs a new parameter transformation action.
                /// </summary>
                /// <param name="sourceFunction">The source function.</param>
                /// <param name="name">The associated param name.</param>
                /// <param name="mapping">The current mapping.</param>
                public TransformParams(
                    FunctionValue sourceFunction,
                    string name,
                    in DestroyStructuresMapping mapping)
                {
                    SourceFunction = sourceFunction;
                    Name = name;
                    Mapping = mapping;
                }

                /// <summary>
                /// Returns the source function.
                /// </summary>
                public FunctionValue SourceFunction { get; }

                /// <summary>
                /// Returns the associated param name.
                /// </summary>
                public string Name { get; }

                /// <summary>
                /// Returns the associated mapping.
                /// </summary>
                public DestroyStructuresMapping Mapping { get; }

                [SuppressMessage("Microsoft.Performance", "CA1822: MarkMembersAsStatic", Justification = "Interface implementation")]
                public FunctionBuilder GetFieldValue(
                    in FieldRef fieldRef,
                    FunctionBuilder functionBuilder,
                    StructureType structureType,
                    int fieldIndex) => functionBuilder;

                public void Apply(
                    in FieldRef fieldRef,
                    FunctionBuilder functionBuilder,
                    StructureType structureType,
                    int fieldIndex)
                {
                    var fieldType = structureType.Children[fieldIndex];
                    var parameter = functionBuilder.AddParameter(fieldType, Name);
                    Mapping.MapParameter(SourceFunction, fieldRef, parameter);
                }
            }

            /// <summary cref="StructuresTransformation{TMapping}.IParameterTransformer{T}.Transform(in TMapping, in T, FunctionValue, Parameter, FunctionBuilder)"/>
            [SuppressMessage("Microsoft.Performance", "CA1822: MarkMembersAsStatic", Justification = "Interface implementation")]
            public void Transform(
                in DestroyStructuresMapping mapping,
                in StructureType structureType,
                FunctionValue sourceFunction,
                Parameter sourceParameter,
                FunctionBuilder functionBuilder)
            {
                var action = new TransformParams(sourceFunction, sourceParameter.Name, mapping);
                structureType.ForEachField(
                    new FieldRef(sourceParameter),
                    functionBuilder,
                    ref action);
            }
        }

        /// <summary cref="StructuresTransformation{TMapping}.GetMapping(IRBuilder, Scope, ParameterMapping)"/>
        protected sealed override DestroyStructuresMapping GetMapping(
            IRBuilder builder,
            Scope scope,
            ParameterMapping parameterMapping)
        {
            var memoryChains = new Dictionary<MemoryValue, (MemoryRef, MemoryRef)>();
            return new DestroyStructuresMapping(
                builder,
                scope,
                parameterMapping,
                memoryChains);
        }

        /// <summary cref="StructuresTransformation{TMapping}.Finish(ref TMapping)"/>
        protected sealed override void Finish(ref DestroyStructuresMapping mapping)
        {
            // Wire memory chains
            foreach (var chainEntry in mapping.MemoryChains)
            {
                MemoryRef.Replace(
                    mapping.Builder,
                    chainEntry.Key,
                    chainEntry.Value.Item1,
                    chainEntry.Value.Item2);
            }
        }

        /// <summary cref="StructuresTransformation{TMapping}.CreateCPSBuilder(ref TMapping, IRBuilder, CFGNode)"/>
        protected sealed override CPSBuilder<CFGNode, CFGNode.Enumerator, FieldRef> CreateCPSBuilder(
            ref DestroyStructuresMapping mapping,
            IRBuilder builder,
            CFGNode entryNode)
        {
            var parameterTransformer = new ParameterTransformer<ICPSBuilder<CFGNode, FieldRef>>(
                this,
                mapping.Scope);
            var provider = new CPSFunctionProvider<
                ParameterTransformer<ICPSBuilder<CFGNode, FieldRef>>, StructureType>(
                mapping,
                parameterTransformer);
            return CPSBuilder<CFGNode, CFGNode.Enumerator, FieldRef>.Create(
                builder,
                entryNode,
                provider);
        }

        /// <summary cref="StructuresTransformation{TMapping}.Transform{TBuilder}(ref TMapping, in CPSBuilder{TBuilder}, Placement.Enumerator)"/>
        protected sealed override bool Transform<TBuilder>(
            ref DestroyStructuresMapping mapping,
            in CPSBuilder<TBuilder> cpsBuilder,
            Placement.Enumerator enumerator)
        {
            bool result = false;
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                switch (current)
                {
                    case Load load:
                        result |= TransformLoad(cpsBuilder, load, mapping);
                        break;
                    case Store store:
                        result |= TransformStore(cpsBuilder, store, mapping);
                        break;
                    case NullValue nullValue:
                        result |= TransformNull(cpsBuilder, nullValue, mapping);
                        break;
                    case GetField getField:
                        result |= TransformGetField(cpsBuilder, getField, mapping);
                        break;
                    case SetField setField:
                        result |= TransformSetField(cpsBuilder, setField, mapping);
                        break;
                }
            }
            return result;
        }

        /// <summary>
        /// Transforms structure field values into distinct scalar values.
        /// </summary>
        readonly struct TransformLocalArguments<TBuilder> : IFieldRefAction<FieldRef>
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            public TransformLocalArguments(
                in CPSBuilder<TBuilder> cpsBuilder)
            {
                CPSBuilder = cpsBuilder;
            }

            public CPSBuilder<TBuilder> CPSBuilder { get; }

            [SuppressMessage("Microsoft.Performance", "CA1822: MarkMembersAsStatic", Justification = "Interface implementation")]
            public FieldRef GetFieldValue(
                in FieldRef fieldRef,
                FieldRef targetFieldRef,
                StructureType structureType,
                int fieldIndex) => targetFieldRef.Access(fieldIndex);

            public void Apply(
                in FieldRef fieldRef,
                FieldRef targetFieldRef,
                StructureType structureType,
                int fieldIndex)
            {
                var sourceValue = CPSBuilder.GetValue(fieldRef);
                CPSBuilder.SetValue(targetFieldRef, sourceValue);
            }
        }

        /// <summary>
        /// Transforms structure field values into distinct call arguments.
        /// </summary>
        readonly struct TransformTopLevelArguments<TBuilder> : IFieldRefAction<object>
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            public TransformTopLevelArguments(
                in CPSBuilder<TBuilder> cpsBuilder,
                ImmutableArray<ValueReference>.Builder arguments)
            {
                CPSBuilder = cpsBuilder;
                Arguments = arguments;
            }

            public CPSBuilder<TBuilder> CPSBuilder { get; }

            public ImmutableArray<ValueReference>.Builder Arguments { get; }

            [SuppressMessage("Microsoft.Performance", "CA1822: MarkMembersAsStatic", Justification = "Interface implementation")]
            public object GetFieldValue(
                in FieldRef fieldRef,
                object _,
                StructureType structureType,
                int fieldIndex) => null;

            public void Apply(
                in FieldRef fieldRef,
                object _,
                StructureType structureType,
                int fieldIndex)
            {
                var sourceValue = CPSBuilder.GetValue(fieldRef);
                Arguments.Add(sourceValue);
            }
        }

        /// <summary cref="StructuresTransformation{TMapping}.TransformCallArguments{TBuilder}(ref TMapping, in CPSBuilder{TBuilder}, CFGNode, FunctionCall)"/>
        protected sealed override void TransformCallArguments<TBuilder>(
            ref DestroyStructuresMapping mapping,
            in CPSBuilder<TBuilder> cpsBuilder,
            CFGNode cfgNode,
            FunctionCall call)
        {
            if (call.IsNonLocalCall)
            {
                // We have to construct an adjusted function call with
                // structure-element arguments

                // CAUTION: It is sufficient to check whether this call
                // is a top-level call. However, it could happen in the future
                // that a local function is called from multiple local functions
                // + this call. In such a case, the other call sites have to
                // be adjusted as well.

                var targetArgs = ImmutableArray.CreateBuilder<ValueReference>(call.NumArguments * 2);

                for (int i = 0, e = call.NumArguments; i < e; ++i)
                {
                    var arg = call.GetArgument(i);
                    if (!TryGetStructure(arg.Type, out StructureType structureType))
                    {
                        targetArgs.Add(arg);
                        continue;
                    }

                    var action = new TransformTopLevelArguments<TBuilder>(cpsBuilder, targetArgs);
                    structureType.ForEachField(
                        new FieldRef(arg),
                        (object)null,
                        ref action);
                }

                var newCall = mapping.Builder.CreateFunctionCall(
                    call.Target,
                    targetArgs.ToImmutable());
                cpsBuilder.SetTerminator(newCall);
            }
            else
            {
                for (int i = 0, e = call.NumArguments; i < e; ++i)
                {
                    var arg = call.GetArgument(i);
                    if (!TryGetStructure(arg.Type, out StructureType structureType))
                        continue;

                    // Set CPS value
                    foreach (var successor in cfgNode.Successors)
                    {
                        var successorFunction = successor.FunctionValue;
                        var targetParam = successorFunction.AttachedParameters[i];

                        var action = new TransformLocalArguments<TBuilder>(cpsBuilder);
                        structureType.ForEachField(
                            new FieldRef(arg),
                            new FieldRef(targetParam),
                            ref action);
                    }
                }
            }
        }

        #endregion
    }
}

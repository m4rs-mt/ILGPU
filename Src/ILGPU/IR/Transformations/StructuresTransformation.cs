// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: StructuresTransformation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FieldRef = ILGPU.IR.Types.StructureType.FieldRef;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Implements a generic transformation that works on structure types.
    /// </summary>
    public abstract class StructuresTransformation : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// Encapsulates a <see cref="CPSBuilder{TNode, TEnumerator, TVariable}"/> and its related
        /// <see cref="CFGNode"/> to simplify CPS construction.
        /// </summary>
        /// <typeparam name="TBuilder">The underlying CPS builder type.</typeparam>
        protected readonly struct CPSBuilder<TBuilder>
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            /// <summary>
            /// Constructs a new CPS builder.
            /// </summary>
            /// <param name="builder">The encapsulated builder.</param>
            /// <param name="currentNode">The current CFG node.</param>
            internal CPSBuilder(TBuilder builder, CFGNode currentNode)
            {
                Debug.Assert(currentNode != null, "Invalid current node");
                Builder = builder;
                CurrentNode = currentNode;
            }

            /// <summary>
            /// Returns the associated CPS builder.
            /// </summary>
            public TBuilder Builder { get; }

            /// <summary>
            /// Returns the current CFG node.
            /// </summary>
            public CFGNode CurrentNode { get; }

            /// <summary>
            /// Returns the value of the given field reference.
            /// </summary>
            /// <param name="fieldRef">The field reference.</param>
            /// <returns>The value of the given field reference.</returns>
            public Value GetValue(FieldRef fieldRef) =>
                Builder.GetValue(CurrentNode, fieldRef);

            /// <summary>
            /// Sets the given field reference to the given value.
            /// </summary>
            /// <param name="fieldRef">The field reference.</param>
            /// <param name="value">The value to set.</param>
            public void SetValue(FieldRef fieldRef, Value value) =>
                Builder.SetValue(CurrentNode, fieldRef, value);

            /// <summary>
            /// Sets the given value as terminator.
            /// </summary>
            /// <param name="value">The value to set.</param>
            public void SetTerminator(Value value) =>
                Builder.SetTerminator(CurrentNode, value);
        }

        /// <summary>
        /// A single parameter mapping that is used to transform parameters.
        /// </summary>
        protected internal readonly struct ParameterMapping
        {
            private readonly Dictionary<FunctionValue, List<(FieldRef, Value)>> parameterMapping;

            /// <summary>
            /// Constructs a new parameter mapping.
            /// </summary>
            /// <param name="parameters">The underlying dictionary.</param>
            internal ParameterMapping(
                Dictionary<FunctionValue, List<(FieldRef, Value)>> parameters)
            {
                parameterMapping = parameters;
            }

            /// <summary>
            /// Registers transformed parameters of the given source function.
            /// </summary>
            /// <param name="sourceFunction">The source function.</param>
            /// <param name="parameterRef">The parameter ref.</param>
            /// <param name="target">The target node.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void MapParameter(
                FunctionValue sourceFunction,
                FieldRef parameterRef,
                Value target)
            {
                Debug.Assert(sourceFunction != null, "Invalid source function");
                Debug.Assert(target != null, "Invalid target");
                if (!parameterMapping.TryGetValue(sourceFunction, out List<(FieldRef, Value)> entries))
                {
                    entries = new List<(FieldRef, Value)>();
                    parameterMapping.Add(sourceFunction, entries);
                }
                entries.Add((parameterRef, target));
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="followUpFlags">The desired flags that indicate passes that should run on the marked function.</param>
        protected StructuresTransformation(
            TransformationFlags flags,
            TransformationFlags followUpFlags)
            : base(flags, followUpFlags, true, false)
        { }

        #endregion
    }

    /// <summary>
    /// Represents an abstract transformation mapping.
    /// </summary>
    public interface IStructuresTransformationMapping
    {
        /// <summary>
        /// Returns the current scope.
        /// </summary>
        Scope Scope { get; }
    }

    /// <summary>
    /// Implements a generic transformation that works on structure types.
    /// </summary>
    /// <typeparam name="TMapping">The custom mapping type.</typeparam>
    public abstract class StructuresTransformation<TMapping> : StructuresTransformation
        where TMapping : IStructuresTransformationMapping
    {
        #region Nested Types

        /// <summary>
        /// A generic parameter transformer that uses intermdiate values
        /// of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The intermediate value type.</typeparam>
        protected interface IParameterTransformer<T>
        {
            /// <summary>
            /// Returns true, iff the given type can be transformed.
            /// </summary>
            /// <param name="mapping">The current mapping.</param>
            /// <param name="sourceFunction">The source function.</param>
            /// <param name="sourceValue">The source value.</param>
            /// <param name="data">The intermediate data.</param>
            /// <returns>True, iff the given parameter can be transformed.</returns>
            bool CanTransform(
                in TMapping mapping,
                FunctionValue sourceFunction,
                Value sourceValue,
                out T data);

            /// <summary>
            /// Transforms the given parameter.
            /// </summary>
            /// <param name="mapping">The current mapping.</param>
            /// <param name="data">The intermediate data.</param>
            /// <param name="sourceFunction">The source function.</param>
            /// <param name="sourceParameter">The source parameter.</param>
            /// <param name="functionBuilder">The target functuion builder.</param>
            void Transform(
                in TMapping mapping,
                in T data,
                FunctionValue sourceFunction,
                Parameter sourceParameter,
                FunctionBuilder functionBuilder);
        }

        /// <summary>
        /// Represents a generic CPS function provider.
        /// </summary>
        /// <typeparam name="TTransformer">The transformer type.</typeparam>
        /// <typeparam name="T">The interdmiate type of a single transformer.</typeparam>
        protected readonly struct CPSFunctionProvider<TTransformer, T> : ICPSBuilderFunctionProvider<CFGNode>
            where TTransformer : IParameterTransformer<T>
        {
            /// <summary>
            /// Stores the builder mapping.
            /// </summary>
            private readonly TMapping mapping;

            /// <summary>
            /// Stores the current transformer;
            /// </summary>
            private readonly TTransformer transformer;

            /// <summary>
            /// Constructs a new CPS function provider.
            /// </summary>
            /// <param name="currentMapping">The current mapping.</param>
            /// <param name="currentTransformer">The used transformer.</param>
            public CPSFunctionProvider(in TMapping currentMapping, in TTransformer currentTransformer)
            {
                mapping = currentMapping;
                transformer = currentTransformer;
            }

            /// <summary cref="ICPSBuilderFunctionProvider{TNode}.GetFunctionBuilder(IRBuilder, TNode)"/>
            public FunctionBuilder GetFunctionBuilder(IRBuilder builder, CFGNode childNode)
            {
                var functionValue = childNode.FunctionValue;
                FunctionBuilder functionBuilder;
                if (functionValue is TopLevelFunction topLevelFunction)
                {
                    functionBuilder = builder.CreateFunction(topLevelFunction.Declaration);

                    topLevelFunction.MemoryParam.Replace(functionBuilder.MemoryParam);
                    topLevelFunction.ReturnParam.Replace(functionBuilder.ReturnParam);
                }
                else
                    functionBuilder = builder.CreateFunction(functionValue.Name);

                foreach (var sourceParam in functionValue.Parameters)
                {
                    if (transformer.CanTransform(
                        mapping,
                        functionValue,
                        sourceParam,
                        out T data))
                    {
                        if (functionValue.IsTopLevel ||
                            functionValue.IsReturnContinuation(mapping.Scope))
                        {
                            // CAUTION: we might require an extended condition at
                            // this point if we allow fused multiple return continuation
                            // calls from different locations.
                            transformer.Transform(
                                mapping,
                                data,
                                functionValue,
                                sourceParam,
                                functionBuilder);
                        }
                    }
                    else
                    {
                        var targetParam = functionBuilder.AddParameter(
                            sourceParam.Type,
                            sourceParam.Name);
                        sourceParam.Replace(targetParam);
                    }
                }

                // Replace function
                functionValue.Replace(functionBuilder.FunctionValue);

                return functionBuilder;
            }

            /// <summary cref="ICPSBuilderFunctionProvider{TNode}.ResolveFunctionCallArgument{TCallback}(FunctionValue, FunctionBuilder, FunctionCall, int, ref TCallback)"/>
            public void ResolveFunctionCallArgument<TCallback>(
                FunctionValue attachedFunction,
                FunctionBuilder targetBuilder,
                FunctionCall currentCall,
                int argumentIdx,
                ref TCallback callback)
                where TCallback : ICPSBuilderFunctionCallArgumentCallback
            {
                Value arg = currentCall.GetArgument(argumentIdx);
                if (transformer.CanTransform(
                    mapping,
                    attachedFunction,
                    arg,
                    out T data))
                    return;
                callback.AddArgument(arg);
            }
        }

        #endregion

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="followUpFlags">The desired flags that indicate passes that should run on the marked function.</param>
        protected StructuresTransformation(
            TransformationFlags flags,
            TransformationFlags followUpFlags)
            : base(flags, followUpFlags)
        { }

        /// <summary>
        /// Resolves an intermediate mapping that is used during all transformation operations.
        /// </summary>
        /// <param name="builder">The IR builder.</param>
        /// <param name="scope">The current scope.</param>
        /// <param name="parameterMapping">The parameter mapping for parameter transformations.</param>
        /// <returns>The created intermdiate mapping.</returns>
        protected abstract TMapping GetMapping(
            IRBuilder builder,
            Scope scope,
            ParameterMapping parameterMapping);

        /// <summary>
        /// Performs final cleanup operations using the given mapping.
        /// </summary>
        /// <param name="mapping">The current mapping.</param>
        protected abstract void Finish(ref TMapping mapping);

        /// <summary>
        /// Creates a new CPS builder.
        /// </summary>
        /// <param name="mapping">The current mapping.</param>
        /// <param name="builder">The current IR builder.</param>
        /// <param name="entryNode">The entry node.</param>
        /// <returns>The created CPS builder.</returns>
        protected abstract CPSBuilder<CFGNode, CFGNode.Enumerator, FieldRef> CreateCPSBuilder(
            ref TMapping mapping,
            IRBuilder builder,
            CFGNode entryNode);

        /// <summary>
        /// Transforms all nodes in the current CFG node.
        /// </summary>
        /// <typeparam name="TBuilder">The CPS builder type.</typeparam>
        /// <param name="mapping">The current mapping.</param>
        /// <param name="cpsBuilder">The CPS builder.</param>
        /// <param name="enumerator">The placement enumerator to enumerate all nodes in the CFG node.</param>
        /// <returns>True, iff at least a single node could be transformed.</returns>
        protected abstract bool Transform<TBuilder>(
            ref TMapping mapping,
            in CPSBuilder<TBuilder> cpsBuilder,
            Placement.Enumerator enumerator)
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>;

        /// <summary>
        /// Transforms all arguments of the given call.
        /// </summary>
        /// <typeparam name="TBuilder">The CPS builder type.</typeparam>
        /// <param name="mapping">The current mapping.</param>
        /// <param name="cpsBuilder">The CPS builder.</param>
        /// <param name="cfgNode">The current CFG node.</param>
        /// <param name="currentCall">The current function call.</param>
        protected abstract void TransformCallArguments<TBuilder>(
            ref TMapping mapping,
            in CPSBuilder<TBuilder> cpsBuilder,
            CFGNode cfgNode,
            FunctionCall currentCall)
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>;

        /// <summary cref="UnorderedTransformation.PerformTransformation(IRBuilder, TopLevelFunction)"/>
        protected sealed override bool PerformTransformation(
            IRBuilder builder,
            TopLevelFunction topLevelFunction)
        {
            var scope = Scope.Create(builder, topLevelFunction);

            var parameterMapping = new Dictionary<FunctionValue, List<(FieldRef, Value)>>();
            var mapping = GetMapping(builder, scope, new ParameterMapping(parameterMapping));

            var cfg = CFG.Create(scope);
            var placement = Placement.CreateCSEPlacement(cfg);
            var cpsBuilder = CreateCPSBuilder(ref mapping, builder, cfg.EntryNode);

            // Setup parameter bindings
            foreach (var paramEntry in parameterMapping)
            {
                var cfgNode = cfg[paramEntry.Key];
                foreach (var paramBinding in paramEntry.Value)
                    cpsBuilder.SetValue(cfgNode, paramBinding.Item1, paramBinding.Item2);
            }

            // Transform values
            bool result = parameterMapping.Count > 0;
            var reversePostOrder = cpsBuilder.ComputeReversePostOrder();
            foreach (var cfgNode in reversePostOrder)
            {
                if (!cpsBuilder.ProcessAndSeal(cfgNode))
                    continue;

                var wrappedCPSBuilder = new CPSBuilder<CPSBuilder<CFGNode, CFGNode.Enumerator, FieldRef>>(
                    cpsBuilder,
                    cfgNode);

                using (var nodeEnumerator = placement[cfgNode])
                    result |= Transform(ref mapping, wrappedCPSBuilder, nodeEnumerator);

                var call = cfgNode.FunctionValue.Target.ResolveAs<FunctionCall>();
                Debug.Assert(call != null, "Invalid function call");
                TransformCallArguments(ref mapping, wrappedCPSBuilder, cfgNode, call);
            }

            // Finish the building process
            cpsBuilder.Finish();
            Finish(ref mapping);

            return result;
        }
    }
}

// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: StructuresTransformation.Create.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using static ILGPU.IR.Types.StructureType;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represens an abstract view specializer that can be used
    /// in the scope of a transformation using the <see cref="SpecializeViews"/>
    /// class.
    /// transformation.
    /// </summary>
    public interface ICreateStructuresSpecializer
    {
        /// <summary>
        /// Returns true iff the given node can be specialized.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>True, iff the given node can be specialized.</returns>
        bool CanSpecialize(Value node);

        /// <summary>
        /// Specializes the given node.
        /// </summary>
        /// <typeparam name="TParameterSpecializer">The specialization type.</typeparam>
        /// <param name="context">The specializer context.</param>
        /// <param name="sourceParameter">The source parameter.</param>
        /// <param name="functionBuilder">The target function builder.</param>
        void SpecializeParameter<TParameterSpecializer>(
            ref TParameterSpecializer context,
            Parameter sourceParameter,
            FunctionBuilder functionBuilder)
            where TParameterSpecializer : struct, ICreateStructuresParameterSpecializationContext;

        /// <summary>
        /// Specializes the given node.
        /// </summary>
        /// <typeparam name="TNodeSpecializer">The specialization type.</typeparam>
        /// <param name="context">The specializer context.</param>
        /// <param name="node">The node.</param>
        bool Specialize<TNodeSpecializer>(
            ref TNodeSpecializer context,
            Value node)
            where TNodeSpecializer : struct, ICreateStructuresSpecializationContext;
    }

    /// <summary>
    /// Represents an abstract parameter specialization context in the scope
    /// of a <see cref="CreateStructures{TSpecializer}"/> transformation.
    /// </summary>
    public interface ICreateStructuresParameterSpecializationContext
    {
        /// <summary>
        /// Returns the current builder.
        /// </summary>
        IRBuilder Builder { get; }

        /// <summary>
        /// Registers transformed parameters of the given source function.
        /// </summary>
        /// <param name="parameter">The source parameter.</param>
        /// <param name="accessChain">The parameter access chain.</param>
        /// <param name="target">The target node.</param>
        void MapParameter(
            Parameter parameter,
            ImmutableArray<int> accessChain,
            Value target);
    }

    /// <summary>
    /// Represents an abstract view specialization context in the scope
    /// of a <see cref="CreateStructures{TSpecializer}"/> transformation.
    /// </summary>
    public interface ICreateStructuresSpecializationContext
    {
        /// <summary>
        /// Returns the current builder.
        /// </summary>
        IRBuilder Builder { get; }

        /// <summary>
        /// The current scope.
        /// </summary>
        Scope Scope { get; }

        /// <summary>
        /// Sets the given variable to the given value.
        /// </summary>
        /// <param name="node">The view node.</param>
        /// <param name="accessChain">The access chain that realizes a field access.</param>
        /// <param name="value">The value to set.</param>
        void SetValue(Value node, ImmutableArray<int> accessChain, Value value);

        /// <summary>
        /// Returns the value of the given variable.
        /// </summary>
        /// <param name="node">The view node.</param>
        /// <param name="accessChain">The access chain that realizes a field access.</param>
        /// <returns>The value of the given variable.</returns>
        Value GetValue(Value node, ImmutableArray<int> accessChain);
    }

    /// <summary>
    /// A custom mapping processor.
    /// </summary>
    public readonly struct CreateStructuresMapping : IStructuresTransformationMapping
    {
        #region Instance

        private readonly StructuresTransformation.ParameterMapping parameterMapping;

        /// <summary>
        /// Constructs a new mapping processor.
        /// </summary>
        /// <param name="builder">The associated builder.</param>
        /// <param name="scope">The associated scope.</param>
        /// <param name="parameters">The mapping of all parameters.</param>
        internal CreateStructuresMapping(
            IRBuilder builder,
            Scope scope,
            StructuresTransformation.ParameterMapping parameters)
        {
            Builder = builder;
            Scope = scope;
            parameterMapping = parameters;
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
            Value target) => parameterMapping.MapParameter(
                sourceFunction,
                parameterRef,
                target);

        #endregion
    }

    /// <summary>
    /// Creates structures values out of scalar ones.
    /// </summary>
    /// <typeparam name="TSpecializer">The specialize type.</typeparam>
    public sealed class CreateStructures<TSpecializer> : StructuresTransformation<CreateStructuresMapping>
        where TSpecializer : ICreateStructuresSpecializer
    {
        #region Nested Types

        /// <summary>
        /// Represents a single context that is used during specialization.
        /// </summary>
        private readonly struct ParameterSpecializationContext : ICreateStructuresParameterSpecializationContext
        {
            private readonly CreateStructuresMapping mapping;
            private readonly FunctionValue functionValue;

            /// <summary>
            /// Constructs a new specialization context.
            /// </summary>
            /// <param name="currentMapping">The current mapping.</param>
            /// <param name="parentFunction">The parent function.</param>
            public ParameterSpecializationContext(
                in CreateStructuresMapping currentMapping,
                FunctionValue parentFunction)
            {
                mapping = currentMapping;
                functionValue = parentFunction;
            }

            /// <summary cref="ICreateStructuresParameterSpecializationContext.Builder"/>
            public IRBuilder Builder => mapping.Builder;

            /// <summary cref="ICreateStructuresParameterSpecializationContext.MapParameter(Parameter, ImmutableArray{int}, Value)"/>
            public void MapParameter(
                Parameter parameter,
                ImmutableArray<int> accessChain,
                Value target) =>
                mapping.MapParameter(functionValue, new FieldRef(parameter, accessChain), target);
        }

        /// <summary>
        /// Represents a single context that is used during specialization.
        /// </summary>
        private readonly struct SpecializationContext<TBuilder> : ICreateStructuresSpecializationContext
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            /// <summary>
            /// Constructs a new specialization context.
            /// </summary>
            /// <param name="builder">The current IR builder.</param>
            /// <param name="scope">The current scope.</param>
            /// <param name="cpsBuilder">The CPS builder.</param>
            public SpecializationContext(
                IRBuilder builder,
                Scope scope,
                in CPSBuilder<TBuilder> cpsBuilder)
            {
                Builder = builder;
                Scope = scope;
                CPSBuilder = cpsBuilder;
            }

            #region Properties

            /// <summary cref="ICreateStructuresSpecializationContext.Builder"/>
            public IRBuilder Builder { get; }

            /// <summary cref="ICreateStructuresSpecializationContext.Scope"/>
            public Scope Scope { get; }

            /// <summary>
            /// Returns the current CPS builder.
            /// </summary>
            public CPSBuilder<TBuilder> CPSBuilder { get; }

            #endregion

            #region Methods

            /// <summary cref="ICreateStructuresSpecializationContext.SetValue(Value, ImmutableArray{int}, Value)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetValue(Value node, ImmutableArray<int> accessChain, Value value) =>
                CPSBuilder.SetValue(new FieldRef(node, accessChain), value);

            /// <summary cref="ICreateStructuresSpecializationContext.GetValue(Value, ImmutableArray{int})"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Value GetValue(Value node, ImmutableArray<int> accessChain) =>
                CPSBuilder.GetValue(new FieldRef(node, accessChain));

            #endregion
        }


        #endregion

        /// <summary>
        /// The desired transformations that should run after
        /// applying this transformation.
        /// </summary>
        private const TransformationFlags FollowUpFlags =
            TransformationFlags.TransformToCPS |
            TransformationFlags.DestroyStructures;

        /// <summary>
        /// The underlying specializer.
        /// </summary>
        private TSpecializer specializer;

        /// <summary>
        /// Constructs a new device specializer.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="usedSpecializer">The associated specializer.</param>
        public CreateStructures(TransformationFlags flags, TSpecializer usedSpecializer)
            : base(flags, FollowUpFlags)
        {
            RequiredTransformationFlags = TransformationFlags.Inlining;
            specializer = usedSpecializer;
        }

        /// <summary cref="ICreateStructuresSpecializer.CanSpecialize(Value)"/>
        private bool CanSpecialize(Value node) => specializer.CanSpecialize(node);

        /// <summary cref="ICreateStructuresSpecializer.SpecializeParameter{TParameterSpecializer}(ref TParameterSpecializer, Parameter, FunctionBuilder)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SpecializeParameter(
            ref ParameterSpecializationContext context,
            Parameter sourceParameter,
            FunctionBuilder functionBuilder)
        {
            specializer.SpecializeParameter(
                ref context,
                sourceParameter,
                functionBuilder);
        }

        /// <summary cref="ICreateStructuresSpecializer.Specialize{TSpecializer}(ref TSpecializer, Value)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Specialize<TBuilder>(
            ref SpecializationContext<TBuilder> context,
            Value node)
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            return specializer.Specialize(ref context, node);
        }

        /// <summary>
        /// Represents a parameter transformer that creates structures by adding parameters.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type.</typeparam>
        private readonly struct ParameterTransformer<TBuilder> : IParameterTransformer<object>
            where TBuilder : ICPSBuilder<CFGNode, FieldRef>
        {
            /// <summary>
            /// Constructs a new function provider.
            /// </summary>
            /// <param name="parent">The parent transformation.</param>
            public ParameterTransformer(CreateStructures<TSpecializer> parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// Returns the parent reference.
            /// </summary>
            public CreateStructures<TSpecializer> Parent { get; }

            /// <summary cref="StructuresTransformation{TMapping}.IParameterTransformer{T}.CanTransform(in TMapping, FunctionValue, Value, out T)"/>
            public bool CanTransform(
                in CreateStructuresMapping mapping,
                FunctionValue sourceFunction,
                Value sourceValue,
                out object outValue)
            {
                outValue = null;
                return Parent.CanSpecialize(sourceValue);
            }

            /// <summary cref="StructuresTransformation{TMapping}.IParameterTransformer{T}.Transform(in TMapping, in T, FunctionValue, Parameter, FunctionBuilder)"/>
            public void Transform(
                in CreateStructuresMapping mapping,
                in object _,
                FunctionValue sourceFunction,
                Parameter sourceParameter,
                FunctionBuilder functionBuilder)
            {
                var context = new ParameterSpecializationContext(mapping, sourceFunction);
                Parent.SpecializeParameter(
                    ref context,
                    sourceParameter,
                    functionBuilder);
            }
        }

        /// <summary cref="StructuresTransformation{TMapping}.GetMapping(IRBuilder, Scope, ParameterMapping)"/>
        protected sealed override CreateStructuresMapping GetMapping(
            IRBuilder builder,
            Scope scope,
            ParameterMapping parameterMapping)
        {
            return new CreateStructuresMapping(builder, scope, parameterMapping);
        }

        /// <summary cref="StructuresTransformation{TMapping}.Finish(ref TMapping)"/>
        protected sealed override void Finish(ref CreateStructuresMapping mapping)
        { }

        /// <summary cref="StructuresTransformation{TMapping}.CreateCPSBuilder(ref TMapping, IRBuilder, CFGNode)"/>
        protected sealed override CPSBuilder<CFGNode, CFGNode.Enumerator, FieldRef> CreateCPSBuilder(
            ref CreateStructuresMapping mapping,
            IRBuilder builder,
            CFGNode entryNode)
        {
            var parameterTransformer = new ParameterTransformer<ICPSBuilder<CFGNode, FieldRef>>(this);
            var provider = new CPSFunctionProvider<
                ParameterTransformer<ICPSBuilder<CFGNode, FieldRef>>, object>(
                mapping,
                parameterTransformer);
            return CPSBuilder<CFGNode, CFGNode.Enumerator, FieldRef>.Create(
                builder,
                entryNode,
                provider);
        }

        /// <summary cref="StructuresTransformation{TMapping}.Transform{TBuilder}(ref TMapping, in CPSBuilder{TBuilder}, Placement.Enumerator)"/>
        protected sealed override bool Transform<TBuilder>(
            ref CreateStructuresMapping mapping,
            in CPSBuilder<TBuilder> cpsBuilder,
            Placement.Enumerator enumerator)
        {
            var context = new SpecializationContext<TBuilder>(
                mapping.Builder,
                mapping.Scope,
                cpsBuilder);
            bool result = false;
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                if (current.IsReplaced ||
                    !CanSpecialize(current))
                    continue;
                Specialize(ref context, current);
                result = true;
            }
            return result;
        }

        /// <summary cref="StructuresTransformation{TMapping}.TransformCallArguments{TBuilder}(ref TMapping, in CPSBuilder{TBuilder}, CFGNode, FunctionCall)"/>
        protected sealed override void TransformCallArguments<TBuilder>(
            ref CreateStructuresMapping mapping,
            in CPSBuilder<TBuilder> cpsBuilder,
            CFGNode cfgNode,
            FunctionCall call)
        { }
    }
}

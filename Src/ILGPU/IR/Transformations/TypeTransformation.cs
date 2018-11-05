// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: TypeTransformation.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents the transformation context in the scope of a
    /// <see cref="TypeTransformation{TTransformation}"/>.
    /// </summary>
    public interface ITypeTransformationContext
    {
        /// <summary>
        /// Returns the current builder.
        /// </summary>
        IRBuilder Builder { get; }

        /// <summary>
        /// The current scope.
        /// </summary>
        Scope Scope { get; }
    }

    /// <summary>
    /// A specification for a <see cref="TypeTransformation{TTransformation}"/>.
    /// </summary>
    public interface ITypeTransformationSpecification
    {
        /// <summary>
        /// Transforms the given basic type into its desired new type.
        /// </summary>
        /// <typeparam name="TContext">The transformation context type.</typeparam>
        /// <param name="context">The transformation context.</param>
        /// <param name="type">The basic type to transform.</param>
        /// <returns>The new type or null.</returns>
        PrimitiveType TransformPrimitiveType<TContext>(
            in TContext context,
            PrimitiveType type)
            where TContext : ITypeTransformationContext;

        /// <summary>
        /// Transforms the specified pointer type.
        /// </summary>
        /// <typeparam name="TContext">The transformation context type.</typeparam>
        /// <param name="context">The transformation context.</param>
        /// <param name="elementType">The pointer element type.</param>
        /// <param name="addressSpace">The pointer address space.</param>
        /// <returns>The new type or null.</returns>
        TypeNode TransformPointerType<TContext>(
            in TContext context,
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
            where TContext : ITypeTransformationContext;

        /// <summary>
        /// Transforms the specified view type.
        /// </summary>
        /// <typeparam name="TContext">The transformation context type.</typeparam>
        /// <param name="context">The transformation context.</param>
        /// <param name="elementType">The view element type.</param>
        /// <param name="addressSpace">The view address space.</param>
        /// <returns>The new type or null.</returns>
        TypeNode TransformViewType<TContext>(
            in TContext context,
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
            where TContext : ITypeTransformationContext;
    }

    /// <summary>
    /// Represents an abstract type replacement pass.
    /// </summary>
    /// <typeparam name="TTransformation">The transformation type.</typeparam>
    /// <remarks>Note that a type transformation transforms all nodes in post order.</remarks>
    public abstract class TypeTransformation<TTransformation> : UnorderedTransformation
        where TTransformation : ITypeTransformationSpecification
    {
        #region Nested Types

        /// <summary>
        /// A new transformation context.
        /// </summary>
        private readonly struct TransformationContext : ITypeTransformationContext
        {
            /// <summary>
            /// Constructs a new internal transformation context.
            /// </summary>
            /// <param name="builder"></param>
            /// <param name="scope"></param>
            public TransformationContext(IRBuilder builder, Scope scope)
            {
                Builder = builder;
                Scope = scope;
            }

            /// <summary cref="ITypeTransformationContext.Builder"/>
            public IRBuilder Builder { get; }

            /// <summary cref="ITypeTransformationContext.Scope"/>
            public Scope Scope { get; }
        }

        /// <summary>
        /// A specific <see cref="IFunctionCloningSpecializer"/> that transforms parameter
        /// types.
        /// </summary>
        private readonly struct CloningSpecializer : IFunctionCloningSpecializer
        {
            /// <summary>
            /// Constructs a new cloning specializer.
            /// </summary>
            /// <param name="parent">The parent type transformation.</param>
            /// <param name="context">The current transformation context.</param>
            public CloningSpecializer(
                TypeTransformation<TTransformation> parent,
                TransformationContext context)
            {
                Debug.Assert(parent != null, "Invalid parent");

                Parent = parent;
                Context = context;
            }

            /// <summary>
            /// Returns the parent type transformation.
            /// </summary>
            public TypeTransformation<TTransformation> Parent { get; }

            /// <summary>
            /// Returns the current transformation context.
            /// </summary>
            public TransformationContext Context { get; }

            /// <summary cref="IFunctionCloningSpecializer.MapType(TypeNode)"/>
            public TypeNode MapType(TypeNode type) => Parent.TransformType(Context, type) ?? type;

            /// <summary cref="IFunctionCloningSpecializer.SpecializeParameter(FunctionValue, Parameter, TypeNode, FunctionBuilder)"/>
            public Parameter SpecializeParameter(
                FunctionValue sourceFunction,
                Parameter sourceParameter,
                TypeNode mappedType,
                FunctionBuilder functionBuilder) =>
                functionBuilder.AddParameter(mappedType, sourceParameter.Name);
        }

        #endregion

        #region Instance

        /// <summary>
        /// The stored transformation.
        /// </summary>
        private TTransformation transformation;

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        /// <param name="flags">The associated transformation flags.</param>
        /// <param name="followUpFlags">The desired flags that indicate passes that should run on the marked function.</param>
        /// <param name="usedTransformation">The transformation specification.</param>
        protected TypeTransformation(
            TransformationFlags flags,
            TransformationFlags followUpFlags,
            TTransformation usedTransformation)
            : base(flags, followUpFlags, true, false)
        {
            transformation = usedTransformation;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to transform a basic type.
        /// </summary>
        /// <typeparam name="TContext">The transformation context type.</typeparam>
        /// <param name="context">The transformation context.</param>
        /// <param name="type">The type to transform.</param>
        /// <returns>True, if the type could be transformed.</returns>
        protected TypeNode TransformType<TContext>(in TContext context, TypeNode type)
            where TContext : ITypeTransformationContext
        {
            if (type.IsMemoryType || type.IsVoidType || type.IsStringType)
                return type;

            if (type is AddressSpaceType addressSpaceType)
            {
                var elementType = TransformType(context, addressSpaceType.ElementType);
                var addressSpace = addressSpaceType.AddressSpace;
                if (addressSpaceType.IsPointerType)
                    return transformation.TransformPointerType(
                        context,
                        elementType,
                        addressSpace);
                else
                    return transformation.TransformViewType(
                        context,
                        elementType,
                        addressSpace);
            }
            else if (type is ContainerType containerType)
            {
                var childTypes = ImmutableArray.CreateBuilder<TypeNode>(
                    containerType.NumChildren);
                foreach (var childType in containerType.Children)
                    childTypes.Add(TransformType(context, childType));
                var children = childTypes.MoveToImmutable();

                if (containerType is StructureType structureType)
                    return context.Builder.CreateStructureType(
                        children,
                        structureType);
                else
                    return context.Builder.CreateFunctionType(
                        children,
                        containerType.Source);
            }
            else if (type is PrimitiveType primitiveType)
                return transformation.TransformPrimitiveType(context, primitiveType);
            else
                throw new NotSupportedException("Not supported type");
        }

        /// <summary>
        /// Transforms the type of the given node.
        /// </summary>
        /// <typeparam name="TContext">The transformation context type.</typeparam>
        /// <param name="context">The transformation context.</param>
        /// <param name="node">The node to transform.</param>
        /// <returns>The transformed node or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Value TransformType<TContext>(in TContext context, Value node)
            where TContext : ITypeTransformationContext
        {
            var builder = context.Builder;
            switch (node)
            {
                // Casts
                case PointerCast pointerCast:
                    return builder.CreatePointerCast(
                        pointerCast.Value,
                        TransformType(context, pointerCast.TargetElementType));
                case ViewCast viewCast:
                    return builder.CreateViewCast(
                        viewCast.Value,
                        TransformType(context, viewCast.TargetElementType));
                case ConvertValue convert:
                    return builder.CreateConvert(
                        convert.Value,
                        TransformType(context, convert.Type), convert.Flags);
                // Constants
                case PrimitiveValue primitiveValue:
                    return builder.CreateConvert(
                        primitiveValue,
                        transformation.TransformPrimitiveType(context, primitiveValue.Type));
                case UndefValue undefValue:
                    return builder.CreateUndef(
                        TransformType(context, undefValue.Type));
                case NullValue nullValue:
                    return builder.CreateNull(
                        TransformType(context, nullValue.Type));
                // Addresses
                case LoadFieldAddress loadFieldAddress:
                    // We have to refresh lfa nodes since they
                    // need update type information
                    return builder.CreateLoadFieldAddress(
                        loadFieldAddress.Source,
                        loadFieldAddress.FieldIndex);
                // Memory
                case Alloca alloca:
                    var allocaElementType = TransformType(context, alloca.AllocaType);
                    var startAllocaParent = builder.CreateUndefMemoryReference();
                    var newAlloca = builder.CreateAlloca(
                        startAllocaParent,
                        alloca.ArrayLength,
                        allocaElementType,
                        alloca.AddressSpace);
                    var endAllocaParent = builder.CreateMemoryReference(newAlloca);
                    MemoryRef.Replace(
                        builder,
                        alloca,
                        startAllocaParent,
                        endAllocaParent);
                    return newAlloca;
            }
            // Note that load and store nodes do not have to be updated
            // since they can refresh their types automatically
            return null;
        }

        /// <summary>
        /// Transforms the type of the given node and returns true if the transformation
        /// was successful.
        /// </summary>
        /// <typeparam name="TContext">The transformation context type.</typeparam>
        /// <param name="context">The transformation context.</param>
        /// <param name="node">The node to transform.</param>
        /// <returns>True, if the transformation was successful.</returns>
        protected bool TransformNode<TContext>(in TContext context, Value node)
            where TContext : ITypeTransformationContext
        {
            var replaced = TransformType(context, node);
            var result = replaced != null && replaced != node;
            if (result)
            {
                Debug.Assert(node.CanBeReplaced, "Node cannot be replaced");
                node.Replace(replaced);
            }
            return result;
        }

        /// <summary>
        /// Transforms all types of all nodes that are defined by a post ordering.
        /// </summary>
        /// <typeparam name="TContext">The transformation context type.</typeparam>
        /// <param name="context">The transformation context.</param>
        /// <param name="postOrder">The post order of all nodes to transform.</param>
        protected abstract void TransformTypes<TContext>(
            in TContext context,
            Scope.PostOrderEnumerator postOrder)
            where TContext : ITypeTransformationContext;

        /// <summary cref="UnorderedTransformation.PerformTransformation(IRBuilder, TopLevelFunction)"/>
        protected sealed override bool PerformTransformation(
            IRBuilder builder,
            TopLevelFunction topLevelFunction)
        {
            var scope = Scope.Create(builder, topLevelFunction);
            var context = new TransformationContext(builder, scope);

            // Handle functions
            var cloningSpecializer = new CloningSpecializer(this, context);
            foreach (var functionValue in scope.Functions)
            {
                builder.CloneAndReplaceSealedFunction(
                    functionValue,
                    cloningSpecializer);
            }

            using (var postOrder = scope.PostOrder)
                TransformTypes(context, postOrder);

            return true;
        }

        #endregion
    }
}

// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: SpecializeViews.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represens an abstract view specializer that can be used
    /// in the scope of a <see cref="SpecializeViews{TSpecializer}"/> transformation.
    /// </summary>
    public interface IViewSpecializer
    {
        /// <summary>
        /// Returns the underlying implementation type that implements a specific view.
        /// </summary>
        /// <typeparam name="TContext">The specialization context.</typeparam>
        /// <param name="context">The specializer context.</param>
        /// <param name="elementType">The view element type.</param>
        /// <param name="addressSpace">The view address space.</param>
        TypeNode CreateImplementationType<TContext>(
            in TContext context,
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
            where TContext : ITypeTransformationContext;

        /// <summary>
        /// Specializes the given node.
        /// </summary>
        /// <typeparam name="TContext">The specialization context.</typeparam>
        /// <param name="context">The specializer context.</param>
        /// <param name="newView">The new view.</param>
        Value Specialize<TContext>(
            in TContext context,
            NewView newView)
            where TContext : ITypeTransformationContext;

        /// <summary>
        /// Specializes the given node.
        /// </summary>
        /// <typeparam name="TContext">The specialization context.</typeparam>
        /// <param name="context">The specializer context.</param>
        /// <param name="getViewLength">The view length property.</param>
        Value Specialize<TContext>(
            in TContext context,
            GetViewLength getViewLength)
            where TContext : ITypeTransformationContext;

        /// <summary>
        /// Specializes the given node.
        /// </summary>
        /// <typeparam name="TContext">The specialization context.</typeparam>
        /// <param name="context">The specializer context.</param>
        /// <param name="subViewValue">The sub view value.</param>
        Value Specialize<TContext>(
            in TContext context,
            SubViewValue subViewValue)
            where TContext : ITypeTransformationContext;

        /// <summary>
        /// Specializes the given node.
        /// </summary>
        /// <typeparam name="TContext">The specialization context.</typeparam>
        /// <param name="context">The specializer context.</param>
        /// <param name="viewElementAddress">The view element address node.</param>
        Value Specialize<TContext>(
            in TContext context,
            LoadElementAddress viewElementAddress)
            where TContext : ITypeTransformationContext;

        /// <summary>
        /// Specializes the given node.
        /// </summary>
        /// <typeparam name="TContext">The specialization context.</typeparam>
        /// <param name="context">The specializer context.</param>
        /// <param name="viewCast">The view cast node.</param>
        Value Specialize<TContext>(
            in TContext context,
            ViewCast viewCast)
            where TContext : ITypeTransformationContext;

        /// <summary>
        /// Specializes the given node.
        /// </summary>
        /// <typeparam name="TContext">The specialization context.</typeparam>
        /// <param name="context">The specializer context.</param>
        /// <param name="addressSpaceCast">The cast node.</param>
        Value Specialize<TContext>(
            in TContext context,
            AddressSpaceCast addressSpaceCast)
            where TContext : ITypeTransformationContext;
    }

    /// <summary>
    /// Wraps a view specializer with an <see cref="ITypeTransformationSpecification"/>.
    /// </summary>
    /// <typeparam name="TSpecializer">The view specializer type.</typeparam>
    public struct SpecializeViewsTypeTransformationAdapter<TSpecializer> : ITypeTransformationSpecification
        where TSpecializer : IViewSpecializer
    {
        private TSpecializer specializer;

        /// <summary>
        /// Constructs a new type-transformation wrapper.
        /// </summary>
        /// <param name="usedSpecializer">The underlying view specializer.</param>
        public SpecializeViewsTypeTransformationAdapter(in TSpecializer usedSpecializer)
        {
            specializer = usedSpecializer;
        }

        /// <summary cref="ITypeTransformationSpecification.TransformPrimitiveType{TContext}(in TContext, PrimitiveType)"/>
        PrimitiveType ITypeTransformationSpecification.TransformPrimitiveType<TContext>(
            in TContext context,
            PrimitiveType type) => type;

        /// <summary cref="ITypeTransformationSpecification.TransformPointerType{TContext}(in TContext, TypeNode, MemoryAddressSpace)"/>
        TypeNode ITypeTransformationSpecification.TransformPointerType<TContext>(
            in TContext context,
            TypeNode elementType,
            MemoryAddressSpace addressSpace) =>
            context.Builder.CreatePointerType(elementType, addressSpace);

        /// <summary cref="ITypeTransformationSpecification.TransformViewType{TContext}(in TContext, TypeNode, MemoryAddressSpace)"/>
        TypeNode ITypeTransformationSpecification.TransformViewType<TContext>(
            in TContext context,
            TypeNode elementType,
            MemoryAddressSpace addressSpace) =>
            specializer.CreateImplementationType(context, elementType, addressSpace);
    }

    /// <summary>
    /// Represents a complete view-type specialization pass.
    /// </summary>
    /// <typeparam name="TSpecializer">The view specializer type.</typeparam>
    public class SpecializeViews<TSpecializer> :
        TypeTransformation<SpecializeViewsTypeTransformationAdapter<TSpecializer>>
        where TSpecializer : IViewSpecializer
    {
        /// <summary>
        /// The stored view specializer.
        /// </summary>
        private TSpecializer specializer;

        /// <summary>
        /// Constructs a new view specializer.
        /// </summary>
        /// <param name="usedSpecializer">The underlying specializer to use.</param>
        public SpecializeViews(TSpecializer usedSpecializer)
            : base(
                  TransformationFlags.SpecializeViews,
                  TransformationFlags.DestroyStructures,
                  new SpecializeViewsTypeTransformationAdapter<TSpecializer>(usedSpecializer))
        {
            specializer = usedSpecializer;
        }

        /// <summary>
        /// Replaces the source node with the specialized one.
        /// </summary>
        /// <param name="source">The source node.</param>
        /// <param name="specialized">The specialized node.</param>
        private static void Transform(Value source, Value specialized)
        {
            if (specialized == null)
                return;

            source.Replace(specialized);
        }

        /// <summary cref="TypeTransformation{TTransformation}.TransformTypes{TContext}(in TContext, Scope.PostOrderEnumerator)"/>
        protected override void TransformTypes<TContext>(
            in TContext context,
            Scope.PostOrderEnumerator postOrder)
        {
            while (postOrder.MoveNext())
            {
                var node = postOrder.Current;
                switch (node)
                {
                    case NewView newView:
                        Transform(node, specializer.Specialize(context, newView));
                        break;
                    case GetViewLength getViewLength:
                        Transform(node, specializer.Specialize(context, getViewLength));
                        break;
                    case SubViewValue subViewValue:
                        Transform(node, specializer.Specialize(context, subViewValue));
                        break;
                    case LoadElementAddress loadViewElementAddress:
                        Transform(node, specializer.Specialize(context, loadViewElementAddress));
                        break;
                    case ViewCast viewCast:
                        Transform(node, specializer.Specialize(context, viewCast));
                        break;
                    case AddressSpaceCast addressSpaceCast:
                        Transform(node, specializer.Specialize(context, addressSpaceCast));
                        break;
                    default:
                        TransformNode(context, node);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Contains helper methods for the creation of <see cref="SpecializeViews{TSpecializer}"/>
    /// transformations.
    /// </summary>
    public static class SpecializeViews
    {
        /// <summary>
        /// Creates a new view specializer transformation.
        /// </summary>
        /// <typeparam name="TSpecializer">The view specializer type.</typeparam>
        /// <param name="specializer">The view specializer.</param>
        /// <returns>The created transformation.</returns>
        public static SpecializeViews<TSpecializer> Create<TSpecializer>(TSpecializer specializer)
            where TSpecializer : IViewSpecializer =>
            new SpecializeViews<TSpecializer>(specializer);

        /// <summary>
        /// Creates a complete transformer that performs a view specialization transformation.
        /// </summary>
        /// <typeparam name="TSpecializer">The specializer type.</typeparam>
        /// <param name="configuration">The transformer configuration.</param>
        /// <param name="specializer">The view specializer.</param>
        /// <returns>The created transformer.</returns>
        public static Transformer CreateTransformer<TSpecializer>(
            TransformerConfiguration configuration,
            TSpecializer specializer)
            where TSpecializer : IViewSpecializer =>
            Transformer.Create(
                configuration,
                new Transformer.TransformSpecification(Create(specializer), 1));
    }
}

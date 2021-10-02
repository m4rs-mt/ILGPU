// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Transformation.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a generic transformation.
    /// </summary>
    public abstract class Transformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an abstract transform execution driver closure.
        /// </summary>
        protected internal interface ITransformExecutor
        {
            /// <summary>
            /// Executes the current transformation.
            /// </summary>
            /// <param name="builder">The current method builder.</param>
            /// <returns>True, if the transformation could be applied.</returns>
            bool Execute(Method.Builder builder);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        protected Transformation() { }

        #endregion

        #region Methods

        /// <summary>
        /// Transforms all method in the given context.
        /// </summary>
        /// <param name="methods">The methods to transform.</param>
        public abstract void Transform(in MethodCollection methods);

        /// <summary>
        /// Transforms the given method using the provided builder.
        /// </summary>
        /// <param name="builder">The current method builder.</param>
        /// <param name="executor">The desired transform executor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal static bool ExecuteTransform<TExecutor>(
            Method.Builder builder,
            in TExecutor executor)
            where TExecutor : struct, ITransformExecutor
        {
            var result = executor.Execute(builder);
            if (result)
            {
                builder.Method.AddTransformationFlags(
                    MethodTransformationFlags.Dirty);
            }

            return result;
        }

        #endregion
    }

    /// <summary>
    /// Represents a generic transformation that can be applied in an unordered manner.
    /// </summary>
    /// <remarks>
    /// Note that this transformation is applied in parallel to all methods.
    /// </remarks>
    public abstract class UnorderedTransformation : Transformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an unordered executor.
        /// </summary>
        private readonly struct Executor : ITransformExecutor
        {
            /// <summary>
            /// Constructs a new executor.
            /// </summary>
            /// <param name="parent">The parent transformation.</param>
            public Executor(UnorderedTransformation parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// The associated parent transformation.
            /// </summary>
            public UnorderedTransformation Parent { get; }

            /// <summary>
            /// Applies the parent transformation.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            /// <returns>True, if the transformation could be applied.</returns>
            public bool Execute(Method.Builder builder) =>
                Parent.PerformTransformation(builder);
        }

        #endregion

        #region Instance

        private readonly Action<Method> transformerDelegate;

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        protected UnorderedTransformation()
        {
            transformerDelegate = (Method method) =>
            {
                var executor = new Executor(this);
                {
                    using var builder = method.CreateBuilder();
                    ExecuteTransform(builder, executor);
                }
            };
        }

        #endregion

        #region Methods

        /// <summary>
        /// Transforms all methods in the given context.
        /// </summary>
        /// <param name="methods">The methods to transform.</param>
        public override void Transform(in MethodCollection methods) =>
            Parallel.ForEach(methods, transformerDelegate);

        /// <summary>
        /// Transforms the given method using the provided builder.
        /// </summary>
        /// <param name="builder">The current method builder.</param>
        protected abstract bool PerformTransformation(Method.Builder builder);

        #endregion
    }

    /// <summary>
    /// Represents a generic transformation that can be applied in an unordered manner.
    /// </summary>
    /// <remarks>
    /// Note that this transformation is applied in parallel to all methods.
    /// </remarks>
    public abstract class UnorderedTransformationWithPrePass : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an unordered executor.
        /// </summary>
        private readonly struct Executor : ITransformExecutor
        {
            /// <summary>
            /// Constructs a new executor.
            /// </summary>
            /// <param name="parent">The parent transformation.</param>
            public Executor(UnorderedTransformationWithPrePass parent)
            {
                Parent = parent;
            }

            /// <summary>
            /// The associated parent transformation.
            /// </summary>
            public UnorderedTransformationWithPrePass Parent { get; }

            /// <summary>
            /// Applies the parent transformation.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            public bool Execute(Method.Builder builder)
            {
                Parent.PrePerformTransformation(builder);
                return false;
            }
        }

        #endregion

        #region Instance

        private readonly Action<Method> transformerDelegate;

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        protected UnorderedTransformationWithPrePass()
        {
            transformerDelegate = (Method method) =>
            {
                var executor = new Executor(this);
                {
                    using var builder = method.CreateBuilder();
                    ExecuteTransform(builder, executor);
                }
            };
        }

        #endregion

        #region Methods

        /// <summary>
        /// Transforms all methods in the given context.
        /// </summary>
        /// <param name="methods">The methods to transform.</param>
        public override void Transform(in MethodCollection methods)
        {
            Parallel.ForEach(methods, transformerDelegate);
            base.Transform(methods);
        }

        /// <summary>
        /// Transforms the given method using the provided builder.
        /// </summary>
        /// <param name="builder">The current method builder.</param>
        /// <remarks>
        /// Note that this method is executed on all methods prior to executing the
        /// <see cref="PrePerformTransformation(Method.Builder)"/> method.
        /// </remarks>
        protected abstract void PrePerformTransformation(Method.Builder builder);

        #endregion
    }

    /// <summary>
    /// Represents a generic transformation that can be applied in an unordered manner.
    /// </summary>
    /// <remarks>
    /// Note that this transformation is applied sequentially to all methods.
    /// </remarks>
    public abstract class SequentialUnorderedTransformation : Transformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an unordered executor.
        /// </summary>
        private readonly struct Executor : ITransformExecutor
        {
            /// <summary>
            /// Constructs a new executor.
            /// </summary>
            /// <param name="parent">The parent transformation.</param>
            /// <param name="context">The context IR context.</param>
            public Executor(
                SequentialUnorderedTransformation parent,
                IRContext context)
            {
                Parent = parent;
                Context = context;
            }

            /// <summary>
            /// The associated parent transformation.
            /// </summary>
            public SequentialUnorderedTransformation Parent { get; }

            /// <summary>
            /// Returns the current IR context in the scope of this transformation.
            /// </summary>
            public IRContext Context { get; }

            /// <summary>
            /// Applies the parent transformation.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            /// <returns>True, if the transformation could be applied.</returns>
            public bool Execute(Method.Builder builder) =>
                Parent.PerformTransformation(Context, builder);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Transforms all methods in the given context.
        /// </summary>
        /// <param name="methods">The methods to transform.</param>
        public override void Transform(in MethodCollection methods)
        {
            foreach (var method in methods)
            {
                var executor = new Executor(this, methods.Context);
                {
                    using var builder = method.CreateBuilder();
                    ExecuteTransform(builder, executor);
                }
            }
        }

        /// <summary>
        /// Transforms the given method using the provided builder.
        /// </summary>
        /// <param name="context">The parent IR context to operate on.</param>
        /// <param name="builder">The current method builder.</param>
        protected abstract bool PerformTransformation(
            IRContext context,
            Method.Builder builder);

        #endregion
    }

    /// <summary>
    /// Represents a generic transformation that can be applied in an unordered manner.
    /// </summary>
    /// <typeparam name="TIntermediate">The type of the intermediate values.</typeparam>
    public abstract class UnorderedTransformation<TIntermediate> : Transformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an unordered executor.
        /// </summary>
        private readonly struct Executor : ITransformExecutor
        {
            /// <summary>
            /// Constructs a new executor.
            /// </summary>
            /// <param name="parent">The parent transformation.</param>
            /// <param name="intermediate">The intermediate value.</param>
            public Executor(
                UnorderedTransformation<TIntermediate> parent,
                TIntermediate intermediate)
            {
                Parent = parent;
                Intermediate = intermediate;
            }

            /// <summary>
            /// The associated parent transformation.
            /// </summary>
            public UnorderedTransformation<TIntermediate> Parent { get; }

            /// <summary>
            /// Returns the associated intermediate value.
            /// </summary>
            public TIntermediate Intermediate { get; }

            /// <summary>
            /// Applies the parent transformation.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            /// <returns>True, if the transformation could be applied.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Execute(Method.Builder builder) =>
                Parent.PerformTransformation(builder, Intermediate);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        protected UnorderedTransformation() { }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new intermediate value.
        /// </summary>
        /// <returns>The resulting intermediate value.</returns>
        protected abstract TIntermediate CreateIntermediate(in MethodCollection methods);

        /// <summary>
        /// Is invoked after all methods have been transformed.
        /// </summary>
        /// <param name="intermediate">The current intermediate value.</param>
        protected abstract void FinishProcessing(TIntermediate intermediate);

        /// <summary>
        /// Transforms all methods in the given context.
        /// </summary>
        /// <param name="methods">The methods to transform.</param>
        public override void Transform(in MethodCollection methods)
        {
            var intermediate = CreateIntermediate(methods);

            // Apply transformation to all methods
            foreach (var method in methods)
            {
                var executor = new Executor(this, intermediate);
                {
                    using var builder = method.CreateBuilder();
                    ExecuteTransform(builder, executor);
                }
            }

            FinishProcessing(intermediate);
        }

        /// <summary>
        /// Transforms the given method using the provided builder.
        /// </summary>
        /// <param name="builder">The current method builder.</param>
        /// <param name="intermediate">The intermediate value.</param>
        protected abstract bool PerformTransformation(
            Method.Builder builder,
            TIntermediate intermediate);

        #endregion
    }

    /// <summary>
    /// Represents a generic transformation that will be applied in the post order
    /// of the induced call graph.
    /// </summary>
    /// <typeparam name="TIntermediate">The type of the intermediate values.</typeparam>
    public abstract class OrderedTransformation<TIntermediate> : Transformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an ordered executor.
        /// </summary>
        private readonly struct Executor : ITransformExecutor
        {
            /// <summary>
            /// Constructs a new executor.
            /// </summary>
            /// <param name="parent">The parent transformation.</param>
            /// <param name="context">The context IR context.</param>
            /// <param name="intermediate">The intermediate value.</param>
            /// <param name="landscape">The current landscape.</param>
            /// <param name="entry">The current landscape entry.</param>
            public Executor(
                OrderedTransformation<TIntermediate> parent,
                IRContext context,
                TIntermediate intermediate,
                Landscape landscape,
                Landscape.Entry entry)
            {
                Parent = parent;
                Context = context;
                Intermediate = intermediate;
                Landscape = landscape;
                Entry = entry;
            }

            /// <summary>
            /// The associated parent transformation.
            /// </summary>
            public OrderedTransformation<TIntermediate> Parent { get; }

            /// <summary>
            /// Returns the current IR context in the scope of this transformation.
            /// </summary>
            public IRContext Context { get; }

            /// <summary>
            /// Returns the associated intermediate value.
            /// </summary>
            public TIntermediate Intermediate { get; }

            /// <summary>
            /// Returns the current landscape.
            /// </summary>
            public Landscape Landscape { get; }

            /// <summary>
            /// Returns the current entry.
            /// </summary>
            public Landscape.Entry Entry { get; }

            /// <summary>
            /// Applies the parent transformation.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            /// <returns>True, if the transformation could be applied.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool Execute(Method.Builder builder) =>
                Parent.PerformTransformation(
                    Context,
                    builder,
                    Intermediate,
                    Landscape,
                    Entry);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        protected OrderedTransformation() { }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new intermediate value.
        /// </summary>
        /// <returns>The resulting intermediate value.</returns>
        protected abstract TIntermediate CreateIntermediate(in MethodCollection methods);

        /// <summary>
        /// Is invoked after all methods have been transformed.
        /// </summary>
        /// <param name="intermediate">The current intermediate value.</param>
        protected abstract void FinishProcessing(in TIntermediate intermediate);

        /// <summary>
        /// Transforms all methods in the given context.
        /// </summary>
        /// <param name="methods">The methods to transform.</param>
        public sealed override void Transform(in MethodCollection methods)
        {
            var landscape = Landscape.Create(methods);
            if (landscape.Count < 1)
                return;

            var intermediate = CreateIntermediate(methods);
            foreach (var entry in landscape)
            {
                var executor = new Executor(
                    this,
                    methods.Context,
                    intermediate,
                    landscape,
                    entry);
                {
                    using var builder = entry.Method.CreateBuilder();
                    ExecuteTransform(builder, executor);
                }
            }
            FinishProcessing(intermediate);
        }

        /// <summary>
        /// Transforms the given method using the provided builder.
        /// </summary>
        /// <param name="context">The parent IR context to operate on.</param>
        /// <param name="builder">The current method builder.</param>
        /// <param name="intermediate">The intermediate value.</param>
        /// <param name="landscape">The global processing landscape.</param>
        /// <param name="current">The current landscape entry.</param>
        protected abstract bool PerformTransformation(
            IRContext context,
            Method.Builder builder,
            in TIntermediate intermediate,
            Landscape landscape,
            Landscape.Entry current);

        #endregion
    }

    /// <summary>
    /// Represents a generic transformation that will be applied in the post order
    /// of the induced call graph.
    /// </summary>
    public abstract class OrderedTransformation : OrderedTransformation<object>
    {
        #region Instance

        /// <summary>
        /// Constructs a new transformation.
        /// </summary>
        protected OrderedTransformation() { }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new intermediate value.
        /// </summary>
        /// <returns>The resulting intermediate value.</returns>
        protected sealed override object CreateIntermediate(
            in MethodCollection methods) => null;

        /// <summary>
        /// Is invoked after all methods have been transformed.
        /// </summary>
        /// <param name="intermediate">The current intermediate value.</param>
        protected sealed override void FinishProcessing(in object intermediate) { }

        /// <summary>
        /// Transforms the given method using the provided builder.
        /// </summary>
        /// <param name="context">The parent IR context to operate on.</param>
        /// <param name="builder">The current method builder.</param>
        /// <param name="intermediate">The intermediate value.</param>
        /// <param name="landscape">The global processing landscape.</param>
        /// <param name="current">The current landscape entry.</param>
        protected sealed override bool PerformTransformation(
            IRContext context,
            Method.Builder builder,
            in object intermediate,
            Landscape landscape,
            Landscape.Entry current) =>
            PerformTransformation(context, builder, landscape, current);

        /// <summary>
        /// Transforms the given method using the provided builder.
        /// </summary>
        /// <param name="context">The parent IR context to operate on.</param>
        /// <param name="builder">The current method builder.</param>
        /// <param name="landscape">The global processing landscape.</param>
        /// <param name="current">The current landscape entry.</param>
        protected abstract bool PerformTransformation(
            IRContext context,
            Method.Builder builder,
            Landscape landscape,
            Landscape.Entry current);

        #endregion
    }
}

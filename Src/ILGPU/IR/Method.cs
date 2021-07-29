// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Method.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend;
using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Intrinsics;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents custom method flags.
    /// </summary>
    [Flags]
    public enum MethodFlags : int
    {
        /// <summary>
        /// No flags (default).
        /// </summary>
        None = 0,

        /// <summary>
        /// This method should be inlined.
        /// </summary>
        Inline = 1 << 0,

        /// <summary>
        /// An external method declaration (without an implementation).
        /// </summary>
        External = 1 << 1,

        /// <summary>
        /// An intrinsic method that requires a backend-specific implementation.
        /// </summary>
        Intrinsic = 1 << 2,

        /// <summary>
        /// Marks entry-point methods.
        /// </summary>
        EntryPoint = 1 << 3,
    }

    /// <summary>
    /// Represents transformation flags.
    /// </summary>
    [Flags]
    public enum MethodTransformationFlags : int
    {
        /// <summary>
        /// No flags (default).
        /// </summary>
        None = 0,

        /// <summary>
        /// This method has been modified since the last GC.
        /// </summary>
        Dirty = 1 << 0,

        /// <summary>
        /// This method has been transformed and does not require further
        /// transformation passes.
        /// </summary>
        Transformed = 1 << 1
    }

    /// <summary>
    /// Represents a method node within the IR.
    /// </summary>
    public sealed partial class Method :
        ValueParent,
        IMethodMappingObject,
        IControlFlowAnalysisSource<Forwards>,
        IDumpable
    {
        #region Nested Types

        /// <summary>
        /// Represents a readonly view on all parameters.
        /// </summary>
        public readonly struct ParameterCollection : IReadOnlyCollection<Parameter>
        {
            #region Nested Types

            /// <summary>
            /// Enumerates all actual (not replaced) parameters.
            /// </summary>
            public struct Enumerator : IEnumerator<Parameter>
            {
                private readonly ImmutableArray<Parameter> parameters;
                private ImmutableArray<Parameter>.Enumerator enumerator;

                /// <summary>
                /// Constructs a new parameter enumerator.
                /// </summary>
                /// <param name="arguments">The parent source array.</param>
                internal Enumerator(ImmutableArray<Parameter> arguments)
                {
                    parameters = arguments;
                    enumerator = parameters.GetEnumerator();
                }

                /// <summary>
                /// Returns the current parameter.
                /// </summary>
                public Parameter Current => enumerator.Current;

                /// <summary cref="IEnumerator.Current"/>
                object IEnumerator.Current => Current;

                /// <summary cref="IDisposable.Dispose"/>
                public void Dispose() { }

                /// <summary cref="IEnumerator.MoveNext"/>
                public bool MoveNext() => enumerator.MoveNext();

                /// <summary cref="IEnumerator.Reset"/>
                void IEnumerator.Reset() => throw new InvalidOperationException();
            }

            #endregion

            #region Instance

            private readonly ImmutableArray<Parameter> parameters;

            /// <summary>
            /// Constructs a new parameter collection.
            /// </summary>
            /// <param name="nodeReferences">The source parameters.</param>
            internal ParameterCollection(ImmutableArray<Parameter> nodeReferences)
            {
                parameters = nodeReferences;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the number of attached parameters.
            /// </summary>
            public int Count => parameters.Length;

            /// <summary>
            /// Returns the i-th parameter.
            /// </summary>
            /// <param name="index">The parameter index.</param>
            /// <returns>The resolved parameter.</returns>
            public Parameter this[int index] => parameters[index];

            #endregion

            #region Methods

            /// <summary>
            /// Returns an enumerator to enumerate all actual (not replaced) parameters.
            /// </summary>
            /// <returns>The enumerator.</returns>
            public Enumerator GetEnumerator() => new Enumerator(parameters);

            /// <summary>
            /// Returns an enumerator to enumerator all actual (not replaced) parameters.
            /// </summary>
            /// <returns>The enumerator.</returns>
            IEnumerator<Parameter> IEnumerable<Parameter>.GetEnumerator() =>
                GetEnumerator();

            /// <summary>
            /// Returns an enumerator to enumerator all actual (not replaced) parameters.
            /// </summary>
            /// <returns>The enumerator.</returns>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #endregion
        }

        /// <summary>
        /// Represents a parameter mapping.
        /// </summary>
        public readonly ref struct ParameterMapping
        {
            #region Instance

            /// <summary>
            /// Constructs a new parameter mapping.
            /// </summary>
            /// <param name="method">The associated method.</param>
            /// <param name="arguments">The parameter arguments.</param>
            internal ParameterMapping(
                Method method,
                ReadOnlySpan<ValueReference> arguments)
            {
                method.AssertNotNull(method);
                method.Assert(arguments.Length == method.Parameters.Count);

                Method = method;
                Arguments = arguments;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated method.
            /// </summary>
            public Method Method { get; }

            /// <summary>
            /// Returns the associated arguments.
            /// </summary>
            public ReadOnlySpan<ValueReference> Arguments { get; }

            /// <summary>
            /// Returns the value that is assigned to the given parameter.
            /// </summary>
            /// <param name="parameter">The parameter to map to a value.</param>
            /// <returns>The mapped value.</returns>
            public readonly ValueReference this[Parameter parameter]
            {
                get
                {
                    Method.AssertNotNull(parameter);
                    Method.Assert(parameter.Method == Method);

                    return Arguments[parameter.Index];
                }
            }

            #endregion
        }

        /// <summary>
        /// Represents a method mapping.
        /// </summary>
        public readonly struct MethodMapping
        {
            #region Instance

            private readonly Dictionary<Method, Method> mapping;

            /// <summary>
            /// Constructs a new method mapping.
            /// </summary>
            /// <param name="methodMapping">The method mapping.</param>
            public MethodMapping(Dictionary<Method, Method> methodMapping)
            {
                mapping = methodMapping;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Resolve the source method to a remapped target method.
            /// </summary>
            /// <param name="source">The source method.</param>
            /// <returns>The resolved target method.</returns>
            public Method this[Method source]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => mapping != null && mapping.TryGetValue(source, out Method target)
                    ? target
                    : source;
            }

            #endregion
        }

        /// <summary>
        /// Represents a location that is bound to a managed method.
        /// </summary>
        public sealed class MethodLocation : Location
        {
            /// <summary>
            /// Constructs a new method location.
            /// </summary>
            /// <param name="method">The target method (if any).</param>
            public MethodLocation(MethodBase method)
            {
                Method = method;
            }

            /// <summary>
            /// Returns the managed method (if any).
            /// </summary>
            public MethodBase Method { get; }

            /// <summary>
            /// Tries to include managed method information if possible.
            /// </summary>
            public override string FormatErrorMessage(string message) =>
                Method != null
                    ? string.Format(
                        ErrorMessages.LocationMethodMessage,
                        message,
                        Method,
                        Method.DeclaringType)
                    : message;
        }

        #endregion

        #region Static

        /// <summary>
        /// Compares two methods according to their id.
        /// </summary>
        internal static readonly new Comparison<Method> Comparison =
            (first, second) => first.Id.CompareTo(second.Id);

        /// <summary>
        /// Resolves <see cref="MethodFlags"/> that represents properties of the
        /// given method base.
        /// </summary>
        /// <param name="methodBase">The method base.</param>
        /// <returns>The resolved method flags.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodFlags ResolveMethodFlags(MethodBase methodBase)
        {
            Debug.Assert(methodBase != null, "Invalid method base");

            // Check general method flags
            if ((methodBase.MethodImplementationFlags &
                 MethodImplAttributes.InternalCall) ==
                MethodImplAttributes.InternalCall)
            {
                return MethodFlags.External;
            }

            // Check for custom intrinsic implementations
            if (methodBase.IsDefined(typeof(IntrinsicImplementationAttribute)))
                return MethodFlags.Intrinsic;

            if (methodBase.IsDefined(typeof(ExternalAttribute)))
                return MethodFlags.External;

            // No custom method flags.
            return MethodFlags.None;
        }

        #endregion

        #region Instance

        /// <summary>
        /// Stores internal transformation flags.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private volatile MethodTransformationFlags transformationFlags =
            MethodTransformationFlags.None;

        /// <summary>
        /// Stores all blocks.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ImmutableArray<BasicBlock> blocks;

        /// <summary>
        /// Stores all parameters.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ImmutableArray<Parameter> parameters = ImmutableArray<Parameter>.Empty;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private volatile Builder builder = null;

        /// <summary>
        /// Creates a new method instance.
        /// </summary>
        /// <param name="baseContext">The context this method belongs to.</param>
        /// <param name="declaration">The associated declaration.</param>
        /// <param name="location">The current location.</param>
        internal Method(
            IRBaseContext baseContext,
            in MethodDeclaration declaration,
            Location location)
            : base(
                location.IsKnown
                    ? location
                    : new MethodLocation(declaration.Source))
        {
            Location.Assert(
                declaration.HasHandle && declaration.ReturnType != null);

            BaseContext = baseContext;
            Declaration = declaration;

            // Create entry block
            EntryBlock = new BasicBlock(this, location, "Entry");
            EntryBlock.BeginControlFlowUpdate(0);
            blocks = ImmutableArray.Create(EntryBlock);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated IR context reference.
        /// </summary>
        public IRBaseContext BaseContext { get; }

        /// <summary>
        /// Returns the associated method name.
        /// </summary>
        public string Name => Declaration.Handle.ToString();

        /// <summary>
        /// Returns the associated method flags.
        /// </summary>
        public MethodFlags Flags => Declaration.Flags;

        /// <summary>
        /// Returns the associated method declaration.
        /// </summary>
        public MethodDeclaration Declaration { get; private set; }

        /// <summary>
        /// Returns the associated method handle.
        /// </summary>
        public MethodHandle Handle => Declaration.Handle;

        /// <summary>
        /// Returns the original source method (may be null).
        /// </summary>
        public MethodBase Source => Declaration.Source;

        /// <summary>
        /// Returns true if the associated source method is not null.
        /// </summary>
        public bool HasSource => Declaration.HasSource;

        /// <summary>
        /// Returns the return-type of the method.
        /// </summary>
        public TypeNode ReturnType => Declaration.ReturnType;

        /// <summary>
        /// Returns true if the return type of the method is void.
        /// </summary>
        public bool IsVoid => ReturnType.IsVoidType;

        /// <summary>
        /// Returns true if this method has an implementation
        /// (no intrinsic or external method).
        /// </summary>
        public bool HasImplementation =>
            !HasFlags(MethodFlags.Intrinsic | MethodFlags.External);

        /// <summary>
        /// Returns the current transformation flags.
        /// </summary>
        public MethodTransformationFlags TransformationFlags => transformationFlags;

        /// <summary>
        /// Returns all attached parameters.
        /// </summary>
        public ParameterCollection Parameters => new ParameterCollection(parameters);

        /// <summary>
        /// Returns the number of attached parameters.
        /// </summary>
        public int NumParameters => parameters.Length;

        /// <summary>
        /// Returns the associated entry block.
        /// </summary>
        public BasicBlock EntryBlock { get; }

        /// <summary>
        /// Returns all attached blocks.
        /// </summary>
        public BasicBlockCollection<ReversePostOrder, Forwards> Blocks =>
            new BasicBlockCollection<ReversePostOrder, Forwards>(EntryBlock, blocks);

        /// <summary>
        /// Returns all attached values.
        /// </summary>
        public BasicBlockCollection<ReversePostOrder, Forwards>.ValueCollection Values =>
            Blocks.Values;

        /// <summary>
        /// Returns the current builder.
        /// </summary>
        public Builder MethodBuilder
        {
            get
            {
                Debug.Assert(builder != null, "Invalid builder");
                return builder;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Computes the exit block.
        /// </summary>
        /// <returns>The exit block.</returns>
        public BasicBlock FindExitBlock() => Blocks.FindExitBlock();

        /// <summary>
        /// Performs an internal GC run.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GC()
        {
            // Check for an active dirty flag
            if (!HasTransformationFlags(MethodTransformationFlags.Dirty))
                return;

            foreach (var block in Blocks)
                block.GC();

            // Remove dirty flag form a transformed method
            RemoveTransformationFlags(MethodTransformationFlags.Dirty);
        }

        /// <summary>
        /// Creates a new parameter mapping.
        /// </summary>
        /// <param name="arguments">The argument values.</param>
        /// <returns>The created parameter mapping.</returns>
        public ParameterMapping CreateParameterMapping(
            in ReadOnlySpan<ValueReference> arguments)
        {
            this.Assert(arguments.Length == Parameters.Count);
            return new ParameterMapping(this, arguments);
        }

        /// <summary>
        /// Dumps this method to the given text writer.
        /// </summary>
        /// <param name="textWriter">The text writer.</param>
        public override void Dump(TextWriter textWriter)
        {
            if (textWriter == null)
                throw new ArgumentNullException(nameof(textWriter));

            textWriter.Write(ToString());
            // Dump parameters
            textWriter.Write('(');
            for (int i = 0, e = NumParameters; i < e; ++i)
            {
                textWriter.Write(Parameters[i].ToParameterString());
                if (i + 1 < e)
                    textWriter.Write(", ");
            }

            textWriter.WriteLine(')');
            Blocks.Dump(textWriter);
        }

        /// <summary>
        /// Seals the current parameters.
        /// </summary>
        /// <param name="parameterArray">The new parameters.</param>
        internal void SealParameters(ImmutableArray<Parameter> parameterArray)
        {
            Debug.Assert(parameters.IsDefaultOrEmpty, "Invalid sealing operation");
            parameters = parameterArray;
        }

        /// <summary>
        /// Creates a new builder for this method.
        /// </summary>
        /// <returns>The created builder.</returns>
        public Builder CreateBuilder()
        {
            var newBuilder = new Builder(this);
            return Interlocked.CompareExchange(ref builder, newBuilder, null) == null
                ? newBuilder
                : throw new InvalidOperationException();
        }

        /// <summary>
        /// Releases the given builder.
        /// </summary>
        /// <param name="oldBuilder">The builder to release.</param>
        internal void ReleaseBuilder(Builder oldBuilder)
        {
            Debug.Assert(oldBuilder != null, "Invalid builder");
            if (Interlocked.CompareExchange(ref builder, null, oldBuilder) != oldBuilder)
                throw new InvalidOperationException();
        }

        /// <summary>
        /// Extracts the current method and all of its dependencies into a separate
        /// context instance.
        /// </summary>
        /// <param name="method">The extracted method reference.</param>
        /// <returns>The created context instance.</returns>
        public IRContext ExtractToContext(out Method method)
        {
            var context = new IRContext(BaseContext.Context);

            // Note that it is safe to call Import here, since we are currently the
            // only owner of the context instance (since it has just been created)
            method = context.Import(this);

            return context;
        }

        /// <summary>
        /// Returns true if this method has the given method flags.
        /// </summary>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, if this method has the given method flags.</returns>
        public bool HasFlags(MethodFlags flags) => Declaration.HasFlags(flags);

        /// <summary>
        /// Adds the given flags to this method.
        /// </summary>
        /// <param name="flags">The flags to add.</param>
        public void AddFlags(MethodFlags flags) =>
            Declaration = Declaration.AddFlags(flags);

        /// <summary>
        /// Removes the given flags from this method.
        /// </summary>
        /// <param name="flags">The flags to remove.</param>
        public void RemoveFlags(MethodFlags flags) =>
            Declaration = Declaration.RemoveFlags(flags);

        /// <summary>
        /// Returns true if this method has the given transformation flags.
        /// </summary>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, if this method has the given transformation flags.</returns>
        public bool HasTransformationFlags(MethodTransformationFlags flags) =>
            (transformationFlags & flags) == flags;

        /// <summary>
        /// Adds the given flags to this method.
        /// </summary>
        /// <param name="flags">The flags to add.</param>
        public void AddTransformationFlags(MethodTransformationFlags flags) =>
            transformationFlags |= flags;

        /// <summary>
        /// Removes the given flags from this method.
        /// </summary>
        /// <param name="flags">The flags to remove.</param>
        public void RemoveTransformationFlags(MethodTransformationFlags flags) =>
            transformationFlags &= ~flags;

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString()
        {
            string result = Name;
            if (Flags != MethodFlags.None)
                result += "[" + Flags.ToString() + "]";
            return result;
        }

        #endregion
    }
}

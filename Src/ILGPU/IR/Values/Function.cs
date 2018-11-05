// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Function.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single function value of the form
    /// f(p0, ..., pn-1): => call ...
    /// </summary>
    public class FunctionValue : InstantiatedValue
    {
        #region Instance

        private TypeNode functionType;

        /// <summary>
        /// Constructs a new function.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="name">The function name (or null).</param>
        internal FunctionValue(
            ValueGeneration generation,
            string name)
            : base(generation, true)
        {
            Name = name ?? "Fn";
            AttachedParameters = ImmutableArray<ValueReference>.Empty;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated type.
        /// </summary>
        public FunctionType FunctionType => Type as FunctionType;

        /// <summary>
        /// Returns the (meaningless) function name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the jump target.
        /// </summary>
        public ValueReference Target => Nodes[0];

        /// <summary>
        /// Returns true iff this function has a valid jump target.
        /// </summary>
        public bool HasTarget => Nodes.Length > 0;

        /// <summary>
        /// Returns all directly attachted parameters.
        /// </summary>
        public ImmutableArray<ValueReference> AttachedParameters { get; private set; }

        /// <summary>
        /// Returns true iff this method is a top-level function.
        /// </summary>
        public bool IsTopLevel => this is TopLevelFunction;

        /// <summary>
        /// Returns all active (non-replaced) parameters.
        /// </summary>
        public ParameterCollection Parameters => new ParameterCollection(AttachedParameters);

        /// <summary cref="Value.Type"/>
        public override TypeNode Type => functionType;

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if this function is used as return continuation.
        /// </summary>
        /// <param name="scope">The current scope.</param>
        /// <returns>True, if this function is used as return continuation.</returns>
        public bool IsReturnContinuation(Scope scope)
        {
            // We have to add 1 to compensate the first call argument
            const int ReturnContinuationOffset = TopLevelFunction.ReturnParameterIndex + 1;

            Debug.Assert(scope != null, "Invalid scope");

            // We require at least a memory return argument
            if (IsTopLevel ||
                AttachedParameters.Length < TopLevelFunction.MemoryParameterIndex + 1)
                return false;

            // Is this function passed as return continuation?
            foreach (var use in scope.GetUses(this))
            {
                if (use.Resolve() is FunctionCall &&
                    use.Index == ReturnContinuationOffset)
                    return true;
            }
            return false;
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder)
        {
            var target = rebuilder.Rebuild(Target);
            var functionBuilder = rebuilder.ResolveFunctionBuilder(this);
            return functionBuilder.Seal(target);
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor)
        {
            visitor.Visit(this);
        }

        /// <summary>
        /// Seals the function type.
        /// </summary>
        /// <param name="type">The function type.</param>

        internal void SealFunctionType(FunctionType type)
        {
            Debug.Assert(type != null, "Invalid function type");
            Debug.Assert(Type == null, "Invalid sealing operation");
            functionType = type;
        }

        /// <summary>
        /// Seals this function.
        /// </summary>
        /// <param name="parameters">The function parameters.</param>
        /// <param name="target">The jump target.</param>
        internal void Seal(
            ImmutableArray<ValueReference> parameters,
            ValueReference target)
        {
            AttachedParameters = parameters;

            var builder = ImmutableArray.CreateBuilder<ValueReference>(parameters.Length + 1);
            builder.Add(target);
            builder.AddRange(parameters);
            Seal(builder.MoveToImmutable());
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => Name;

        /// <summary cref="Value.ToArgString"/>
        protected sealed override string ToArgString()
        {
            var result = new StringBuilder();
            result.Append('(');
            var parameterEnumerator = Parameters.GetEnumerator();
            if (parameterEnumerator.MoveNext())
                result.Append(parameterEnumerator.Current.ToParameterString());
            while (parameterEnumerator.MoveNext())
            {
                result.Append(", ");
                result.Append(parameterEnumerator.Current.ToParameterString());
            }
            result.Append(") => ");
            if (Target.IsValid)
                result.Append(Target.Resolve().ToString());
            else
                result.Append("??");
            return result.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Represents custom function flags.
    /// </summary>
    [Flags]
    public enum TopLevelFunctionFlags : int
    {
        /// <summary>
        /// No flags (default).
        /// </summary>
        None = 0,

        /// <summary>
        /// This function should not be inlined.
        /// </summary>
        NoInlining = 1 << 0,

        /// <summary>
        /// This function should always be inlined.
        /// </summary>
        AggressiveInlining = 1 << 1,

        /// <summary>
        /// An external function declaration (without an implementation).
        /// </summary>
        ExternalDeclaration = 1 << 2,

        /// <summary>
        /// An external function reference (without an implementation).
        /// </summary>
        /// <remarks>Note that such a function is also marked as <see cref="NoInlining"/></remarks>
        External = ExternalDeclaration | NoInlining,
    }

    /// <summary>
    /// Represents transformation flags.
    /// </summary>
    [Flags]
    public enum TopLevelFunctionTransformationFlags : int
    {
        /// <summary>
        /// No flags (default).
        /// </summary>
        None = 0,

        /// <summary>
        /// This function requires a GC step.
        /// </summary>
        Dirty = 1 << 0,

        /// <summary>
        /// The transformation pipeline has been applied.
        /// </summary>
        Transformed = 1 << 1,
    }

    /// <summary>
    /// Represents a special top-level function.
    /// </summary>
    public sealed class TopLevelFunction : FunctionValue, IFunctionMappingObject
    {
        #region Constants

        /// <summary>
        /// The index of the memory parameter.
        /// </summary>
        public const int MemoryParameterIndex = 0;

        /// <summary>
        /// The index of the return parameter.
        /// </summary>
        public const int ReturnParameterIndex = 1;

        /// <summary>
        /// The index of the first parameter.
        /// </summary>
        public const int ParametersOffset = 2;

        #endregion

        #region Instance

        /// <summary>
        /// Stores the internal transformation flags.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private volatile TopLevelFunctionTransformationFlags transformationFlags =
            TopLevelFunctionTransformationFlags.None;

        /// <summary>
        /// Constructs a new top-level function.
        /// </summary>
        /// <param name="generation">The current generation.</param>
        /// <param name="declaration">The associated function declaration.</param>
        internal TopLevelFunction(
            ValueGeneration generation,
            in FunctionDeclaration declaration)
            : base(generation, declaration.Handle.ToString())
        {
            Debug.Assert(
                declaration.HasHandle && declaration.ReturnType != null,
                "Invalid declaration");
            Declaration = declaration;
            AddTransformationFlags(TopLevelFunctionTransformationFlags.Dirty);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated function flags.
        /// </summary>
        public TopLevelFunctionFlags Flags => Declaration.Flags;

        /// <summary>
        /// Returns the associated function declaration.
        /// </summary>
        public FunctionDeclaration Declaration { get; }

        /// <summary>
        /// Returns the associated function handle.
        /// </summary>
        public FunctionHandle Handle => Declaration.Handle;

        /// <summary>
        /// Returns the original source method (may be null).
        /// </summary>
        public MethodBase Source => Declaration.Source;

        /// <summary>
        /// Returns the memory parameter.
        /// </summary>
        public ValueReference MemoryParam => AttachedParameters[MemoryParameterIndex];

        /// <summary>
        /// Returns the return parameter.
        /// </summary>
        public ValueReference ReturnParam => AttachedParameters[ReturnParameterIndex];

        /// <summary>
        /// Returns the return-type of the method.
        /// </summary>
        public TypeNode ReturnType => Declaration.ReturnType;

        /// <summary>
        /// Returns true iff the return type of the method is void.
        /// </summary>
        public bool IsVoid => ReturnType.IsVoidType;

        /// <summary>
        /// Returns the current transformation flags.
        /// </summary>
        public TopLevelFunctionTransformationFlags TransformationFlags => transformationFlags;

        /// <summary>
        /// Returns true if this is an higher-order function.
        /// </summary>
        public bool IsHigherOrder
        {
            get
            {
                var paramIndex = 0;
                foreach (var param in Parameters)
                {
                    if (paramIndex++ < ParametersOffset)
                        continue;
                    if (param.Type.IsFunctionType)

                        return true;
                }
                return false;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns true if this funct4ion has the given function flags.
        /// </summary>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, if this function has the given function flags.</returns>
        public bool HasFlags(TopLevelFunctionFlags flags) =>
            (Flags & flags) == flags;

        /// <summary>
        /// Returns true iff this function has the given transformation flags.
        /// </summary>
        /// <param name="flags">The flags to check.</param>
        /// <returns>True, iff this function has the given transformation flags.</returns>
        public bool HasTransformationFlags(TopLevelFunctionTransformationFlags flags) =>
            (transformationFlags & flags) == flags;

        /// <summary>
        /// Adds the given flags to this function.
        /// </summary>
        /// <param name="flags">The flags to add.</param>
        public void AddTransformationFlags(TopLevelFunctionTransformationFlags flags)
        {
            transformationFlags |= flags;
        }

        /// <summary>
        /// Removes the given flags from this function.
        /// </summary>
        /// <param name="flags">The flags to remove.</param>
        public void RemoveTransformationFlags(TopLevelFunctionTransformationFlags flags)
        {
            transformationFlags &= ~flags;
        }

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder)
        {
            FunctionValue function;
            if (rebuilder.Entry == this)
                function = base.Rebuild(builder, rebuilder) as FunctionValue;
            else
            {
                // This is an external function -> resolve via function map
                function = builder.DeclareFunction(Declaration);
            }

            if (function is TopLevelFunction topLevelFunction)
            {
                topLevelFunction.AddTransformationFlags(
                    transformationFlags & ~TopLevelFunctionTransformationFlags.Dirty);
            }

            return function;
        }

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString()
        {
            var expression = "TL." + base.ToPrefixString();
            if (Flags != TopLevelFunctionFlags.None)
                expression += "@" + Flags.ToString();
            return expression;
        }

        #endregion
    }
}

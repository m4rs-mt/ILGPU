// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Method.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Intrinsic;
using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.Rewriting;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BlockCollection = ILGPUC.IR.MethodValues.BasicBlockCollection<
    ILGPUC.IR.Analyses.ReversePostOrder<ILGPUC.IR.MethodValues.BasicBlock>,
    ILGPUC.IR.MethodValues.Forwards>;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents custom method flags.
/// </summary>
[Flags]
enum MethodFlags : int
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
    /// This method should never be inlined.
    /// </summary>
    NoInline = 1 << 1,

    /// <summary>
    /// An external method declaration (without an implementation).
    /// </summary>
    External = 1 << 2,

    /// <summary>
    /// An intrinsic method that requires a backend-specific implementation.
    /// </summary>
    Intrinsic = 1 << 3,

    /// <summary>
    /// Marks entry-point methods.
    /// </summary>
    EntryPoint = 1 << 4,

    /// <summary>
    /// Marks a method whose body was synthesized from an
    /// <c>IntrinsicAttribute</c>-backed generator rather than
    /// disassembled from IL. Used to memoise the synthesis and to
    /// distinguish reified intrinsics from regular compiled methods
    /// when inspecting method flags.
    /// </summary>
    BodySynthesized = 1 << 5,
}

/// <summary>
/// Represents a method node within the IR.
/// </summary>
sealed partial class Method : ModuleValue,
    IGenerationObject,
    IControlFlowAnalysisSource<Forwards>,
    IValueScope,
    IDumpable
{
    #region Nested Types

    /// <summary>
    /// Represents an enumerator to iterate over all parameters.
    /// </summary>
    /// <param name="method">The parent method.</param>
    internal struct ParameterEnumerator(Method method)
    {
        private int _index = -1;

        /// <inheritdoc cref="IEnumerator.Current"/>
        public readonly Parameter Current => method.Parameters[_index];

        /// <inheritdoc cref="IEnumerator.MoveNext()"/>
        public bool MoveNext() => ++_index < method.NumParameters;
    }

    /// <summary>
    /// Represents a parameter collection of a method.
    /// </summary>
    /// <param name="method">The parent method.</param>
    internal readonly struct ParameterCollection(Method method)
    {
        /// <summary>
        /// Returns the number of parameters.
        /// </summary>
        public readonly int Count => method.NumParameters;

        /// <summary>
        /// Gets the specified parameter.
        /// </summary>
        public Parameter this[int index] => method.GetParameter(index);

        /// <summary>
        /// Returns an enumerator to iterate over all parameters.
        /// </summary>
        public ParameterEnumerator GetEnumerator() => new(method);
    }

    /// <summary>
    /// Represents an enumerator to iterate over all called methods.
    /// </summary>
    /// <param name="method">The parent method.</param>
    internal struct CalledMethodEnumerator(Method method)
    {
        private int _index = -1;

        /// <inheritdoc cref="IEnumerator.Current"/>
        public readonly Method Current => method.CalledMethods[_index];

        /// <inheritdoc cref="IEnumerator.MoveNext()"/>
        public bool MoveNext() => ++_index < method.NumCalledMethods;
    }

    /// <summary>
    /// Represents a collection of called methods.
    /// </summary>
    /// <param name="method">The parent method.</param>
    internal readonly struct CalledMethodCollection(Method method)
    {
        /// <summary>
        /// Returns the number of called methods.
        /// </summary>
        public int Count => method.NumCalledMethods;

        /// <summary>
        /// Returns an enumerator to iterate over all called methods.
        /// </summary>
        public CalledMethodEnumerator GetEnumerator() => new(method);
    }

    /// <summary>
    /// Represents a location that is bound to a managed method.
    /// </summary>
    /// <param name="method">The target method (if any).</param>
    internal sealed class MethodLocation(MethodBase? method) : Location
    {
        /// <summary>
        /// Returns the managed method (if any).
        /// </summary>
        public MethodBase? Method { get; } = method;

        /// <summary>
        /// Tries to include managed method information if possible.
        /// </summary>
        public override string FormatErrorMessage(string message) =>
            Method is not null
            ? string.Format(
                ErrorMessages.LocationMethodMessage,
                message,
                Method,
                Method.DeclaringType)
            : message;
    }

    /// <summary>
    /// A provider that uses registered called methods of a method.
    /// </summary>
    internal readonly struct SuccessorsProvider :
        ITraversalSuccessorsProvider<Method, Forwards>
    {
        /// <summary>
        /// Returns registered successors of a method.
        /// </summary>
        public static ReadOnlySpan<Method> GetSuccessors(Method method) =>
            !method.IsExternal ? method.CalledMethods : [];
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
    public static MethodFlags ResolveMethodFlags(MethodBase methodBase)
    {
        // Check general method flags
        if ((methodBase.MethodImplementationFlags &
            MethodImplAttributes.InternalCall) ==
            MethodImplAttributes.InternalCall)
        {
            return MethodFlags.External;
        }

        // Check for custom intrinsic implementations
        if (methodBase.IsDefined(typeof(IntrinsicAttribute)))
            return MethodFlags.Intrinsic;

        if (methodBase.IsDefined(typeof(ExternalAttribute)))
            return MethodFlags.External;

        // No custom method flags.
        return MethodFlags.None;
    }

    #endregion

    #region Instance

    /// <summary>
    /// The set of all called methods.
    /// </summary>
    private Lazy<ValueSetList<Module, Method>> _calledMethods =
        Utilities.InitNotNullable<Lazy<ValueSetList<Module, Method>>>();

    /// <summary>
    /// Creates a new method instance.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="declaration">The associated declaration.</param>
    public Method(in ModuleValueInitializer initializer, in MethodDeclaration declaration)
        : base(initializer, declaration.ReturnType)
    {
        Location.Assert(
            declaration.HasHandle && declaration.ReturnType is not null);

        Declaration = declaration;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the associated method name.
    /// </summary>
    public string Name => Declaration.Handle.ToRefString();

    /// <summary>
    /// Returns the associated method flags.
    /// </summary>
    public MethodFlags Flags => Declaration.Flags;

    /// <summary>
    /// Returns the associated method declaration.
    /// </summary>
    public MethodDeclaration Declaration { get; private set; }

    /// <summary>
    /// Returns the original source method (may be null).
    /// </summary>
    public MethodBase? Source => Declaration.Source;

    /// <summary>
    /// Returns true if the return type of the method is void.
    /// </summary>
    public bool IsVoid => Type is VoidType;

    /// <summary>
    /// Returns true if this method is an external method without implementation.
    /// </summary>
    public bool IsExternal => HasFlags(MethodFlags.External);

    /// <summary>
    /// Returns true if this method is an intrinsic method requiring
    /// a backend-specific implementation.
    /// </summary>
    public bool IsIntrinsic => HasFlags(MethodFlags.Intrinsic);

    /// <summary>
    /// Returns true if this method is an entry-point method.
    /// </summary>
    public bool IsEntryPoint => HasFlags(MethodFlags.EntryPoint);

    /// <summary>
    /// Returns true if this method is marked for inlining.
    /// </summary>
    public bool IsInline => HasFlags(MethodFlags.Inline);

    /// <summary>
    /// Returns true if this method must not be inlined.
    /// </summary>
    public bool IsNoInline => HasFlags(MethodFlags.NoInline);

    /// <summary>
    /// Returns true if this method has an implementation
    /// (no intrinsic or external method).
    /// </summary>
    public bool HasImplementation => !IsExternal && !IsIntrinsic;

    /// <summary>
    /// Returns the number of attached parameters.
    /// </summary>
    public int NumParameters { get; private set; }

    /// <summary>
    /// Returns the number of methods called from this method.
    /// </summary>
    public int NumCalledMethods => _calledMethods.Value.Count;

    /// <summary>
    /// Returns the internally stored counters
    /// </summary>
    internal int NumValues { get; private set; }

    /// <summary>
    /// Returns the internal global value offset.
    /// </summary>
    internal int MethodOffset { get; private set; }

    /// <summary>
    /// Returns the associated entry block.
    /// </summary>
    public BasicBlock EntryBlock => GetValue<BasicBlock>(0);

    /// <summary>
    /// Returns the associated exit block.
    /// </summary>
    public BasicBlock ExitBlock { get; private set; } =
        Utilities.InitNotNullable<BasicBlock>();

    /// <summary>
    /// Returns all basic blocks.
    /// </summary>
    public BlockCollection Blocks { get; private set; }

    /// <summary>
    /// Returns all attached parameters.
    /// </summary>
    public ParameterCollection Parameters => new(this);

    /// <summary>
    /// Returns all attached methods called.
    /// </summary>
    public ReadOnlySpan<Method> CalledMethods => _calledMethods.Value.AsReadOnlySpan();

    #endregion

    #region Methods

    /// <summary>
    /// Returns the specified parameter.
    /// </summary>
    /// <param name="index">The parameter index.</param>
    private Parameter GetParameter(int index) => GetValue<Parameter>(index + 1);

    /// <summary>
    /// Returns true if the given method is called from this method.
    /// </summary>
    /// <param name="method">The method to be tested.</param>
    /// <returns>True if the given method is called from this method.</returns>
    public bool CallsMethod(Method method) => _calledMethods.Value.Contains(method);

    /// <summary>
    /// Returns the stored exit block.
    /// </summary>
    BasicBlock IControlFlowAnalysisSource<Forwards>.FindExitBlock() => ExitBlock;

    /// <summary>
    /// Sets up the internal method offset.
    /// </summary>
    internal void SetupMethodOffset(int methodOffset) => MethodOffset = methodOffset;

    /// <summary>
    /// Computes all uses.
    /// </summary>
    public override bool ComputeUses(GlobalValueSet visited)
    {
        // External methods have no blocks to traverse, so just skip them.
        if (IsExternal)
            return true;

        // Compute all own uses
        if (!base.ComputeUses(visited)) return false;

        // Iterate over all blocks and compute uses
        foreach (var block in Blocks)
            block.ComputeUses(visited);

        return true;
    }

    /// <summary>
    /// Creates a new set to remember all values in this method.
    /// </summary>
    public ValueSet<Method, T> CreateSet<T>()
        where T : Value<Method> => new(this, NumValues);

    /// <summary>
    /// Creates a new set list to remember all values in this method.
    /// </summary>
    public ValueSetList<Method, T> CreateSetList<T>()
        where T : Value<Method> => new(this, NumValues);

    /// <summary>
    /// Creates a new value map.
    /// </summary>
    /// <typeparam name="TKey">The element type.</typeparam>
    /// <typeparam name="TValue">The value map type.</typeparam>
    /// <param name="valueProvider">The optional default value provider.</param>
    /// <returns>The value map created.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public ValueMap<Method, TKey, TValue> CreateMap<TKey, TValue>(
        Func<TKey, TValue?>? valueProvider = null)
        where TKey : Value<Method>
    {
        var result = new ValueMap<Method, TKey, TValue>(this, NumValues);
        if (valueProvider is null) return result;

        void AddValue(TKey value)
        {
            var valueToAdd = valueProvider(value);
            if (valueToAdd is not null)
                result.Add(value, valueToAdd);
        }

        ForEachValue<TKey>(AddValue);

        return result;
    }

    /// <summary>
    /// Invokes the callback for each <see cref="Value"/> in this method exactly once.
    /// Note that the callback will be invoked in reverse post order of the blocks.
    /// </summary>
    /// <param name="callback">The callback to be invoked.</param>
    public void ForEachValue<TValue>(Action<TValue> callback)
        where TValue : Value<Method>
    {
        ValueSet<Method, TValue>? set = null;
        ForEachValue(callback, ref set);
    }

    /// <summary>
    /// Invokes the callback for each <see cref="Value"/> in this method exactly once.
    /// Note that the callback will be invoked in reverse post order of the blocks.
    /// </summary>
    /// <param name="callback">The callback to be invoked.</param>
    /// <param name="set">The value set to be used (or null to create a new set).</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ForEachValue<TValue>(
        Action<TValue> callback,
        ref ValueSet<Method, TValue>? set)
        where TValue : Value<Method>
    {
        if (ValueUtil<TValue>.IsParameterOrBase)
        {
            foreach (var parameter in Parameters)
                callback(parameter.AsNotNullCast<TValue>());
        }

        foreach (var block in Blocks)
        {
            if (ValueUtil<TValue>.IsBasicBlockOrBase)
                callback(block.AsNotNullCast<TValue>());
            block.ForEachValue(callback, ref set);
        }
    }

    /// <summary>
    /// Dumps this method to the given text writer.
    /// </summary>
    /// <param name="textWriter">The text writer.</param>
    public override void Dump(TextWriter textWriter) =>
        new IRPrinter(textWriter).Print(this);

    /// <inheritdoc cref="IDumpable.Dump(TextWriter, IRDumpMode, IRDumpPoint)"/>
    public void Dump(
        TextWriter textWriter,
        IRDumpMode mode,
        IRDumpPoint point = IRDumpPoint.None) =>
        Dump(textWriter, mode, IRPrinterFormat.ILGPU, point, annotation: null);

    /// <inheritdoc
    ///     cref="IDumpable.Dump(TextWriter, IRDumpMode, IRPrinterFormat, IRDumpPoint)"/>
    public void Dump(
        TextWriter textWriter,
        IRDumpMode mode,
        IRPrinterFormat format,
        IRDumpPoint point = IRDumpPoint.None) =>
        Dump(textWriter, mode, format, point, annotation: null);

    /// <summary>
    /// Dumps this method to the given text writer using the specified dump mode,
    /// printer format, pipeline point, and optional annotation comment.
    /// </summary>
    /// <param name="textWriter">The text writer.</param>
    /// <param name="mode">The dump mode to use.</param>
    /// <param name="format">The printer format to use.</param>
    /// <param name="point">The pipeline stage at which the dump is taken.</param>
    /// <param name="annotation">
    /// Optional annotation written as a header comment before the IR body.
    /// </param>
    public void Dump(
        TextWriter textWriter,
        IRDumpMode mode,
        IRPrinterFormat format,
        IRDumpPoint point,
        string? annotation)
    {
        if (annotation is not null)
            textWriter.WriteLine($"; stage: {point}  {annotation}");
        bool debugIds = mode == IRDumpMode.Raw;
        if (format == IRPrinterFormat.LLVM)
            new LLVMIRPrinter(textWriter, point, debugIds).Print(this);
        else
            new IRPrinter(textWriter, point).Print(this);
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
    /// Returns an enumerator to enumerate all called method.
    /// </summary>
    /// <returns>An enumerator to enumerate all called methods.</returns>
    public CalledMethodEnumerator GetCalledMethodEnumerator() => new(this);

    /// <summary>
    /// Seals this method using the given information provided by the parent builder.
    /// </summary>
    /// <param name="numValues">The number of values.</param>
    /// <param name="exitBlock">The unique exit block.</param>
    /// <param name="values">All method values.</param>
    /// <param name="blocks">The collection of all blocks.</param>
    internal void SealMethod(
        int numValues,
        BasicBlock exitBlock,
        ref ValueBuilderList values,
        BlockCollection blocks)
    {
        NumValues = numValues;
        _calledMethods = new(
            [MethodImpl(MethodImplOptions.AggressiveOptimization)] () =>
        {
            // Collect all Methods reachable from this method's body. Includes:
            // - Methods called directly via MethodCall
            // - Methods referenced as values by any other BasicBlockValue
            //   operand (e.g. a WarpReduce whose custom operation is a lambda
            //   Method). These must be kept alive during module sealing's RPO
            //   traversal and visited by transformation passes even though
            //   they are not "called" in the CFG sense.
            var calledMethods = Module.CreateSetList<Method>();
            foreach (var block in Blocks)
            {
                foreach (var (bbValue, _) in block.BasicBlockValues)
                {
                    if (bbValue is MethodCall call)
                    {
                        calledMethods.Add(call.Target);
                    }
                    else
                    {
                        foreach (var operand in bbValue.Values)
                        {
                            if (operand is Method m)
                                calledMethods.Add(m);
                        }
                    }
                }
            }
            return calledMethods;
        }, isThreadSafe: true);

        NumParameters = values.Count - 1;
        ExitBlock = exitBlock;
        Blocks = blocks;

        Seal(ref values);
    }

    /// <summary>
    /// Rewrites the current method completely.
    /// </summary>
    /// <typeparam name="TRewriter">
    /// The rewriter type to control rewriting of values and value classes.
    /// </typeparam>
    /// <param name="rewriter">The current rewriter.</param>
    /// <returns>The rewritten value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public BlockCollection Rewrite<
        TRewriter>(in TRewriter rewriter)
        where TRewriter : IMethodRewriter, allows ref struct
    {
        // Try to narrow context
        rewriter.Generation.ValidateCurrentOrPreviousGeneration(this);

        // Get new entry block
        var entryBlock = rewriter.RewriteAs<BasicBlock>(EntryBlock);

        // Traverse the space to determine all new blocks
        var newBlocks = entryBlock.TraverseToCollection<
            ReversePostOrder<BasicBlock>,
            BasicBlock.SuccessorsProvider<Forwards>,
            Forwards>(rewriter.Builder.NumBasicBlocks);

        return newBlocks;
    }

    #endregion

    #region Object

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() =>
        Declaration.Source is not null ? Declaration.Source.Name : Declaration.ToString();

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        Flags != MethodFlags.None ? $"[{Flags}]" : base.ToArgString();

    /// <summary>
    /// Returns the hash code of the value id.
    /// </summary>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Returns true if the given value is exactly this value.
    /// </summary>
    public sealed override bool Equals(object? obj) =>
        obj is Method method && method.Id == Id;

    #endregion
}

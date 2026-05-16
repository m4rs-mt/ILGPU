// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Module.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Util;
using ILGPUC.IR.ModuleValues.Construction;
using ILGPUC.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents a completely self contained module.
/// </summary>
sealed class Module : IGenerationObject, IValueScope, IDumpable
{
    #region Nested Types

    /// <summary>
    /// All methods in reverse post order.
    /// </summary>
    /// <param name="module">The parent module.</param>
    internal readonly struct ReversePostOrderMethodsCollection(Module module)
    {
        /// <summary>
        /// Represents an enumerator to iterate over all methods.
        /// </summary>
        /// <param name="module">The parent module.</param>
        internal struct Enumerator(Module module)
        {
            private int _index = -1;

            /// <inheritdoc cref="IEnumerator.Current"/>
            public readonly Method Current => module.GetMethod(_index);

            /// <inheritdoc cref="IEnumerator.MoveNext()"/>
            public bool MoveNext() => ++_index < module.NumMethods;
        }

        /// <summary>
        /// Returns the number of methods in this module.
        /// </summary>
        public int Count => module.NumMethods;

        /// <summary>
        /// Returns the ith method.
        /// </summary>
        public Method this[int index] => module.GetMethod(index);

        /// <summary>
        /// Returns an enumerator to iterate over all methods.
        /// </summary>
        public Enumerator GetEnumerator() => new(module);

        /// <summary>
        /// Converts this collection into an inline list.
        /// </summary>
        /// <returns>The created list.</returns>
        public InlineList<Method> ToInlineList()
        {
            var result = InlineList<Method>.Create(Count);
            foreach (var method in this)
                result.Add(method);
            return result;
        }
    }

    /// <summary>
    /// All methods in post order.
    /// </summary>
    /// <param name="module">The parent module.</param>
    internal readonly struct PostOrderMethodsCollection(Module module)
    {
        /// <summary>
        /// Represents an enumerator to iterate over all methods.
        /// </summary>
        /// <param name="module">The parent module.</param>
        internal struct Enumerator(Module module)
        {
            private int _index = module.NumMethods;

            /// <inheritdoc cref="IEnumerator.Current"/>
            public readonly Method Current => module.GetMethod(_index);

            /// <inheritdoc cref="IEnumerator.MoveNext()"/>
            public bool MoveNext() => --_index >= 0;
        }

        /// <summary>
        /// Returns the number of methods in this module.
        /// </summary>
        public int Count => module.NumMethods;

        /// <summary>
        /// Returns the ith method.
        /// </summary>
        public Method this[int index] => module.GetMethod(Count - index - 1);

        /// <summary>
        /// Returns an enumerator to iterate over all methods.
        /// </summary>
        public Enumerator GetEnumerator() => new(module);

        /// <summary>
        /// Converts this collection into an inline list.
        /// </summary>
        /// <returns>The created list.</returns>
        public InlineList<Method> ToInlineList()
        {
            var result = InlineList<Method>.Create(Count);
            foreach (var method in this)
                result.Add(method);
            return result;
        }
    }

    #endregion

    #region Instance

    private readonly BasicValueTypeMap<PrimitiveType> _basicValueTypes = [];
    private readonly Lazy<Dictionary<MethodHandle, int>> _methodLookup;
    private ReadOnlyMemory<Method> _methods;
    private ReadOnlyMemory<Global> _globals;
    private ReadOnlyMemory<TypeValue> _types;

    /// <summary>
    /// Constructs a new module.
    /// </summary>
    /// <param name="generation">The current generation.</param>
    /// <param name="location">The current location.</param>
    internal Module(Generation generation, Location location)
    {
        _methodLookup = new(() =>
        {
            var result = new Dictionary<MethodHandle, int>(_methods.Length);
            for (int i = 0; i < _methods.Length; ++i)
                result.Add(_methods.Span[i].Declaration.Handle, i);
            return result;
        });

        Generation = generation;
        Location = location;
    }

    /// <summary>
    /// Initializes all internal types.
    /// </summary>
    /// <param name="moduleBuilder">The parent module builder.</param>
    internal void Initialize(ModuleBuilder moduleBuilder)
    {
        foreach (var type in Enum.GetValues<BasicValueType>())
            _basicValueTypes.Add(type, moduleBuilder.GetPrimitiveType(type));

        MathMode = moduleBuilder.Properties.MathMode;

        TargetPlatform = moduleBuilder.TargetPlatform;
        IntPointerArithmeticType = moduleBuilder.IntPointerArithmeticType;
        IntPointerType = moduleBuilder.IntPointerType;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The value id of this module.
    /// </summary>
    public ValueId Id { get; } = ValueId.CreateNew();

    /// <summary>
    /// Returns the current generation.
    /// </summary>
    public Generation Generation { get; }

    /// <summary>
    /// Returns the current location.
    /// </summary>
    public Location Location { get; }

    /// <summary>
    /// Returns the current math mode.
    /// </summary>
    public MathMode MathMode { get; private set; }

    /// <summary>
    /// Returns the main entry point handle.
    /// </summary>
    public MethodHandle EntryPointHandle { get; private set; }

    /// <summary>
    /// Returns the main entry point method.
    /// </summary>
    public Method EntryPoint => this[EntryPointHandle];

    /// <summary>
    /// Returns the method that corresponds to the given method handle.
    /// </summary>
    /// <param name="methodHandle">The method handle to map.</param>
    /// <returns>The mapped method corresponding to the given handle.</returns>
    public Method this[MethodHandle methodHandle] =>
        GetMethod(_methodLookup.Value[methodHandle]);

    /// <summary>
    /// Returns the total number of children.
    /// </summary>
    public int Count => NumMethods + NumGlobals + NumTypes;

    /// <summary>
    /// Returns the number of methods directly attached.
    /// </summary>
    public int NumMethods => _methods.Length;

    /// <summary>
    /// Returns the number of globals directly attached.
    /// </summary>
    public int NumGlobals => _globals.Length;

    /// <summary>
    /// Returns the number of types directly attached.
    /// </summary>
    public int NumTypes => _types.Length;

    /// <summary>
    /// Returns the number of all direct values in this module.
    /// </summary>
    public int NumValues { get; private set; }

    /// <summary>
    /// Returns the total number of all values in this module.
    /// </summary>
    public int TotalNumValues { get; private set; }

    /// <summary>
    /// Returns all directly attached methods.
    /// </summary>
    public ReadOnlySpan<Method> Methods => _methods.Span;

    /// <summary>
    /// Returns all directly attached globals.
    /// </summary>
    public ReadOnlySpan<Global> Globals => _globals.Span;

    /// <summary>
    /// Returns all directly attached types.
    /// </summary>
    public ReadOnlySpan<TypeValue> Types => _types.Span;

    /// <summary>
    /// Returns all directly attached methods in post order.
    /// </summary>
    public PostOrderMethodsCollection MethodsInPostOrder => new(this);

    /// <summary>
    /// Returns all directly attached methods in reverse post order.
    /// </summary>
    public ReversePostOrderMethodsCollection MethodsInReversePostOrder => new(this);

    #endregion

    #region Pointers

    /// <summary>
    /// Returns the current target platform.
    /// </summary>
    public TargetPlatform TargetPlatform { get; private set; }

    /// <summary>
    /// Returns the arithmetic type of native pointer.
    /// </summary>
    public ArithmeticBasicValueType IntPointerArithmeticType { get; private set; }

    /// <summary>
    /// Returns the basic type of native pointer.
    /// </summary>
    public BasicValueType PointerBasicValueType => IntPointerType.BasicValueType;

    /// <summary>
    /// Returns the type of native pointer.
    /// </summary>
    public PrimitiveType IntPointerType { get; private set; } =
        Utilities.InitNotNullable<PrimitiveType>();

    /// <summary>
    /// Returns the pointer size of a native pointer type.
    /// </summary>
    public int PointerSize => IntPointerType.Size;

    #endregion

    #region Methods

    /// <summary>
    /// Creates a new method set that can hold all methods of this module.
    /// </summary>
    /// <returns>The newly created method set.</returns>
    public ValueSet<Module, T> CreateSet<T>()
        where T : Value<Module> =>
        new(this, NumValues);

    /// <summary>
    /// Creates a new method set list that can hold all methods of this module.
    /// </summary>
    /// <returns>The newly created method set list.</returns>
    public ValueSetList<Module, T> CreateSetList<T>()
        where T : Value<Module> =>
        new(this, NumValues);

    /// <summary>
    /// Creates a new type map that can hold all methods of this module.
    /// </summary>
    /// <returns>The newly created type map.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public ValueMap<Module, TKey, TValue> CreateMap<TKey, TValue>(
        Func<TKey, TValue?>? provider = null)
        where TKey : Value<Module>
    {
        var mapping = new ValueMap<Module, TKey, TValue>(this, NumValues);
        if (provider is null) return mapping;

        if (ValueUtil<TKey>.IsMethod)
        {
            foreach (var method in MethodsInReversePostOrder)
            {
                var key = method.AsNotNullCast<TKey>();
                var mapped = provider(key);
                if (mapped is not null) mapping.Add(key, mapped);
            }
        }

        if (ValueUtil<TKey>.IsGlobal)
        {
            foreach (var global in Globals)
            {
                var key = global.AsNotNullCast<TKey>();
                var mapped = provider(key);
                if (mapped is not null) mapping.Add(key, mapped);
            }
        }

        if (ValueUtil<TKey>.IsType)
        {
            foreach (var type in Types)
            {
                var key = type.AsNotNullCast<TKey>();
                var mapped = provider(key);
                if (mapped is not null) mapping.Add(key, mapped);
            }
        }

        return mapping;
    }

    /// <summary>
    /// Creates a global set.
    /// </summary>
    /// <param name="capacity">The optionally desired capacity</param>
    /// <returns>The global set ready to used.</returns>
    public GlobalValueSet CreateGlobalSet(int? capacity = null) =>
        new(Generation, Math.Max(capacity ?? TotalNumValues, 16));

    /// <summary>
    /// Creates a global map.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="capacity">The optionally desired capacity</param>
    /// <returns>The global map ready to be used.</returns>
    public GlobalValueMap<TValue> CreateGlobalMap<TValue>(int? capacity = null) =>
        new(Generation, Math.Max(capacity ?? TotalNumValues, 16));

    /// <summary>
    /// Resolves the primitive type that corresponds to the given
    /// <see cref="BasicValueType"/>.
    /// </summary>
    /// <param name="basicValueType">The basic value type.</param>
    /// <returns>The created primitive type.</returns>
    public PrimitiveType GetPrimitiveType(BasicValueType basicValueType) =>
        _basicValueTypes[basicValueType];

    /// <summary>
    /// Returns the i-th method.
    /// </summary>
    /// <param name="index">The index of the method to access.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Method GetMethod(int index) => Methods[index];

    /// <summary>
    /// Invokes the given action for each module value in parallel.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="action">
    /// The action to invoke for each module value of the given type.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ForEachValue<TValue>(Action<TValue> action)
        where TValue : ModuleValue
    {
        if (ValueUtil<TValue>.IsMethodOrBase)
        {
            foreach (var method in Methods)
                action(method.AsNotNullCast<TValue>());
        }

        if (ValueUtil<TValue>.IsGlobalOrBase)
        {
            foreach (var global in Globals)
                action(global.AsNotNullCast<TValue>());
        }

        if (ValueUtil<TValue>.IsTypeOrBase)
        {
            foreach (var type in Types)
                action(type.AsNotNullCast<TValue>());
        }
    }

    /// <summary>
    /// Dumps this module to the given text writer.
    /// </summary>
    /// <param name="textWriter">The text writer.</param>
    public void Dump(TextWriter textWriter) =>
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
    /// Dumps this module to the given text writer using the specified dump mode,
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
    /// Seals this module using the given typing information and globals.
    /// </summary>
    /// <param name="numValues">Number of values to be collected.</param>
    /// <param name="totalNumValues">Total number of values in this module.</param>
    /// <param name="entryPoint">The main entry point to be called.</param>
    /// <param name="methods">All methods in this module.</param>
    /// <param name="globals">All globals in this module.</param>
    /// <param name="types">All types in this module.</param>
    internal void Seal(
        int numValues,
        int totalNumValues,
        ReadOnlyMemory<Method> methods,
        ReadOnlyMemory<Global> globals,
        ReadOnlyMemory<TypeValue> types,
        MethodHandle entryPoint)
    {
        if (methods.Length < 1)
            throw new InvalidOperationException();

        NumValues = numValues;
        TotalNumValues = totalNumValues;

        _methods = methods;
        _globals = globals;
        _types = types;

        EntryPointHandle = entryPoint;
    }

    /// <summary>
    /// Returns a flat string representation of this module to be referenced.
    /// </summary>
    /// <returns>A flat module string representation.</returns>
    public override string ToString() => $"Module_{Id}[{Generation}]";

    #endregion
}

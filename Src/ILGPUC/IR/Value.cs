// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Value.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using UseList = ILGPU.Util.InlineList<ILGPUC.IR.Use>;
using ValueList = ILGPU.Util.InlineList<ILGPUC.IR.Value>;

namespace ILGPUC.IR;

/// <summary>
/// The base interface of all values.
/// </summary>
interface IValue : ILocation, IDumpable, IGenerationObject
{
    /// <summary>
    /// Returns the unique value id.
    /// </summary>
    ValueId Id { get; }

    /// <summary>
    /// Returns the current value kind.
    /// </summary>
    ValueKind ValueKind { get; }

    /// <summary>
    /// Returns the current value class.
    /// </summary>
    ValueClass ValueClass { get; }

    /// <summary>
    /// Returns the type of this value.
    /// </summary>
    TypeValue Type { get; }

    /// <summary>
    /// Returns all nested child values.
    /// </summary>
    ReadOnlySpan<Value> Values { get; }

    /// <summary>
    /// Returns all associated uses.
    /// </summary>
    UseCollection Uses { get; }

    /// <summary>
    /// Returns the number of uses.
    /// </summary>
    int NumUses { get; }

    /// <summary>
    /// Accepts the given visitor.
    /// </summary>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    void Accept<TVisitor>(TVisitor visitor) where TVisitor : IValueVisitor;
}

/// <summary>
/// The base interface of all values in a specific scope.
/// </summary>
/// <typeparam name="TScope">The scope marker type.</typeparam>
interface IValue<TScope> : IValue where TScope : class, IValueScope
{
    /// <summary>
    /// Returns the parent scope.
    /// </summary>
    TScope Scope { get; }
}

/// <summary>
/// The base interface of all values.
/// </summary>
interface IValueInformation
{
    /// <summary>
    /// Returns the current value kind.
    /// </summary>
    static abstract ValueKind ValueKind { get; }

    /// <summary>
    /// Returns the current value group.
    /// </summary>
    static abstract ValueGroup ValueGroup { get; }
}

/// <summary>
/// The base interface of class values.
/// </summary>
interface IValueClassInformation
{
    /// <summary>
    /// Returns the value class.
    /// </summary>
    static abstract ValueClass ValueClass { get; }
}

/// <summary>
/// A general value initializer.
/// </summary>
/// <param name="Module">The parent module instance.</param>
/// <param name="Location">The associated location.</param>
readonly record struct ValueInitializer(Module Module, Location Location) : ILocation
{
    /// <summary>
    /// Formats an error message to include specific location information.
    /// </summary>
    string ILocation.FormatErrorMessage(string message) =>
        Location.FormatErrorMessage(message);
}

// TODO: Implement global lookup key!!
// Alternatively: support local lookup key with offsets given

/// <summary>
/// Represents a scoped value parent.
/// </summary>
interface IValueScope : IGenerationObject;

/// <summary>
/// Represents a basic intermediate-representation node.
/// It is the base class for all nodes in the scope of this IR.
/// </summary>
/// <param name="initializer">The current initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class Value(in ValueInitializer initializer, TypeValue type) : IValue
{
    #region Static

    /// <summary>
    /// Compares two nodes according to their id.
    /// </summary>
    internal static readonly Comparison<Value> Comparison =
        (first, second) => first.Id.CompareTo(second.Id);

    /// <summary>
    /// Compares two nodes according to their id.
    /// </summary>
    internal readonly struct Comparer : IEqualityComparer<Value>
    {
        public bool Equals(Value? x, Value? y) => x?.Id == y?.Id;
        public int GetHashCode([DisallowNull] Value obj) => obj.Id.GetHashCode();
    }

    #endregion

    #region Instance

    /// <summary>
    /// The list of all values.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ValueList _values = ValueList.Empty;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private UseList _uses = UseList.Create(4);

    #endregion

    #region Properties

    /// <summary>
    /// Returns the unique node id.
    /// </summary>
    public ValueId Id { get; } = ValueId.CreateNew();

    /// <summary>
    /// Returns the parent module.
    /// </summary>
    public Module Module { get; } = initializer.Module;

    /// <summary>
    /// Returns the current generation.
    /// </summary>
    public Generation Generation => Module.Generation;

    /// <summary>
    /// Returns the associated location.
    /// </summary>
    public Location Location { get; private set; } = initializer.Location;

    /// <summary>
    /// Returns the current value kind.
    /// </summary>
    public abstract ValueKind ValueKind { get; }

    /// <summary>
    /// Returns the current value group.
    /// </summary>
    public abstract ValueGroup ValueGroup { get; }

    /// <summary>
    /// Returns the current value class.
    /// </summary>
    public abstract ValueClass ValueClass { get; }

    /// <summary>
    /// Returns the associated type information.
    /// </summary>
    public TypeValue Type { get; private set; } = type;

    /// <summary>
    /// Returns the associated basic value type.
    /// </summary>
    public BasicValueType BasicValueType { get; private set; } =
        type?.BasicValueType ?? BasicValueType.None;

    /// <summary>
    /// Returns the number of child values.
    /// </summary>
    public int Count => _values.Count;

    /// <summary>
    /// Exposes stored child values as span.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public ReadOnlySpan<Value> Values => _values;

    /// <inheritdoc/>
    public UseCollection Uses => new(this, _uses);

    /// <summary>
    /// Returns the number of uses.
    /// </summary>
    public int NumUses => _uses.Count;

    /// <summary>
    /// Returns the number of uses.
    /// </summary>
    public bool HasUses => NumUses > 0;

    /// <summary>
    /// Returns true if the current value has been sealed.
    /// </summary>
    public bool IsSealed { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Overwrites internally stored types.
    /// </summary>
    /// <param name="typeValue">The type value to use.</param>
    /// <param name="basicValueType">The basic value type to use.</param>
    protected void OverwriteType(
        TypeValue? typeValue = null,
        BasicValueType? basicValueType = null)
    {
        Type = typeValue ?? Type;
        BasicValueType = basicValueType ?? typeValue?.BasicValueType ?? BasicValueType;
    }

    /// <summary>
    /// Returns the n-th value.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="index">The value index.</param>
    /// <returns>The n-th value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValue<T>(int index) where T : Value =>
        _values[index].AsNotNullCast<T>();

    /// <summary>
    /// Returns the assigned type.
    /// </summary>
    /// <typeparam name="T">The value-type target type.</typeparam>
    /// <returns>The type of this value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetTypeAs<T>() where T : TypeValue => Type.As<T>();

    /// <summary>
    /// Returns the index of the given value (if any).
    /// </summary>
    /// <param name="value">The value to look for.</param>
    /// <returns>
    /// The index of the given value or -1 in case the value cannot be found.
    /// </returns>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    public int IndexOf(Value value)
    {
        var nativeSpan = Values;
        for (int i = 0; i < nativeSpan.Length; ++i)
        {
            if (nativeSpan[i] == value)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Returns true if the given value is a primitive value with the specified raw
    /// value.
    /// </summary>
    /// <param name="rawValue">The expected raw value.</param>
    /// <returns>
    /// True if the given value is a primitive value with the specified raw value.
    /// </returns>
    public virtual bool IsPrimitiveValue(long rawValue) => false;

    /// <summary>
    /// Registers the given use with this value.
    /// </summary>
    /// <param name="use">The use to add.</param>
    protected internal void AddUseInternal(Use use) => _uses.Add(use);

    /// <summary>
    /// Computes uses and adds them to the target values for reference.
    /// </summary>
    /// <remarks>This operation is thread safe.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public virtual bool ComputeUses(GlobalValueSet visited)
    {
        if (!visited.Add(this)) return false;
        if (!IsSealed)
        {
            throw new InvalidOperationException(
                $"[ComputeUses] Value has not been sealed: " +
                $"type={GetType().Name}, val={ToReferenceString()}, gen={Generation}");
        }

        // Declare our uses and mark our node as processed
        var values = _values.AsReadOnlySpan();
        for (int i = 0; i < values.Length; ++i)
            values[i].AddUseInternal(new(this, i));

        // Define all uses recursively
        foreach (var value in values)
            value.ComputeUses(visited);

        // Recurse into types
        Type.ComputeUses(visited);

        return true;
    }

    /// <summary>
    /// Formats an error message to include specific exception information.
    /// </summary>
    /// <param name="message">The source error message.</param>
    /// <returns>The formatted error message.</returns>
    public virtual string FormatErrorMessage(string message) =>
        Location.FormatErrorMessage(message);

    /// <summary>
    /// Dumps this method to the given text writer.
    /// </summary>
    /// <param name="textWriter">The text writer.</param>
    public virtual void Dump(TextWriter textWriter) =>
        textWriter.WriteLine(ToString());

    /// <summary>
    /// Accepts the given visitor.
    /// </summary>
    /// <typeparam name="TVisitor">The visitor type.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    public abstract void Accept<TVisitor>(TVisitor visitor)
        where TVisitor : IValueVisitor;

    /// <summary>
    /// Resolves the first use.
    /// </summary>
    /// <returns>The first use.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Use GetFirstUse()
    {
        var enumerator = Uses.GetEnumerator();
        return enumerator.MoveNext()
            ? enumerator.Current
            : throw new InvalidOperationException(ErrorMessages.NoUses);
    }

    /// <summary>
    /// Invokes the callback for every use of the given value type.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="callback">
    /// The callback to be invoked for a use of the given value type.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEachUseOf<TValue>(Action<TValue> callback)
        where TValue : Value =>
        Uses.ForEachUseOf(callback);

    /// <summary>
    /// Adds a new node as a child to this value.
    /// </summary>
    /// <param name="value">The node to add.</param>
    private void AddValue(Value value)
    {
        // Verify generation
        Generation.ValidateCurrentOrPreviousGeneration(value);

        // Register value
        _values.Add(value);
    }

    /// <summary>
    /// Infers the location (if required) of the current node.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="elements">Elements we can infer the location from.</param>
    protected void InferLocation<T>(ReadOnlySpan<T> elements)
        where T : Value
    {
        if (Location.IsKnown)
            return;
        foreach (var element in elements)
            Location = Location.Merge(Location, element.Location);
    }

    /// <summary>
    /// Seals this value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Seal()
    {
        // Verify sealing process
        Debug.Assert(!IsSealed, "Value has already been sealed");
        IsSealed = true;

        // Register the final type node use
        Type.AddUseInternal(new Use(this));

        // Infer location
        InferLocation<Value>(_values);
    }

    /// <summary>
    /// Seals this value.
    /// </summary>
    /// <param name="value1">The first child node.</param>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    protected void Seal(Value value1)
    {
        _values.Reserve(1);

        AddValue(value1);

        Seal();
    }

    /// <summary>
    /// Seals this value.
    /// </summary>
    /// <param name="value1">The first child node.</param>
    /// <param name="value2">The second child node.</param>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    protected void Seal(Value value1, Value value2)
    {
        _values.Reserve(2);

        AddValue(value1);
        AddValue(value2);

        Seal();
    }

    /// <summary>
    /// Seals this value.
    /// </summary>
    /// <param name="value1">The first child node.</param>
    /// <param name="value2">The second child node.</param>
    /// <param name="value3">The third child node.</param>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    protected void Seal(Value value1, Value value2, Value value3)
    {
        _values.Reserve(3);

        AddValue(value1);
        AddValue(value2);
        AddValue(value3);

        Seal();
    }

    /// <summary>
    /// Seals this value.
    /// </summary>
    /// <param name="valueList">The nested child nodes.</param>
    [MethodImpl(
        MethodImplOptions.AggressiveInlining |
        MethodImplOptions.AggressiveOptimization)]
    protected void Seal(ref ValueBuilderList valueList)
    {
        // Move value list for performance reasons
        valueList.MoveTo(ref _values);

        Seal();
    }

    #endregion

    #region Object

    /// <summary>
    /// Returns the argument string (operation arguments) of this node.
    /// </summary>
    /// <returns>The argument string.</returns>
    protected virtual string ToArgString() => ToArgString(offset: 0);

    /// <summary>
    /// Returns the argument string (operation arguments) of this node.
    /// </summary>
    /// <returns>The argument string.</returns>
    protected string ToArgString(int offset) =>
        $"({_values.AsReadOnlySpan()[offset..Count].ToString(
            t => t.ToReferenceString())})";

    /// <summary>
    /// Returns the prefix string (operation name) of this node.
    /// </summary>
    /// <returns>The prefix string.</returns>
    protected abstract string ToPrefixString();

    /// <summary>
    /// Returns the string representation of this node as reference.
    /// </summary>
    /// <returns>The string representation of this node as reference.</returns>
    public string ToReferenceString() => $"{ToPrefixString()}_{Id}";

    /// <summary>
    /// Returns the string representation of this node.
    /// </summary>
    /// <returns>The string representation of this node.</returns>
    public override string ToString()
    {
        var argString = ToArgString();
        return string.IsNullOrEmpty(argString)
            ? ToReferenceString()
            : ToReferenceString() + ": " + argString;
    }

    /// <summary>
    /// Returns the hash code of the value id.
    /// </summary>
    public override int GetHashCode() =>
        HashCode.Combine(
            ValueKind.GetHashCode(),
            BasicValueType,
            Count);

    /// <summary>
    /// Returns true if the given value is a value of the same value kind.
    /// </summary>
    public override bool Equals(object? obj) =>
        obj is Value value &&
        value.ValueKind == ValueKind &&
        value.BasicValueType == BasicValueType;

    #endregion
}

/// <summary>
/// Represents a basic intermediate-representation node.
/// It is the base class for all nodes in the scope of this IR.
/// </summary>
/// <param name="initializer">The current initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class Value<TScope>(in ValueInitializer initializer, TypeValue type) :
    Value(initializer, type),
    IValue<TScope>
    where TScope : class, IValueScope
{
    /// <summary>
    /// Returns the parent scope.
    /// </summary>
    public abstract TScope Scope { get; }

    /// <summary>
    /// Returns the parent scope.
    /// </summary>
    TScope IValue<TScope>.Scope => Scope;

    [Conditional("DEBUG")]
    public void VerifyScope(TScope other) =>
        this.Assert(
            Scope == other,
            $"Value scope mismatch: expected '{other}', got '{Scope}'");
}

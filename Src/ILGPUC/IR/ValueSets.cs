// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueSets.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Analyses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR;

/// <summary>
/// Represents a set of generic values.
/// </summary>
/// <typeparam name="TScope">The scope type.</typeparam>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="scope">The parent scope.</param>
/// <param name="numValues">The number of values.</param>
/// <param name="validator">
/// The generation validator (optional). If not provided, a default validator
/// using scope.Generation will be created.
/// </param>
readonly struct ValueSet<TScope, T>(
    TScope scope,
    int numValues,
    GenerationValidator? validator = null
) : IGenerationObject,
    ITraversalSet<T>
    where TScope : class, IValueScope
    where T : Value<TScope>
{
    /// <summary>
    /// An enumerator to iterate over elements in a set.
    /// </summary>
    internal ref struct Enumerator(ValueSet<TScope, T> parent)
    {
        private HashSet<T>.Enumerator _enumerator = parent._elements.GetEnumerator();

        /// <summary>
        /// Returns the current element.
        /// </summary>
        public readonly T Current => _enumerator.Current;

        /// <summary>
        /// Moves the enumerator to the next element and returns true if there is a new
        /// element available.
        /// </summary>
        public bool MoveNext() => _enumerator.MoveNext();
    }

    private readonly HashSet<T> _elements = new(Math.Max(numValues >> 3, 32));
    private readonly GenerationValidator _validator = validator ?? new(scope.Generation);

    /// <summary>
    /// Constructs a new generic value set.
    /// </summary>
    /// <param name="set">The origin set.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public ValueSet(ValueSet<TScope, T> set) :
        this(set.Scope, set._elements.Capacity)
    {
        foreach (var element in set._elements)
            _elements.Add(element);
    }

    /// <summary>
    /// Returns the parent scope.
    /// </summary>
    public TScope Scope => scope;

    /// <summary>
    /// Returns the underling generation.
    /// </summary>
    public Generation Generation => _validator.Generation;

    /// <summary>
    /// Returns the number of elements in this set.
    /// </summary>
    public int Count => _elements.Count;

    /// <summary>
    /// Returns true if this value set represents the empty set.
    /// </summary>
    public bool IsEmpty => Count < 1;

    /// <summary>
    /// Returns true if this set has at least one element.
    /// </summary>
    public bool HasAny => Count > 0;

    /// <summary>
    /// Verifies the current scope.
    /// </summary>
    [Conditional("DEBUG")]
    private void VerifyValue(T value)
    {
        _validator.ValidateGeneration(value);
        value.VerifyScope(scope);
    }

    /// <inheritdoc cref="HashSet{T}.Add(T)"/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Add(T value)
    {
        VerifyValue(value);
        return _elements.Add(value);
    }

    /// <inheritdoc cref="HashSet{T}.Remove(T)"/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool Remove(T value)
    {
        VerifyValue(value);
        return _elements.Remove(value);
    }

    /// <inheritdoc cref="HashSet{T}.Contains(T)"/>
    public bool Contains(T value)
    {
        VerifyValue(value);
        return _elements.Contains(value);
    }

    /// <summary>
    /// Clones this value set.
    /// </summary>
    /// <returns>The cloned value set.</returns>
    public ValueSet<TScope, T> Clone() => new(this);

    /// <inheritdoc cref="HashSet{T}.Clear"/>
    public void Clear() => _elements.Clear();

    /// <summary>
    /// Returns a new enumerator to iterate over all elements in this set.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);
}

/// <summary>
/// Represents a set list of generic values.
/// </summary>
/// <typeparam name="TScope">The scope type.</typeparam>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="scope">The parent scope.</param>
/// <param name="numValues">The number of values.</param>
sealed class ValueSetList<TScope, T>(TScope scope, int numValues) :
    IGenerationObject,
    ITraversalSet<T>
    where TScope : class, IValueScope
    where T : Value<TScope>
{
    /// <summary>
    /// An enumerator for a set-based list map.
    /// </summary>
    /// <param name="parent">The parent map.</param>
    internal ref struct Enumerator(ValueSetList<TScope, T> parent)
    {
        private ReadOnlySpan<T>.Enumerator _enumerator = parent._elements.GetEnumerator();

        /// <summary>
        /// Returns the current value.
        /// </summary>
        public readonly T Current => _enumerator.Current;

        /// <summary>
        /// Moves the enumerator to the next element of the map and returns true if there
        /// is a next element.
        /// </summary>
        public bool MoveNext() => _enumerator.MoveNext();
    }

    private readonly ValueSet<TScope, T> _set = new(scope, numValues);
    private InlineList<T> _elements = InlineList<T>.Create(Math.Max(numValues >> 3, 32));

    /// <summary>
    /// Returns the parent scope.
    /// </summary>
    public TScope Scope => scope;

    /// <summary>
    /// Returns the underling generation.
    /// </summary>
    public Generation Generation => scope.Generation;

    /// <summary>
    /// Returns the number of elements in this set.
    /// </summary>
    public int Count => _set.Count;

    /// <summary>
    /// Returns true if this value set represents the empty set.
    /// </summary>
    public bool IsEmpty => _set.IsEmpty;

    /// <summary>
    /// Returns true if this set has at least one element.
    /// </summary>
    public bool HasAny => _set.HasAny;

    /// <summary>
    /// Returns the i-th element.
    /// </summary>
    /// <param name="index">The element index.</param>
    /// <returns>The element.</returns>
    public T this[int index] => _elements[index];

    /// <summary>
    /// Verifies the current scope.
    /// </summary>
    [Conditional("DEBUG")]
    private void VerifyValue(T value)
    {
        Generation.ValidateGeneration(value);
        value.VerifyScope(scope);
    }

    /// <inheritdoc cref="HashSet{T}.Add(T)"/>
    public bool Add(T value)
    {
        VerifyValue(value);
        if (!_set.Add(value)) return false;
        _elements.Add(value);
        return true;
    }

    /// <inheritdoc cref="HashSet{T}.Contains(T)"/>
    public bool Contains(T value)
    {
        VerifyValue(value);
        return _set.Contains(value);
    }

    /// <summary>
    /// Returns a new readonly value set list.
    /// </summary>
    /// <returns>The value set list in read-only mode.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan() => _elements.AsReadOnlySpan();

    /// <summary>
    /// Returns a new enumerator to iterate over all elements in this set.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);
}

/// <summary>
/// Represents a set of generic global values.
/// </summary>
/// <param name="generation">The current generation.</param>
/// <param name="numValues">The number of values.</param>
/// <param name="validator">
/// The generation validator (optional). If not provided, a default validator
/// using the generation parameter will be created.
/// </param>
readonly struct GlobalValueSet(
    Generation generation,
    int numValues,
    GenerationValidator? validator = null
) : IGenerationObject
{
    /// <summary>
    /// An enumerator to iterate over elements in a set.
    /// </summary>
    internal struct Enumerator(GlobalValueSet parent)
    {
        private Dictionary<Value, byte>.Enumerator
            _enumerator = parent._set.GetEnumerator();

        /// <summary>
        /// Returns the current element.
        /// </summary>
        public Value Current => _enumerator.Current.Key;

        /// <summary>
        /// Moves the enumerator to the next element and returns true if there is a new
        /// element available.
        /// </summary>
        public bool MoveNext() => _enumerator.MoveNext();
    }

    private readonly Dictionary<Value, byte> _set = new(
        Math.Max(numValues >> 2, 512));
    private readonly GenerationValidator _validator = validator ?? new(generation);

    /// <summary>
    /// Returns the underling generation.
    /// </summary>
    public Generation Generation => _validator.Generation;

    /// <summary>
    /// Returns the number of elements in this set.
    /// </summary>
    public int Count => _set.Count;

    /// <summary>
    /// Returns true if this value set represents the empty set.
    /// </summary>
    public bool IsEmpty => _set.Count < 1;

    /// <summary>
    /// Returns true if this set has at least one element.
    /// </summary>
    public bool HasAny => _set.Count > 0;

    /// <summary>
    /// Verifies the current scope.
    /// </summary>
    [Conditional("DEBUG")]
    private void VerifyValue(Value value) => _validator.ValidateGeneration(value);

    /// <inheritdoc cref="HashSet{T}.Add(T)"/>
    public bool Add(Value value)
    {
        VerifyValue(value);
        return _set.TryAdd(value, 1);
    }

    /// <inheritdoc cref="HashSet{T}.Remove(T)"/>
    public bool Remove(Value value)
    {
        VerifyValue(value);
        return _set.Remove(value, out _);
    }

    /// <inheritdoc cref="HashSet{T}.Contains(T)"/>
    public bool Contains(Value value)
    {
        VerifyValue(value);
        return _set.ContainsKey(value);
    }

    /// <summary>
    /// Returns a new enumerator to iterate over all elements in this set.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);
}
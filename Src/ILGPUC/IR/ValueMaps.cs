// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ValueMaps.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPUC.IR;

/// <summary>
/// Represents a map from values to values.
/// </summary>
/// <typeparam name="TScope">The parent scope.</typeparam>
/// <typeparam name="TKey">The value type.</typeparam>
/// <typeparam name="TValue">The mapped value type.</typeparam>
/// <param name="scope">The parent scope.</param>
/// <param name="numValues">The number of values.</param>
/// <param name="validator">
/// The generation validator (optional). If not provided, a default validator
/// using scope.Generation will be created.
/// </param>
readonly struct ValueMap<TScope, TKey, TValue>(
    TScope scope,
    int numValues,
    GenerationValidator? validator = null
) : IGenerationObject
    where TScope : class, IValueScope
    where TKey : Value<TScope>
{
    /// <summary>
    /// An enumerator to iterate over elements in a map.
    /// </summary>
    internal struct Enumerator(ValueMap<TScope, TKey, TValue> parent)
    {
        private Dictionary<TKey, TValue>.Enumerator _enumerator =
            parent._map.GetEnumerator();

        /// <summary>
        /// Returns the current element.
        /// </summary>
        public readonly KeyValuePair<TKey, TValue> Current => _enumerator.Current;

        /// <summary>
        /// Moves the enumerator to the next element and returns true if there is a new
        /// element available.
        /// </summary>
        public bool MoveNext() => _enumerator.MoveNext();
    }

    private readonly Dictionary<TKey, TValue> _map = new(Math.Max(numValues >> 3, 32));
    private readonly GenerationValidator _validator = validator ?? new(scope.Generation);

    /// <summary>
    /// Returns the parent scope.
    /// </summary>
    public TScope Parent => scope;

    /// <summary>
    /// Returns the underling generation.
    /// </summary>
    public Generation Generation => _validator.Generation;

    /// <summary>
    /// Returns the number of elements in this map.
    /// </summary>
    public int Count => _map.Count;

    /// <summary>
    /// Returns true if this value map represents the empty map.
    /// </summary>
    public bool IsEmpty => _map.Count == 0;

    /// <summary>
    /// Returns true if this map has at least one element.
    /// </summary>
    public bool HasAny => _map.Count > 0;

    /// <summary>
    /// Gets or sets the referenced value.
    /// </summary>
    /// <param name="key">The key.</param>
    public TValue this[TKey key]
    {
        get
        {
            Verify(key);
            return _map[key];
        }
        set
        {
            Verify(key);
            _map[key] = value;
        }
    }

    /// <summary>
    /// Verifies the current scope.
    /// </summary>
    [Conditional("DEBUG")]
    private void Verify(TKey key)
    {
        _validator.ValidateGeneration(key);
        key.VerifyScope(scope);
    }

    /// <inheritdoc cref="Dictionary{TKey, TValue}.Add(TKey, TValue)"/>
    public bool Add(TKey key, TValue value)
    {
        Verify(key);
        return _map.TryAdd(key, value);
    }

    /// <inheritdoc cref="Dictionary{TKey, TValue}.Remove(TKey)"/>
    public bool Remove(TKey key)
    {
        Verify(key);
        return _map.Remove(key);
    }

    /// <inheritdoc cref="Dictionary{TKey, TValue}.ContainsKey(TKey)"/>
    public bool ContainsKey(TKey key)
    {
        Verify(key);
        return _map.ContainsKey(key);
    }

    /// <inheritdoc cref="Dictionary{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        Verify(key);
        return _map.TryGetValue(key, out value);
    }

    /// <summary>
    /// Updates or adds the value referenced by the given key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>True if the key was newly added</returns>
    public bool Update(TKey key, TValue value)
    {
        if (Add(key, value)) return true;
        this[key] = value;
        return false;
    }

    /// <inheritdoc cref="Dictionary{TKey, TValue}.Clear"/>
    public void Clear() => _map.Clear();

    /// <inheritdoc cref="Dictionary{TKey, TValue}.GetEnumerator"/>
    public Enumerator GetEnumerator() => new(this);
}

/// <summary>
/// Represents a map of generic global values.
/// </summary>
/// <param name="generation">The current generation.</param>
/// <param name="numValues">The number of values.</param>
/// <param name="validator">
/// The generation validator (optional). If not provided, a default validator
/// using the generation parameter will be created.
/// </param>
readonly struct GlobalValueMap<TValue>(
    Generation generation,
    int numValues,
    GenerationValidator? validator = null
) : IGenerationObject
{
    /// <summary>
    /// An enumerator to iterate over elements in a map.
    /// </summary>
    internal ref struct Enumerator(GlobalValueMap<TValue> parent)
    {
        private Dictionary<Value, TValue>.Enumerator _enumerator =
            parent._map.GetEnumerator();

        /// <summary>
        /// Returns the current element.
        /// </summary>
        public readonly KeyValuePair<Value, TValue> Current => _enumerator.Current;

        /// <summary>
        /// Moves the enumerator to the next element and returns true if there is a new
        /// element available.
        /// </summary>
        public bool MoveNext() => _enumerator.MoveNext();
    }

    private readonly Dictionary<Value, TValue> _map = new(numValues);
    private readonly GenerationValidator _validator = validator ?? new(generation);

    /// <summary>
    /// Returns the underling generation.
    /// </summary>
    public Generation Generation => _validator.Generation;

    /// <summary>
    /// Returns the number of entries in this map.
    /// </summary>
    public int Count => _map.Count;

    /// <summary>
    /// Returns true if this value map is empty.
    /// </summary>
    public bool IsEmpty => _map.Count < 1;

    /// <summary>
    /// Returns true if this map has at least one element.
    /// </summary>
    public bool HasAny => _map.Count > 0;

    /// <summary>
    /// Gets or sets the value with the given key.
    /// </summary>
    /// <param name="index">The index.</param>
    public TValue this[Value index]
    {
        get => _map[index];
        set => _map[index] = value;
    }

    /// <summary>
    /// Verifies the current scope.
    /// </summary>
    [Conditional("DEBUG")]
    private void VerifyKey(Value value) => _validator.ValidateGeneration(value);

    /// <inheritdoc cref="Dictionary{TKey, TValue}.Add(TKey, TValue)"/>
    public bool Add(Value key, TValue value)
    {
        VerifyKey(key);
        return _map.TryAdd(key, value);
    }

    /// <inheritdoc cref="Dictionary{TKey, TValue}.Remove(TKey)"/>
    public bool Remove(Value key)
    {
        VerifyKey(key);
        return _map.Remove(key, out _);
    }

    /// <inheritdoc cref="Dictionary{TKey, TValue}.ContainsKey(TKey)"/>
    public bool ContainsKey(Value value)
    {
        VerifyKey(value);
        return _map.ContainsKey(value);
    }

    /// <inheritdoc cref="Dictionary{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
    public bool TryGetValue(Value key, [MaybeNullWhen(false)] out TValue? value)
    {
        VerifyKey(key);
        return _map.TryGetValue(key, out value);
    }

    /// <inheritdoc cref="Dictionary{TKey, TValue}.GetEnumerator"/>
    public Enumerator GetEnumerator() => new(this);
}
// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Use.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR;

/// <summary>
/// Represents the use of a single node.
/// </summary>
/// <param name="Target">The target reference.</param>
/// <param name="Index">The argument index.</param>
readonly record struct Use(Value Target, int Index = -1)
{
    /// <summary>
    /// Returns the target value as a specific type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The target converted into the desired target type.</returns>
    public T GetTargetAs<T>() where T : Value => Target.AsNotNullCast<T>();

    /// <summary>
    /// Returns the string representation of this use.
    /// </summary>
    /// <returns>The string representation of this use.</returns>
    public override string ToString() => Index >= 0
        ? $"Target: {Target} [{Index}]"
        : $"Target: {Target} [generic]";

    /// <summary>
    /// Converts the given use implicitly to its underlying target.
    /// </summary>
    /// <param name="use">The use to convert.</param>
    public static implicit operator Value(Use use) => use.Target;
}

/// <summary>
/// Represents an enumerable of uses that point to values.
/// </summary>
/// <param name="value">The parent value.</param>
/// <param name="uses">All uses of the parent node.</param>
/// <param name="predicate">The predicate to pre-filter all uses.</param>
readonly ref struct UseCollection(
    Value value,
    ReadOnlySpan<Use> uses,
    Predicate<Use>? predicate = null)
{
    /// <summary>
    /// An internal use enumerator.
    /// </summary>
    /// <param name="parent">The parent use collection.</param>
    internal ref struct Enumerator(UseCollection parent)
    {
        private ReadOnlySpan<Use>.Enumerator _enumerator = parent._uses.GetEnumerator();
        private readonly Predicate<Use>? _predicate = parent._predicate;

        /// <summary>
        /// Returns the current use.
        /// </summary>
        public Use Current => _enumerator.Current;

        /// <summary>
        /// Moves this enumerator to the next value.
        /// </summary>
        /// <returns>True if the enumerator could be moved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_enumerator.MoveNext())
            {
                if (_predicate?.Invoke(Current) ?? true)
                    return true;
            }
            return false;
        }
    }

    private readonly Predicate<Use>? _predicate = predicate;
    private readonly ReadOnlySpan<Use> _uses = uses;

    /// <summary>
    /// Returns the parent value.
    /// </summary>
    public Value Value { get; } = value;

    /// <summary>
    /// Returns the number of uses.
    /// </summary>
    public int Count => _uses.Length;

    /// <summary>
    /// Returns true, if the collection contains at least one use.
    /// </summary>
    public bool HasAny => GetEnumerator().MoveNext();

    /// <summary>
    /// Returns true, if the collection contains exactly one use.
    /// </summary>
    public bool HasExactlyOne
    {
        get
        {
            var enumerator = GetEnumerator();
            return enumerator.MoveNext() && !enumerator.MoveNext();
        }
    }


    /// <summary>
    /// Tries to resolve a single use.
    /// </summary>
    /// <param name="use">The resolved use reference.</param>
    /// <returns>True, if the collection contains exactly one use.</returns>
    public bool TryGetSingleUse(out Use use)
    {
        use = default;
        var enumerator = GetEnumerator();
        if (!enumerator.MoveNext())
            use = enumerator.Current;
        return !enumerator.MoveNext();
    }

    /// <summary>
    /// Returns true if this use collection has at least one phi use.
    /// </summary>
    public bool HasPhiUses => HasUsesOf<PhiValue>();

    /// <summary>
    /// Returns true if this use collection has at least one use having side effects.
    /// </summary>
    public bool HasBBValueUses => HasUsesOf<BasicBlockValue>();

    /// <summary>
    /// Returns true if this use collection has at least one use having side effects.
    /// </summary>
    public bool HasModuleValueUses => HasUsesOf<ModuleValue>();

    /// <summary>
    /// Returns true if this collection has value uses of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type to match.</typeparam>
    /// <returns>True if a match was found.</returns>
    public bool HasUsesOf<T>() where T : Value
    {
        foreach (Value value in this)
            if (value is T) return true;
        return false;
    }

    /// <summary>
    /// Returns true if any of the uses fulfills the given predicate.
    /// </summary>
    /// <param name="predicate">The predicate to use.</param>
    /// <returns>True, if any use fulfills the given predicate.</returns>
    public bool Any(Predicate<Use> predicate)
    {
        if (_predicate is null) return _uses.Any(predicate);
        var current = _predicate;
        return _uses.Any(use => current(use) && predicate(use));
    }

    /// <summary>
    /// Adds an additional predicate to the use collection by returning a refined
    /// version.
    /// </summary>
    /// <param name="predicate">The predicate to be added.</param>
    /// <returns>The refined use collection.</returns>
    public UseCollection With(Predicate<Use> predicate)
    {
        if (_predicate is null) return new(Value, _uses, predicate);
        var current = _predicate;
        return new(Value, _uses, use => current(use) && predicate(use));
    }

    /// <summary>
    /// Invokes the callback for every use of the given value type.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="callback">
    /// The callback to be invoked for a use of the given value type.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ForEachUseOf<TValue>(Action<TValue> callback)
        where TValue : Value
    {
        foreach (var use in this)
        {
            if (use.Target is TValue value)
                callback(value);
        }
    }

    /// <summary>
    /// Tries to find a value fulfilling the given predicate by searching
    /// direct and transitive uses iteratively (BFS with cycle detection).
    /// </summary>
    /// <typeparam name="T">The value type to look for.</typeparam>
    /// <param name="predicate">The predicate to fullfil.</param>
    /// <param name="found">The value found (if any).</param>
    /// <returns>True if a value was found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryFind<T>(
        Predicate<T> predicate,
        [NotNullWhen(true)] out T? found) where T : Value
    {
        var visited = Value.Module.CreateGlobalSet(capacity: 16);
        var workList = new Queue<Value>(capacity: 16);

        // Seed with direct uses
        foreach (var use in this)
        {
            if (use.Target is T targetValue && predicate(targetValue))
            {
                found = targetValue;
                return true;
            }
            if (visited.Add(use.Target))
                workList.Enqueue(use.Target);
        }

        // Search transitive uses (BFS)
        while (workList.Count > 0)
        {
            var current = workList.Dequeue();
            foreach (var use in current.Uses)
            {
                if (use.Target is T targetValue && predicate(targetValue))
                {
                    found = targetValue;
                    return true;
                }
                if (visited.Add(use.Target))
                    workList.Enqueue(use.Target);
            }
        }

        found = null;
        return false;
    }

    /// <summary>
    /// Returns a new enumerator allowing to enumerate all uses in this list.
    /// </summary>
    /// <returns>A new enumerator.</returns>
    public Enumerator GetEnumerator() => new(this);
}

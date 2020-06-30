// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: AnalysisValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// An abstract analysis value context.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    public interface IAnalysisValueContext<T>
        where T : IEquatable<T>
    {
        /// <summary>
        /// Returns the analysis value associated with the given value.
        /// </summary>
        /// <param name="value">The source value to lookup.</param>
        /// <returns>The parent value.</returns>
        AnalysisValue<T> this[Value value] { get; }
    }

    /// <summary>
    /// An abstract value merger to combine different analysis values.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    public interface IAnalysisValueMerger<T>
        where T : IEquatable<T>
    {
        /// <summary>
        /// Tries to merge the given IR value.
        /// </summary>
        /// <typeparam name="TContext">The current value context.</typeparam>
        /// <param name="value">The IR value.</param>
        /// <param name="context">The current analysis value context.</param>
        /// <returns>A merged value in the case of a successful merge.</returns>
        AnalysisValue<T>? TryMerge<TContext>(Value value, TContext context)
            where TContext : IAnalysisValueContext<T>;

        /// <summary>
        /// Merges the given intermediate values.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The merged value.</returns>
        T Merge(T first, T second);
    }

    /// <summary>
    /// An abstract provider of initial analysis values.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public interface IAnalysisValueProvider<T>
        where T : IEquatable<T>
    {
        /// <summary>
        /// Returns the default analysis value for generic IR nodes.
        /// </summary>
        T DefaultValue { get; }

        /// <summary>
        /// Tries to provide an analysis value for the given type.
        /// </summary>
        /// <param name="typeNode">The type node.</param>
        /// <returns>The provided analysis value (if any).</returns>
        AnalysisValue<T>? TryProvide(TypeNode typeNode);
    }

    /// <summary>
    /// Utility methods for <see cref="AnalysisValue{T}"/> instances.
    /// </summary>
    public static class AnalysisValue
    {
        /// <summary>
        /// An abstract analysis wrapper to encapsulate a parent analysis context.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <typeparam name="TContext">The context type.</typeparam>
        private readonly struct AnalysisValueContextWrapper<T, TContext> :
            IAnalysisValueContext<T>
            where T : IEquatable<T>
            where TContext : class, IFixPointAnalysisContext<AnalysisValue<T>, Value>
        {
            /// <summary>
            /// Constructs a new context wrapper.
            /// </summary>
            /// <param name="context">The parent context.</param>
            public AnalysisValueContextWrapper(TContext context)
            {
                Context = context;
            }

            /// <summary>
            /// Returns the parent analysis context.
            /// </summary>
            public TContext Context { get; }

            /// <summary>
            /// Returns the analysis value associated with the given value.
            /// </summary>
            /// <param name="value">The source value to lookup.</param>
            /// <returns>The parent value.</returns>
            public readonly AnalysisValue<T> this[Value value] => Context[value];
        }

        /// <summary>
        /// Merges the given value in-place in the scope of the dictionary.
        /// </summary>
        /// <typeparam name="TKey">The dictionary key type.</typeparam>
        /// <typeparam name="T">The data type.</typeparam>
        /// <typeparam name="TMerger">The merger type.</typeparam>
        /// <param name="mapping">The current dictionary mapping.</param>
        /// <param name="key">The dictionary key.</param>
        /// <param name="value">The value.</param>
        /// <param name="merger">The merger instance.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Merge<TKey, T, TMerger>(
            this Dictionary<TKey, T> mapping,
            TKey key,
            T value,
            TMerger merger)
            where T : IEquatable<T>
            where TMerger : IAnalysisValueMerger<T>
        {
            if (mapping.TryGetValue(key, out var oldValue))
                value = merger.Merge(oldValue, value);
            mapping[key] = value;
        }

        /// <summary>
        /// Merges a value into the given context.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <typeparam name="TMerger">The merger type.</typeparam>
        /// <typeparam name="TContext">The context type.</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="merger">The merger instance.</param>
        /// <param name="context">The context instance.</param>
        /// <returns>True, if the given value has been updated.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MergeTo<T, TMerger, TContext>(
            Value value,
            TMerger merger,
            TContext context)
            where T : IEquatable<T>
            where TMerger : IAnalysisValueMerger<T>
            where TContext : class, IFixPointAnalysisContext<AnalysisValue<T>, Value>
        {
            var oldValue = context[value];
            var newValue = oldValue.Merge(
                value,
                merger,
                new AnalysisValueContextWrapper<T, TContext>(context));

            context[value] = newValue;
            return oldValue != newValue;
        }

        /// <summary>
        /// Creates a new analysis value for the given type node.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="data">The data value.</param>
        /// <param name="type">The type node.</param>
        /// <returns>The created analysis value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AnalysisValue<T> Create<T>(T data, TypeNode type)
            where T : IEquatable<T>
        {
            if (type is StructureType structureType)
            {
                var childData = new T[structureType.NumFields];
                for (int i = 0, e = childData.Length; i < e; ++i)
                    childData[i] = data;
                return new AnalysisValue<T>(data, childData);
            }
            return new AnalysisValue<T>(data);
        }

        /// <summary>
        /// Creates an initial analysis value.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <typeparam name="TMerger">The merger type.</typeparam>
        /// <typeparam name="TProvider">The provider type.</typeparam>
        /// <param name="type">The type node.</param>
        /// <param name="merger">The merger instance.</param>
        /// <param name="provider">The provider instance.</param>
        /// <returns>The created analysis value.</returns>
        public static AnalysisValue<T> Create<T, TMerger, TProvider>(
            TypeNode type,
            TMerger merger,
            TProvider provider)
            where T : IEquatable<T>
            where TMerger : IAnalysisValueMerger<T>
            where TProvider : IAnalysisValueProvider<T>
        {
            if (type is StructureType structureType)
            {
                var childData = new T[structureType.NumFields];
                for (int i = 0, e = childData.Length; i < e; ++i)
                {
                    childData[i] = Create<T, TMerger, TProvider>(
                        structureType[i],
                        merger,
                        provider).Data;
                }
                T data = childData[0];
                for (int i = 1, e = childData.Length; i < e; ++i)
                    data = merger.Merge(data, childData[i]);
                return new AnalysisValue<T>(data, childData);
            }
            return provider.TryProvide(type) ?? Create(provider.DefaultValue, type);
        }
    }

    /// <summary>
    /// An analysis value to encapsulate static program analysis values.
    /// </summary>
    /// <typeparam name="T">The underlying element type.</typeparam>
    /// <remarks>
    /// This type encapsulates a general <see cref="Data"/> element that represents
    /// accumulated analysis information for the whole object. Furthermore, it stores
    /// additional fine-grained information about each child element in the case of
    /// structure values. This improves the overall program analysis precision.
    /// </remarks>
    public readonly struct AnalysisValue<T> :
        IEquatable<AnalysisValue<T>>
        where T : IEquatable<T>
    {
        #region Instance

        private readonly T[] childData;

        /// <summary>
        /// Constructs a new analysis value with the given data value.
        /// </summary>
        /// <param name="data">The accumulated data value.</param>
        public AnalysisValue(T data)
            : this(data, Array.Empty<T>())
        { }

        /// <summary>
        /// Constructs a new analysis value with different data values for each child.
        /// </summary>
        /// <param name="data">The accumulated data value.</param>
        /// <param name="childArray">All child data values.</param>
        public AnalysisValue(T data, T[] childArray)
        {
            Data = data;
            childData = childArray ?? Array.Empty<T>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying data value.
        /// </summary>
        public T Data { get; }

        /// <summary>
        /// Returns the number of child elements.
        /// </summary>
        public readonly int NumFields => childData.Length;

        /// <summary>
        /// Returns the i-th child data element.
        /// </summary>
        /// <param name="index">The child index.</param>
        /// <returns></returns>
        public readonly T this[int index] => childData[index];

        #endregion

        /// <summary>
        /// Merges the given IR value into the current analysis value.
        /// </summary>
        /// <typeparam name="TMerger">The merger type.</typeparam>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="value">The IR value to merge with.</param>
        /// <param name="merger">The merger instance.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly AnalysisValue<T> Merge<TMerger, TContext>(
            Value value,
            TMerger merger,
            TContext context)
            where TMerger : IAnalysisValueMerger<T>
            where TContext : IAnalysisValueContext<T> =>
            value switch
            {
                GetField getField => GetField(getField, merger, context),
                SetField setField => SetField(setField, merger, context),
                StructureValue structureValue =>
                    StructureValue(structureValue, merger, context),
                PhiValue phiValue => PhiValue(phiValue, merger, context),
                _ => merger.TryMerge(value, context) ??
                    GenericValue(value, merger, context),
            };

        /// <summary>
        /// Merges a <see cref="Values.GetField"/> IR value into this analysis value.
        /// </summary>
        /// <typeparam name="TMerger">The merger type.</typeparam>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="getField">The IR value to merge with.</param>
        /// <param name="merger">The merger instance.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        public readonly AnalysisValue<T> GetField<TMerger, TContext>(
            GetField getField,
            TMerger merger,
            TContext context)
            where TMerger : IAnalysisValueMerger<T>
            where TContext : IAnalysisValueContext<T>
        {
            var source = context[getField.ObjectValue];
            var fieldSpan = getField.FieldSpan;
            if (!fieldSpan.HasSpan)
            {
                return AnalysisValue.Create(
                    merger.Merge(
                        Data,
                        source.childData[fieldSpan.Index]),
                    getField.Type);
            }

            var newChildData = new T[fieldSpan.Span];
            var newData = Data;
            for (int i = 1, e = newChildData.Length; i < e; ++i)
            {
                newChildData[i] = source.childData[fieldSpan.Index + i];
                newData = merger.Merge(newData, newChildData[i]);
            }

            return new AnalysisValue<T>(newData, newChildData);
        }

        /// <summary>
        /// Merges a <see cref="Values.SetField"/> into this analysis value.
        /// </summary>
        /// <typeparam name="TMerger">The merger type.</typeparam>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="setField">The IR value to merge with.</param>
        /// <param name="merger">The merger instance.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        public readonly AnalysisValue<T> SetField<TMerger, TContext>(
            SetField setField,
            TMerger merger,
            TContext context)
            where TMerger : IAnalysisValueMerger<T>
            where TContext : IAnalysisValueContext<T>
        {
            var newChildData = new T[NumFields];
            Array.Copy(childData, newChildData, NumFields);

            var nestedValue = context[setField.Value].Data;
            var fieldSpan = setField.FieldSpan;
            for (int i = 0; i < fieldSpan.Span; ++i)
                newChildData[fieldSpan.Index + i] = nestedValue;

            return new AnalysisValue<T>(
                merger.Merge(Data, nestedValue),
                newChildData);
        }

        /// <summary>
        /// Merges a <see cref="Values.StructureValue"/> into this analysis value.
        /// </summary>
        /// <typeparam name="TMerger">The merger type.</typeparam>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="structureValue">The IR structure value to merge with.</param>
        /// <param name="merger">The merger instance.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        public readonly AnalysisValue<T> StructureValue<TMerger, TContext>(
            StructureValue structureValue,
            TMerger merger,
            TContext context)
            where TMerger : IAnalysisValueMerger<T>
            where TContext : IAnalysisValueContext<T>
        {
            var newChildData = new T[NumFields];
            var newData = Data;
            for (int i = 0, e = NumFields; i < e; ++i)
            {
                var childDataEntry = context[structureValue[i]].Data;
                newData = merger.Merge(newData, childDataEntry);
                newChildData[i] = childDataEntry;
            }
            return new AnalysisValue<T>(newData, newChildData);
        }

        /// <summary>
        /// Merges a <see cref="Values.PhiValue"/> into this analysis value.
        /// </summary>
        /// <typeparam name="TMerger">The merger type.</typeparam>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="phi">The IR phi value to merge with.</param>
        /// <param name="merger">The merger instance.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        public readonly AnalysisValue<T> PhiValue<TMerger, TContext>(
            PhiValue phi,
            TMerger merger,
            TContext context)
            where TMerger : IAnalysisValueMerger<T>
            where TContext : IAnalysisValueContext<T>
        {
            if (NumFields < 1)
                return GenericValue(phi, merger, context);

            var newChildData = new T[NumFields];
            Array.Copy(childData, newChildData, NumFields);
            var newData = Data;
            foreach (Value node in phi.Nodes)
            {
                var childDataEntry = context[node];
                for (int i = 0, e = NumFields; i < e; ++i)
                {
                    newData = merger.Merge(newData, childDataEntry.Data);
                    newChildData[i] = merger.Merge(newChildData[i], childDataEntry[i]);
                }
            }
            return new AnalysisValue<T>(newData, newChildData);
        }

        /// <summary>
        /// Merges a generic IR value into this analysis value.
        /// </summary>
        /// <typeparam name="TMerger">The merger type.</typeparam>
        /// <typeparam name="TContext">The value analysis context.</typeparam>
        /// <param name="value">The IR value to merge with.</param>
        /// <param name="merger">The merger instance.</param>
        /// <param name="context">The current value context.</param>
        /// <returns>The merged analysis value.</returns>
        public readonly AnalysisValue<T> GenericValue<TMerger, TContext>(
            Value value,
            TMerger merger,
            TContext context)
            where TMerger : IAnalysisValueMerger<T>
            where TContext : IAnalysisValueContext<T>
        {
            var newData = Data;
            foreach (Value node in value.Nodes)
                newData = merger.Merge(newData, context[node].Data);
            return AnalysisValue.Create(newData, value.Type);
        }

        #region IEquatable

        /// <summary>
        /// Returns true if the given value is equal to the current one.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>True, if the given value is equal to the current one.</returns>
        public readonly bool Equals(AnalysisValue<T> other)
        {
            if (!Data.Equals(other.Data) || NumFields != other.NumFields)
                return false;
            for (int i = 0, e = NumFields; i < e; ++i)
            {
                if (!childData[i].Equals(other.childData[i]))
                    return false;
            }
            return true;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current value.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to the current value.</returns>
        public readonly override bool Equals(object obj) =>
            obj is AnalysisValue<T> value && Equals(value);

        /// <summary>
        /// Returns the hash code of this value.
        /// </summary>
        /// <returns>The hash code of this value.</returns>
        public override int GetHashCode() => Data.GetHashCode() ^ NumFields;

        /// <summary>
        /// Returns the string representation of this value.
        /// </summary>
        /// <returns>The string representation of this value.</returns>
        public readonly override string ToString() =>
            childData.Length > 0
            ? $"{Data} [{string.Join(", ", childData)}]"
            : Data.ToString();

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the first and second value are the same.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>True, if the first and second value are the same.</returns>
        public static bool operator ==(
            AnalysisValue<T> first,
            AnalysisValue<T> second) =>
            first.Equals(second);

        /// <summary>
        /// Returns true if the first and second value are not the same.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>True, if the first and second value are not the same.</returns>
        public static bool operator !=(
            AnalysisValue<T> first,
            AnalysisValue<T> second) =>
            !(first == second);

        #endregion
    }
}

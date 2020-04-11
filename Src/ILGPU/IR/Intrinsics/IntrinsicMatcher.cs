// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IntrinsicMapping.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPU.IR.Intrinsics
{
    /// <summary>
    /// Matches whether intrinsic implementations are compatible
    /// to internal requirements.
    /// </summary>
    public abstract partial class IntrinsicMatcher
    {
        /// <summary>
        /// Constructs a new intrinsic matcher.
        /// </summary>
        internal IntrinsicMatcher() { }
    }

    /// <summary>
    /// Defines an abstract intrinsic implementation.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Design",
        "CA1040:AvoidEmptyInterfaces",
        Justification = "It is used as a type constraint")]
    public interface IIntrinsicImplementation
    {
        // Left blank for future extension possibilities
    }

    /// <summary>
    /// Transforms a single intrinsic implementation into another one.
    /// </summary>
    /// <typeparam name="TFirst">The first implementation type.</typeparam>
    /// <typeparam name="TSecond">The second implementation type.</typeparam>
    public interface IIntrinsicImplementationTransformer<TFirst, TSecond>
        where TFirst : class, IIntrinsicImplementation
        where TSecond : class, IIntrinsicImplementation
    {
        /// <summary>
        /// Transforms the given implementation into another one.
        /// </summary>
        /// <param name="implementation">The implementation to transform.</param>
        /// <returns>The transformed implementation.</returns>
        TSecond Transform(TFirst implementation);
    }

    /// <summary>
    /// Matches whether intrinsic implementations are compatible
    /// to internal requirements.
    /// </summary>
    /// <typeparam name="T">The matcher value type.</typeparam>
    public abstract class IntrinsicMatcher<T> : IntrinsicMatcher
        where T : class, IIntrinsicImplementation
    {
        #region Instance

        /// <summary>
        /// Constructs a new intrinsic matcher.
        /// </summary>
        internal IntrinsicMatcher() { }

        #endregion

        #region Methods

        /// <summary>
        /// Transforms the currently stored intrinsic implementations.
        /// </summary>
        /// <typeparam name="TOther">The other matcher value type.</typeparam>
        /// <typeparam name="TTransformer">The implementation transformer.</typeparam>
        /// <param name="transformer">The transformer to use.</param>
        /// <param name="other">The other matcher.</param>
        public abstract void TransformTo<TOther, TTransformer>(
            TTransformer transformer,
            IntrinsicMatcher<TOther> other)
            where TOther : class, IIntrinsicImplementation
            where TTransformer : struct, IIntrinsicImplementationTransformer<T, TOther>;

        #endregion
    }

    /// <summary>
    /// Matches whether intrinsic implementations are compatible
    /// to internal requirements.
    /// </summary>
    /// <typeparam name="T">The matcher value type.</typeparam>
    /// <typeparam name="TMatchedValue">The value type to be matched.</typeparam>
    public abstract class IntrinsicMatcher<T, TMatchedValue> : IntrinsicMatcher<T>
        where T : class, IIntrinsicImplementation
        where TMatchedValue : class
    {
        #region Instance

        /// <summary>
        /// Constructs a new intrinsic matcher.
        /// </summary>
        internal IntrinsicMatcher() { }

        #endregion

        #region Methods

        /// <summary>
        /// Tries to resolve an intrinsic implementation.
        /// </summary>
        /// <param name="value">The value instance.</param>
        /// <param name="implementation">The resolved implementation (if any).</param>
        /// <returns>True, if an implementation could be resolved.</returns>
        public abstract bool TryGetImplementation(
            TMatchedValue value,
            out T implementation);

        #endregion
    }

    /// <summary>
    /// Represents an intrinsic matcher that matches managed methods.
    /// </summary>
    /// <typeparam name="T">The matcher value type.</typeparam>
    public sealed class IntrinsicMethodMatcher<T> : IntrinsicMatcher<T, MethodInfo>
        where T : class, IIntrinsicImplementation
    {
        #region Instance

        private readonly Dictionary<MethodInfo, T> entries =
            new Dictionary<MethodInfo, T>();

        /// <summary>
        /// Constructs a new intrinsic matcher.
        /// </summary>
        internal IntrinsicMethodMatcher() { }

        #endregion

        #region Methods

        /// <summary>
        /// Registers the given implementation with the current matcher.
        /// </summary>
        /// <param name="value">The method information.</param>
        /// <param name="implementation">
        /// The intrinsic implementation to register.
        /// </param>
        public void Register(MethodInfo value, T implementation)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.IsGenericMethod)
                value = value.GetGenericMethodDefinition();
            entries[value] = implementation ??
                throw new ArgumentNullException(nameof(implementation));
        }

        /// <summary cref="IntrinsicMatcher{T, TMatchedValue}.TryGetImplementation(
        /// TMatchedValue, out T)"/>
        public override bool TryGetImplementation(MethodInfo value, out T implementation)
        {
            if (value.IsGenericMethod)
                value = value.GetGenericMethodDefinition();
            return entries.TryGetValue(value, out implementation);
        }

        /// <summary cref="IntrinsicMatcher{T}.TransformTo{TOther, TTransformer}(
        /// TTransformer, IntrinsicMatcher{TOther})"/>
        public override void TransformTo<TOther, TTransformer>(
            TTransformer transformer,
            IntrinsicMatcher<TOther> other)
        {
            var otherMatcher = other as IntrinsicMethodMatcher<TOther>;
            foreach (var entry in entries)
                otherMatcher.entries[entry.Key] = transformer.Transform(entry.Value);
        }

        #endregion
    }

    /// <summary>
    /// Represents an intrinsic matcher that matches values.
    /// </summary>
    /// <typeparam name="T">The matcher value type.</typeparam>
    public abstract partial class BaseIntrinsicValueMatcher<T> :
        IntrinsicMatcher<T, Value>
        where T : class, IIntrinsicImplementation
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract intrinsic value matcher.
        /// </summary>
        /// <param name="valueKind">The value kind.</param>
        protected BaseIntrinsicValueMatcher(ValueKind valueKind)
        {
            ValueKind = valueKind;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target value kind.
        /// </summary>
        public ValueKind ValueKind { get; }

        #endregion
    }

    /// <summary>
    /// Represents an intrinsic matcher that matches values.
    /// </summary>
    /// <typeparam name="T">The matcher value type.</typeparam>
    public abstract class IntrinsicValueMatcher<T> : BaseIntrinsicValueMatcher<T>
        where T : class, IIntrinsicImplementation
    {
        #region Instance

        /// <summary>
        /// Constructs a new abstract intrinsic value matcher.
        /// </summary>
        /// <param name="valueKind">The value kind.</param>
        protected IntrinsicValueMatcher(ValueKind valueKind)
            : base(valueKind)
        { }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated implementation.
        /// </summary>
        public T Implementation { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Registers the given implementation.
        /// </summary>
        /// <param name="implementation">The implementation to register.</param>
        public void Register(T implementation)
        {
            if (Implementation != null)
                throw new InvalidOperationException();
            Implementation = implementation
                ?? throw new ArgumentNullException(nameof(implementation));
        }

        /// <summary cref="IntrinsicMatcher{T}.TransformTo{TOther, TTransformer}(
        /// TTransformer, IntrinsicMatcher{TOther})"/>
        public sealed override void TransformTo<TOther, TTransformer>(
            TTransformer transformer,
            IntrinsicMatcher<TOther> other)
        {
            var otherMatcher = other as IntrinsicValueMatcher<TOther>;
            otherMatcher.Implementation = transformer.Transform(Implementation);
        }

        #endregion
    }

    /// <summary>
    /// Represents an intrinsic matcher that matches values.
    /// </summary>
    /// <typeparam name="T">The matcher value type.</typeparam>
    /// <typeparam name="TValueKind">The type of the value kind.</typeparam>
    public abstract class IntrinsicValueMatcher<T, TValueKind> :
        BaseIntrinsicValueMatcher<T>
        where T : class, IIntrinsicImplementation
        where TValueKind : struct
    {
        #region Instance

        /// <summary>
        /// All value implementation entries.
        /// </summary>
        private readonly T[] entries;

        /// <summary>
        /// Constructs a new abstract intrinsic value matcher.
        /// </summary>
        /// <param name="valueKind">The value kind.</param>
        protected IntrinsicValueMatcher(ValueKind valueKind)
            : base(valueKind)
        {
            var values = Enum.GetValues(typeof(TValueKind));
            entries = new T[values.Length];
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns a reference to the i-th element.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The resolved reference.</returns>
        protected T this[int index]
        {
            get => entries[index];
            set => entries[index] = value;
        }

        #endregion

        #region Methods

        /// <summary cref="IntrinsicMatcher{T}.TransformTo{TOther, TTransformer}(
        /// TTransformer, IntrinsicMatcher{TOther})"/>
        public sealed override void TransformTo<TOther, TTransformer>(
            TTransformer transformer,
            IntrinsicMatcher<TOther> other)
        {
            var otherMatcher = other as IntrinsicValueMatcher<TOther, TValueKind>;
            for (int i = 0, e = entries.Length; i < e; ++i)
                otherMatcher.entries[i] = transformer.Transform(entries[i]);
        }

        #endregion
    }

    /// <summary>
    /// Represents an intrinsic matcher that matches values.
    /// </summary>
    /// <typeparam name="T">The matcher value type.</typeparam>
    /// <typeparam name="TValueKind">The type of the value kind.</typeparam>
    public abstract class TypedIntrinsicValueMatcher<T, TValueKind> :
        BaseIntrinsicValueMatcher<T>
        where T : class, IIntrinsicImplementation
        where TValueKind : struct
    {
        #region Instance

        /// <summary>
        /// All value implementation entries.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        private readonly T[,] entries;

        /// <summary>
        /// Constructs a new abstract intrinsic value matcher.
        /// </summary>
        /// <param name="valueKind">The value kind.</param>
        [SuppressMessage(
            "Microsoft.Performance",
            "CA1814: PreferJaggedArraysOverMultidimensional")]
        protected TypedIntrinsicValueMatcher(ValueKind valueKind)
            : base(valueKind)
        {
            var values = Enum.GetValues(typeof(TValueKind));
            var basicValues = Enum.GetValues(typeof(BasicValueType));
            entries = new T[values.Length, basicValues.Length];
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns a reference to the i-th element.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <param name="basicValueType">The basic-value type.</param>
        /// <returns>The resolved reference.</returns>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1023:IndexersShouldNotBeMultidimensional")]
        protected T this[int index, BasicValueType basicValueType]
        {
            get => entries[index, (int)basicValueType];
            set => entries[index, (int)basicValueType] = value;
        }

        #endregion

        #region Methods

        /// <summary cref="IntrinsicMatcher{T}.TransformTo{TOther, TTransformer}(
        /// TTransformer, IntrinsicMatcher{TOther})"/>
        public sealed override void TransformTo<TOther, TTransformer>(
            TTransformer transformer,
            IntrinsicMatcher<TOther> other)
        {
            var otherMatcher = other as TypedIntrinsicValueMatcher<TOther, TValueKind>;
            for (int i = 0, e = entries.GetLength(0); i < e; ++i)
            {
                for (int j = 0, e2 = entries.GetLength(1); j < e2; ++j)
                    otherMatcher.entries[i, j] = transformer.Transform(entries[i, j]);
            }
        }

        #endregion
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ValueBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;

namespace ILGPU.IR
{
    /// <summary>
    /// An abstract value builder.
    /// </summary>
    public interface IValueBuilder
    {
        /// <summary>
        /// Returns the parent IR builder.
        /// </summary>
        IRBuilder IRBuilder { get; }

        /// <summary>
        /// Returns the current location.
        /// </summary>
        Location Location { get; }

        /// <summary>
        /// The number of field values.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Returns the value that corresponds to the given field access.
        /// </summary>
        /// <param name="access">The field access.</param>
        /// <returns>The resolved field type.</returns>
        ValueReference this[FieldAccess access] { get; }

        /// <summary>
        /// Adds the given value to the instance builder.
        /// </summary>
        /// <param name="value">The value to add.</param>
        void Add(Value value);

        /// <summary>
        /// Constructs a new value that represents the current value builder.
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        ValueReference Seal();
    }

    /// <summary>
    /// Extensions for <see cref="IValueBuilder"/> instances.
    /// </summary>
    public static class ValueBuilder
    {
        /// <summary>
        /// Constructs a new <see cref="ValueBuilder{TBuilder}"/> wrapper.
        /// </summary>
        /// <typeparam name="TBuilder">The builder type.</typeparam>
        /// <param name="valueBuilder">The value builder instance.</param>
        /// <returns>The created wrapper class.</returns>
        public static ValueBuilder<TBuilder> ToValueBuilder<TBuilder>(
            this TBuilder valueBuilder)
            where TBuilder : IValueBuilder =>
            new ValueBuilder<TBuilder>(valueBuilder);
    }

    /// <summary>
    /// A wrapped <see cref="IValueBuilder"/> that wraps value-based builder structures.
    /// </summary>
    /// <typeparam name="TBuilder">The structure-based builder type.</typeparam>
    public class ValueBuilder<TBuilder> : IValueBuilder
        where TBuilder : IValueBuilder
    {
        /// <summary>
        /// The nested builder.
        /// </summary>
        private TBuilder nestedBuilder;

        /// <summary>
        /// Constructs a new value builder.
        /// </summary>
        /// <param name="builder">The underlying builder structure.</param>
        public ValueBuilder(TBuilder builder)
        {
            nestedBuilder = builder;
        }

        /// <summary>
        /// Returns the parent IR builder.
        /// </summary>
        public IRBuilder IRBuilder => nestedBuilder.IRBuilder;

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public Location Location => nestedBuilder.Location;

        /// <summary>
        /// The number of field values.
        /// </summary>
        public int Count => nestedBuilder.Count;

        /// <summary>
        /// Returns the value that corresponds to the given field access.
        /// </summary>
        /// <param name="access">The field access.</param>
        /// <returns>The resolved field type.</returns>
        public ValueReference this[FieldAccess access] => nestedBuilder[access];

        /// <summary>
        /// Returns a reference to the nested builder.
        /// </summary>
        public ref TBuilder Builder => ref nestedBuilder;

        /// <summary>
        /// Adds the given value to the instance builder.
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void Add(Value value) => nestedBuilder.Add(value);

        /// <summary>
        /// Constructs a new value that represents the current value builder.
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        public ValueReference Seal() => nestedBuilder.Seal();

        /// <summary>
        /// Constructs a new value that represents the current value builder and returns
        /// 
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        public T SealAs<T>()
            where T : Value => Seal().ResolveAs<T>();
    }
}

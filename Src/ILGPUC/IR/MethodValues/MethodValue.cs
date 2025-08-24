// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.ModuleValues.Construction;

namespace ILGPUC.IR.MethodValues;

/// <summary>
/// The base interface of all values.
/// </summary>
interface IMethodValue : IValue<Method>;

/// <summary>
/// A general value initializer.
/// </summary>
/// <param name="Builder">The parent builder.</param>
/// <param name="Location">The associated location.</param>
readonly record struct MethodValueInitializer(
    MethodBuilder Builder,
    Location Location) : ILocation
{
    /// <summary>
    /// Returns the parent module.
    /// </summary>
    public Module Module => ModuleBuilder.Module;

    /// <summary>
    /// Returns the parent module builder.
    /// </summary>
    public ModuleBuilder ModuleBuilder => Builder.ModuleBuilder;

    /// <summary>
    /// Returns the parent method.
    /// </summary>
    public Method Method => Builder.Method;

    /// <summary>
    /// Returns associated compilation properties.
    /// </summary>
    public CompilationProperties Properties => Builder.ModuleBuilder.Properties;

    /// <summary>
    /// Returns the current generation.
    /// </summary>
    public Generation Generation => Builder.Generation;

    /// <summary>
    /// Formats an error message to include specific location information.
    /// </summary>
    string ILocation.FormatErrorMessage(string message) =>
        Location.FormatErrorMessage(message);

    /// <summary>
    /// Implicitly converts the given initializer into a value initializer.
    /// </summary>
    /// <param name="initializer">The initializer to convert.</param>
    public static implicit operator ValueInitializer(
        MethodValueInitializer initializer) =>
        new(initializer.Module, initializer.Location);
}

/// <summary>
/// Represents a basic intermediate-representation value on the method level.
/// It is the base class for all method values in the scope of this IR.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class MethodValue(in MethodValueInitializer initializer, TypeValue type) :
    Value<Method>(initializer, type),
    IMethodValue,
    IValueClassInformation
{
    /// <summary>
    /// A child value node enumerator.
    /// </summary>
    internal struct Enumerator(MethodValue parent)
    {
        private int _index = -1;

        /// <summary>
        /// Returns the current child value.
        /// </summary>
        public readonly Value Current => parent.GetValue<Value>(_index);

        /// <summary>
        /// Moves this enumerator.
        /// </summary>
        public bool MoveNext() => ++_index < parent.Count;
    }

    /// <summary>
    /// Returns the method value class.
    /// </summary>
    static ValueClass IValueClassInformation.ValueClass => ValueClass.Method;

    /// <summary>
    /// Returns the parent method.
    /// </summary>
    public Method Method { get; } = initializer.Method;

    /// <summary>
    /// Returns the parent scope.
    /// </summary>
    public sealed override Method Scope => Method;

    /// <summary>
    /// Returns the value class <see cref="ValueClass.Method"/>.
    /// </summary>
    public override ValueClass ValueClass => ValueClass.Method;

    /// <summary>
    /// Returns the hash code of the value id.
    /// </summary>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Returns true if the given value is a value of the same value kind.
    /// </summary>
    public sealed override bool Equals(object? obj) =>
        obj is MethodValue methodValue && methodValue.Id == Id;

    /// <summary>
    /// Returns an enumerator to enumerate all child values.
    /// </summary>
    /// <returns>An enumerator to enumerate all child values.</returns>
    public Enumerator GetEnumerator() => new(this);
}

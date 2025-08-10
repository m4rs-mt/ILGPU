// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ModuleValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues.Construction;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// The base interface of all values.
/// </summary>
interface IModuleValue : IValue<Module>;

/// <summary>
/// A general value initializer.
/// </summary>
/// <param name="Builder">The parent builder.</param>
/// <param name="Location">The associated location.</param>
readonly record struct ModuleValueInitializer(
    ModuleBuilder Builder,
    Location Location) : ILocation
{
    /// <summary>
    /// Returns the parent module.
    /// </summary>
    public Module Module => Builder.Module;

    /// <summary>
    /// Returns associated compilation properties.
    /// </summary>
    public CompilationProperties Properties => Builder.Properties;

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
        ModuleValueInitializer initializer) =>
        new(initializer.Module, initializer.Location);
}

/// <summary>
/// Represents a basic intermediate-representation value on the module level.
/// It is the base class for all module values in the scope of this IR.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class ModuleValue(in ModuleValueInitializer initializer, TypeValue type) :
    Value<Module>(initializer, type),
    IModuleValue,
    IValueClassInformation
{
    /// <summary>
    /// A child value node enumerator.
    /// </summary>
    internal struct Enumerator(ModuleValue parent)
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
    /// Returns the module class.
    /// </summary>
    static ValueClass IValueClassInformation.ValueClass => ValueClass.Module;

    /// <summary>
    /// Returns the value class <see cref="ValueClass.Module"/>.
    /// </summary>
    public override ValueClass ValueClass => ValueClass.Module;

    /// <summary>
    /// Returns the parent module.
    /// </summary>
    public sealed override Module Scope => Module;

    /// <summary>
    /// Returns the parent module.
    /// </summary>
    Module IValue<Module>.Scope => Module;

    /// <summary>
    /// Returns an enumerator to enumerate all child values.
    /// </summary>
    /// <returns>An enumerator to enumerate all child values.</returns>
    public Enumerator GetEnumerator() => new(this);
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Predicate.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.ModuleValues;

namespace ILGPUC.IR.PureValues.Construction;

partial class PureValueBuilder
{
    /// <summary>
    /// Creates a conditional if predicate.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="condition">The condition.</param>
    /// <param name="trueValue">The true value.</param>
    /// <param name="falseValue">The false value.</param>
    /// <returns>A node that represents the predicate operation.</returns>
    public Value? CreatePredicate(
        Location location,
        Value? condition,
        Value? trueValue,
        Value? falseValue)
    {
        if (condition is null || trueValue is null) return falseValue;
        if (trueValue == falseValue || falseValue is null) return trueValue;

        location.Assert(condition.Type.BasicValueType == BasicValueType.Int1);

        if (!trueValue.Type.Equals(falseValue.Type))
        {
            if (trueValue.Type is PointerType truePointerType &&
                falseValue.Type is PointerType falsePointerType &&
                truePointerType.ElementType.Equals(falsePointerType.ElementType) &&
                (trueValue is NullValue || falseValue is NullValue))
            {
                // A null pointer is null in every address space, such as the
                // null arm of the pointer of a fixed statement on an array
                if (falseValue is NullValue)
                    falseValue = CreateNull(location, truePointerType);
                else
                    trueValue = CreateNull(location, falsePointerType);
            }
            else if (trueValue.Type is PointerType truePointer &&
                falseValue.Type is PointerType)
            {
                // Pointers into different address spaces (e.g. a local closure
                // and a null delegate handle) are selected as generic pointers
                trueValue = CreateAddressSpaceCast(
                    location,
                    trueValue,
                    MemoryAddressSpace.Generic).AsNotNull();
                falseValue = CreatePointerCast(
                    location,
                    CreateAddressSpaceCast(
                        location,
                        falseValue,
                        MemoryAddressSpace.Generic),
                    truePointer.ElementType).AsNotNull();
            }
            else if (trueValue.Type is StructureType trueStructure &&
                falseValue.Type is StructureType falseStructure)
            {
                // Structures holding pointers into different address spaces
                // are selected as structures holding generic pointers
                var type = GetGenericStructureType(trueStructure, falseStructure);
                trueValue = CreateStructureCast(location, trueValue, type);
                falseValue = CreateStructureCast(location, falseValue, type);
            }
            else
            {
                falseValue = CreateConvert(
                    location,
                    falseValue,
                    trueValue.GetTypeAs<PrimitiveType>()).AsNotNull();
            }
        }

        // Match simple constant predicates
        if (condition is PrimitiveValue constant)
            return constant.Int1Value ? trueValue : falseValue;

        // Match bool predicates that can be represented via simple expressions
        if (trueValue.BasicValueType == BasicValueType.Int1)
        {
            // Check for two cases: condition ? True : falseValue
            //                       --> condition | falseValue
            //                      condition ? False : falseValue
            //                       --> !condition & falseValue
            if (trueValue is PrimitiveValue truePrimitive)
            {
                var kind = BinaryArithmeticKind.Or;
                // Check for: condition ? False ...
                if (!truePrimitive.Int1Value)
                {
                    kind = BinaryArithmeticKind.And;
                    condition = CreateArithmetic(
                        location,
                        condition,
                        UnaryArithmeticKind.Not);
                }
                return CreateArithmetic(
                    location,
                    condition,
                    falseValue,
                    kind,
                    ArithmeticFlags.Unsigned);
            }
            // Move constants to the left
            else if (falseValue is PrimitiveValue)
            {
                return CreatePredicate(
                    location,
                    CreateArithmetic(
                        location,
                        condition,
                        UnaryArithmeticKind.Not),
                    falseValue,
                    trueValue);
            }

            // If we arrive here we cannot merge any constants
        }

        // Match negated predicates
        return condition is UnaryArithmeticValue unary &&
            unary.Kind == UnaryArithmeticKind.Not
            ? CreatePredicate(
                location,
                unary.Value,
                falseValue,
                trueValue)
            : Append(new Predicate(
                GetInitializer(location),
                condition,
                trueValue,
                falseValue));
    }

    /// <summary>
    /// Returns the type of the given structure types that differ in the address
    /// spaces of their pointers and views only, whose differing fields are
    /// generic.
    /// </summary>
    private StructureType GetGenericStructureType(
        StructureType first,
        StructureType second)
    {
        var builder = ModuleBuilder.CreateStructureType(first.NumFields);
        for (int i = 0; i < first.NumFields; ++i)
        {
            builder.Add(
                first[i].Equals(second[i]) || first[i] is not AddressSpaceType field
                    ? first[i]
                    : ModuleBuilder.SpecializeAddressSpaceType(
                        field,
                        MemoryAddressSpace.Generic));
        }
        return builder.Seal().As<StructureType>();
    }

    /// <summary>
    /// Casts the pointers and views of the given structure value into the
    /// address spaces of the given structure type.
    /// </summary>
    private Value CreateStructureCast(
        Location location,
        Value value,
        StructureType type)
    {
        if (value.Type.Equals(type))
            return value;

        var instance = CreateStructure(location, type);
        for (int i = 0; i < type.NumFields; ++i)
        {
            var field = CreateGetField(location, value, new FieldSpan(i)).AsNotNull();
            if (type[i] is AddressSpaceType fieldType && !field.Type.Equals(fieldType))
            {
                field = CreateAddressSpaceCast(
                    location,
                    field,
                    fieldType.AddressSpace).AsNotNull();
            }
            instance.Add(field);
        }
        return instance.Seal();
    }
}

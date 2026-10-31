// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Arrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Util;
using System;
using System.Collections.Generic;

namespace ILGPUC.Frontend;

partial class CodeGenerator
{
    /// <summary>
    /// Realizes an array creation.
    /// </summary>
    /// <param name="managedElementType">The element type.</param>
    private void MakeNewArray(Type managedElementType)
    {
        VerifyNoDelegateArray(managedElementType);
        // Create the array type to create
        var elementType = ModuleBuilder.CreateType(
            managedElementType,
            MemoryAddressSpace.Generic);
        var arrayType = ModuleBuilder.CreateArrayType(elementType, 1);

        // Build the actual array instance
        var arrayBuilder = Builder.CreateNewArray(Location, arrayType);
        arrayBuilder.Add(
            Builder.CreateConvertToInt32(
                Location,
                Block.PopInt(Location, ConvertFlags.None)));
        var array = arrayBuilder.Seal();
        ReinitializeArrayInLoop(array);

        // Push array instance
        Block.Push(array);
    }

    /// <summary>
    /// The IL offset ranges of all backward branches (lazily computed).
    /// </summary>
    private List<(int Start, int End)>? _backwardBranchRanges;

    /// <summary>
    /// Returns true if the current instruction can execute more than once, since
    /// it lies between the target and the source of a backward branch.
    /// </summary>
    private bool IsCurrentInstructionInLoop()
    {
        if (_backwardBranchRanges is null)
        {
            _backwardBranchRanges = [];
            for (int i = 0, e = DisassembledMethod.Count; i < e; ++i)
            {
                var instruction = DisassembledMethod[i];
                if (instruction.Argument is not ILInstructionBranchTargets targets)
                    continue;
                foreach (var target in targets.GetTargetOffsets())
                {
                    if (target <= instruction.Offset)
                        _backwardBranchRanges.Add((target, instruction.Offset));
                }
            }
        }

        int offset = CurrentInstructionOffset;
        foreach (var (start, end) in _backwardBranchRanges)
        {
            if (start <= offset && offset <= end)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The maximum number of elements of an array allocated in a loop that are
    /// cleared explicitly at the allocation point.
    /// </summary>
    private const int MaxNumArrayElementsToReinitialize = 1024;

    /// <summary>
    /// Clears all elements of an array allocated inside a loop or inside a
    /// method other than the kernel entry point, which can be inlined into a
    /// loop of its caller. Arrays are backed by a single per-thread buffer that
    /// is zero-initialized once, but every execution of the allocation has to
    /// yield a fresh zeroed array.
    /// </summary>
    /// <param name="array">The allocated array.</param>
    internal void ReinitializeArrayInLoop(Value array)
    {
        if (array is not ArrayValue arrayValue ||
            IsEntryPoint && !IsCurrentInstructionInLoop())
        {
            return;
        }

        // Determine the constant lengths of all dimensions
        int numDimensions = arrayValue.NumDimensions;
        var lengths = new int[numDimensions];
        long numElements = 1;
        for (int i = 0; i < numDimensions; ++i)
        {
            if (arrayValue.Dimensions[i] is not PrimitiveValue length)
                return;
            lengths[i] = length.Int32Value;
            numElements *= lengths[i];
        }
        if (numElements < 1 || numElements > MaxNumArrayElementsToReinitialize)
            return;

        // Store the default value to every element
        var zero = Builder.CreateNull(Location, arrayValue.ElementType);
        var indices = new int[numDimensions];
        for (long element = 0; element < numElements; ++element)
        {
            var laeaBuilder = Builder.CreateLoadArrayElementAddress(
                Location,
                arrayValue);
            for (int i = 0; i < numDimensions; ++i)
                laeaBuilder.Add(Builder.CreatePrimitiveValue(Location, indices[i]));
            CreateStore(laeaBuilder.Seal(), zero);

            // Advance the row-major index tuple
            for (int i = numDimensions - 1; i >= 0; --i)
            {
                if (++indices[i] < lengths[i])
                    break;
                indices[i] = 0;
            }
        }
    }

    /// <summary>
    /// Creates an array element address load.
    /// </summary>
    /// <param name="elementType">The element type to load.</param>
    /// <param name="type">The IR element type to load.</param>
    /// <returns>The loaded array element address.</returns>
    private Value CreateLoadArrayElementAddress(Type elementType, out TypeValue type)
    {
        var index = Block.PopInt(Location, ConvertFlags.None);
        var array = Block.Pop();

        type = ModuleBuilder.CreateType(elementType);

        var laeaBuilder = Builder.CreateLoadArrayElementAddress(Location, array);
        laeaBuilder.Add(index);
        return laeaBuilder.Seal();
    }

    /// <summary>
    /// Realizes an array load-element operation.
    /// </summary>
    /// <param name="elementType">The element type to load.</param>
    private void MakeLoadElementAddress(Type elementType) =>
        Block.Push(
            CreateLoadArrayElementAddress(elementType, out var _));

    /// <summary>
    /// Realizes an array load-element operation.
    /// </summary>
    /// <param name="elementType">The element type to load.</param>
    private void MakeLoadElement(Type elementType) =>
        Block.Push(CreateLoad(
            CreateLoadArrayElementAddress(elementType, out var type),
            type,
            elementType.ToTargetUnsignedFlags()));

    /// <summary>
    /// Realizes an array store-element operation.
    /// </summary>
    /// <param name="elementType">The element type to store.</param>
    private void MakeStoreElement(Type elementType)
    {
        var value = Block.Pop();

        CreateStore(
            CreateLoadArrayElementAddress(elementType, out var type),
            CreateConversion(
                value,
                type,
                elementType.ToTargetUnsignedFlags()));
    }

    /// <summary>
    /// Compares two array references, such as an array with null. Every array
    /// has a buffer of its own, also an empty array, while the null array has
    /// none: arrays are compared by the addresses of their first elements.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="kind">The comparison kind.</param>
    /// <param name="flags">The comparison flags.</param>
    private Value CreateArrayCompare(
        Value left,
        Value right,
        CompareKind kind,
        CompareFlags flags)
    {
        // The null reference of ldnull is the null array of the other operand
        if (left.Type is not ArrayType)
            left = CreateConversion(left, right.Type, ConvertFlags.None);
        if (right.Type is not ArrayType)
            right = CreateConversion(right, left.Type, ConvertFlags.None);

        // A reference is greater than null if it is not null (cgt.un)
        bool unsigned = (flags & CompareFlags.UnsignedOrUnordered) ==
            CompareFlags.UnsignedOrUnordered;
        if (unsigned &&
            (kind == CompareKind.GreaterThan && right is NullValue ||
            kind == CompareKind.LessThan && left is NullValue))
        {
            kind = CompareKind.NotEqual;
        }

        var leftAddress = CreateArrayAddress(left);
        var rightAddress = CreateArrayAddress(right);
        if (!leftAddress.Type.Equals(rightAddress.Type))
        {
            rightAddress = Builder.CreatePointerCast(
                Location,
                rightAddress,
                left.GetTypeAs<ArrayType>().ElementType);
        }
        return Builder.CreateCompare(
            Location,
            leftAddress,
            rightAddress,
            kind,
            flags).ThrowIfNull();
    }

    /// <summary>
    /// Creates the address of the first element of the given array.
    /// </summary>
    /// <param name="array">The array instance.</param>
    private Value CreateArrayAddress(Value array)
    {
        var laeaBuilder = Builder.CreateLoadArrayElementAddress(Location, array);
        for (int i = 0, e = laeaBuilder.ArrayType.NumDimensions; i < e; ++i)
            laeaBuilder.Add(Builder.CreatePrimitiveValue(Location, 0));
        return laeaBuilder.Seal();
    }

    /// <summary>
    /// Realizes an array length value.
    /// </summary>
    private void MakeLoadArrayLength()
    {
        var array = Block.Pop();

        // The length of the null reference of ldnull, such as of a null local
        // in a Release build, is the length 0 of the null array
        if (array is NullValue && array.Type is not ArrayType)
        {
            Block.Push(Builder.CreatePrimitiveValue(Location, 0));
            return;
        }

        var length = Builder.CreateGetArrayLength(
            Location,
            array).AsNotNull();
        Block.Push(length);
    }
}

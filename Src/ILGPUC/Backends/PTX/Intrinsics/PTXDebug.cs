// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXDebug.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR;
using ILGPUC.IR.Values;
using System.Reflection;
using System.Text;

namespace ILGPUC.Backends.PTX.Intrinsics;

/// <summary>
/// Intrinsic wrapper implementations for debugging.
/// </summary>
static class PTXDebug
{
    /// <summary>
    /// A handle to the <see cref="PrintF(string, void*)"/> method.
    /// </summary>
    public static readonly MethodInfo PrintFMethod =
        typeof(PTXDebug).GetMethod(
            nameof(PrintF),
            BindingFlags.Static | BindingFlags.NonPublic)
        .ThrowIfNull();

    /// <summary>
    /// A handle to the <see cref="AssertFailed(string, string, int, string, int)"/>
    /// method.
    /// </summary>
    public static readonly MethodInfo AssertFailedMethod =
        typeof(PTXDebug).GetMethod(
            nameof(AssertFailed),
            BindingFlags.Static | BindingFlags.NonPublic)
        .ThrowIfNull();

    /// <summary>
    /// Represents the intrinsic Cuda printf function.
    /// </summary>
    /// <param name="str">The string format.</param>
    /// <param name="args">A pointer to the argument structure.</param>
    /// <remarks>
    /// Both pointers must be in the generic address space.
    /// </remarks>
    [External("vprintf")]
    public static unsafe int PrintF(string str, void* args) => 0;

    /// <summary>
    /// Represents the intrinsic Cuda assertion failed function.
    /// </summary>
    /// <remarks>
    /// All strings must be in the generic address space.
    /// </remarks>
    [External("__assertfail")]
    public static void AssertFailed(
        string message,
        string file,
        int line,
        string function,
        int charSize)
    { }

    /// <summary>
    /// Maps internal debug assertions to <see cref="AssertFailed(string, string,
    /// int, string, int)"/> method calls.
    /// </summary>
    public static void ImplementDebugAssert(
        IRContext context,
        Method.Builder methodBuilder,
        BasicBlock.Builder builder,
        DebugAssertOperation debugAssert)
    {
        var location = debugAssert.Location;

        // Create a call to the debug-implementation wrapper while taking the
        // current source location into account
        var nextBlock = builder.SplitBlock(debugAssert);
        var innerBlock = methodBuilder.CreateBasicBlock(
            location,
            nameof(AssertFailed));
        builder.CreateIfBranch(
            location,
            debugAssert.Condition,
            nextBlock,
            innerBlock);

        // Create a call to the assert implementation
        var innerBuilder = methodBuilder[innerBlock];
        var assertFailed = innerBuilder.CreateCall(
            location,
            context.Declare(AssertFailedMethod, out var _));

        // Move the debug assertion to this block
        var sourceMessage = debugAssert.Message.ResolveAs<StringValue>().AsNotNull();
        var message = innerBuilder.CreatePrimitiveValue(
            location,
            sourceMessage.String,
            sourceMessage.Encoding);
        assertFailed.Add(message);

        // Append source location information
        var debugLocation = debugAssert.GetLocationInfo();
        assertFailed.Add(
            innerBuilder.CreatePrimitiveValue(location, debugLocation.FileName));
        assertFailed.Add(
            innerBuilder.CreatePrimitiveValue(location, debugLocation.Line));
        assertFailed.Add(
            innerBuilder.CreatePrimitiveValue(location, debugLocation.Method));
        assertFailed.Add(
            innerBuilder.CreatePrimitiveValue(location, 1));

        // Finish the actual assertion call and branch
        assertFailed.Seal();
        innerBuilder.CreateBranch(location, nextBlock);

        // Remove the debug assertion value
        debugAssert.Replace(builder.CreateUndefined());
    }

    /// <summary>
    /// Maps internal <see cref="WriteToOutput"/> values to
    /// <see cref="PrintF(string, void*)"/> method calls.
    /// </summary>
    public static void ImplementWriteToOutput(
        IRContext context,
        Method.Builder methodBuilder,
        BasicBlock.Builder builder,
        WriteToOutput writeToOutput)
    {
        var location = writeToOutput.Location;

        // Convert to format string constant
        var expressionString = writeToOutput.ToPrintFExpression();
        var expression = builder.CreatePrimitiveValue(
            location,
            expressionString,
            Encoding.ASCII);

        // Create an argument structure that can be passed via local memory
        var argumentBuilder = builder.CreateDynamicStructure(
            location,
            writeToOutput.Count);
        foreach (Value argument in writeToOutput.Arguments)
        {
            var converted = WriteToOutput.ConvertToPrintFArgument(
                builder,
                location,
                argument);
            argumentBuilder.Add(converted);
        }
        var argumentStructure = argumentBuilder.Seal();

        // Create local alloca to store all data
        var alloca = builder.CreateAlloca(
            location,
            argumentStructure.Type,
            MemoryAddressSpace.Local);

        // Store structure into chunk of local memory
        builder.CreateStore(location, alloca, argumentStructure);

        // Cast alloca to the generic address space to satisfy the requirements of
        // of the printf method
        alloca = builder.CreateAddressSpaceCast(
            location,
            alloca,
            MemoryAddressSpace.Generic);

        // Create a call to the native printf
        var printFMethod = context.Declare(PrintFMethod, out bool _);
        var callBuilder = builder.CreateCall(location, printFMethod);
        callBuilder.Add(expression);
        callBuilder.Add(alloca);

        // Replace the write node with the call
        callBuilder.Seal();
        builder.Remove(writeToOutput);
    }

    /// <summary>
    /// Maps internal <see cref="AsAligned"/> values to a debug assertion while
    /// preserving the <see cref="AsAligned"/> value.
    /// </summary>
    public static void ImplementAsAligned(
        IRContext context,
        Method.Builder methodBuilder,
        BasicBlock.Builder builder,
        AsAligned asAligned)
    {
        // Preserve the asAligned operation
        if (!context.Properties.EnableAssertions)
            return;

        // Check the actual pointer alignment
        var location = asAligned.Location;
        var comparison = builder.CreateCompare(
            location,
            builder.CreateArithmetic(
                location,
                builder.CreatePointerAsIntCast(
                    location,
                    asAligned.Source,
                    context.PointerBasicValueType),
                builder.CreateConvert(
                    location,
                    asAligned.AlignmentInBytes,
                    context.PointerBasicValueType),
                BinaryArithmeticKind.Rem),
            builder.CreatePrimitiveValue(
                location,
                context.PointerBasicValueType,
                0L),
            CompareKind.Equal);

        // Create the debug assertion
        Value assert = builder.CreateDebugAssert(
            location,
            comparison,
            builder.CreatePrimitiveValue(
                location,
                RuntimeErrorMessages.InvalidlyAssumedPointerOrViewAlignment));
        if (assert is DebugAssertOperation assertOperation)
            ImplementDebugAssert(context, methodBuilder, builder, assertOperation);
    }
}

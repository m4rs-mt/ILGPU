// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXAcceleratorSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend;
using ILGPU.IR;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using System.Reflection;
using System.Text;

namespace ILGPU.Backends.PTX.Transformations
{
    /// <summary>
    /// The PTX accelerator specializer.
    /// </summary>
    public sealed class PTXAcceleratorSpecializer : AcceleratorSpecializer
    {
        #region External Functions

        /// <summary>
        /// Represents the intrinsic Cuda printf function.
        /// </summary>
        /// <param name="str">The string format.</param>
        /// <param name="args">A pointer to the argument structure.</param>
        /// <remarks>
        /// Both pointers must be in the generic address space.
        /// </remarks>
        [External("vprintf")]
        private static unsafe int PrintF(string str, void* args) => 0;

        /// <summary>
        /// Represents the intrinsic Cuda assertion failed function.
        /// </summary>
        /// <remarks>
        /// All strings must be in the generic address space.
        /// </remarks>
        [External("__assertfail")]
        private static void AssertFailed(
            string message,
            string file,
            int line,
            string function,
            int charSize)
        { }

        /// <summary>
        /// A handle to the <see cref="PrintF(string, void*)"/> method.
        /// </summary>
        private static readonly MethodInfo PrintFMethod =
            typeof(PTXAcceleratorSpecializer).GetMethod(
                nameof(PrintF),
                BindingFlags.Static | BindingFlags.NonPublic)
            .ThrowIfNull();

        /// <summary>
        /// A handle to the <see cref="AssertFailed(string, string, int, string, int)"/>
        /// method.
        /// </summary>
        private static readonly MethodInfo AssertFailedMethod =
            typeof(PTXAcceleratorSpecializer).GetMethod(
                nameof(AssertFailed),
                BindingFlags.Static | BindingFlags.NonPublic)
            .ThrowIfNull();

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new PTX accelerator specializer.
        /// </summary>
        /// <param name="pointerType">The actual pointer type to use.</param>
        /// <param name="enableAssertions">True, if the assertions are enabled.</param>
        /// <param name="enableIOOperations">True, if the IO is enabled.</param>
        public PTXAcceleratorSpecializer(
            PrimitiveType pointerType,
            bool enableAssertions,
            bool enableIOOperations)
            : base(
                  AcceleratorType.Cuda,
                  PTXBackend.WarpSize,
                  pointerType,
                  enableAssertions,
                  enableIOOperations)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Maps internal debug assertions to <see cref="AssertFailed(string, string,
        /// int, string, int)"/> method calls.
        /// </summary>

        protected override void Implement(
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
        protected override void Implement(
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
        protected override void Implement(
            IRContext context,
            Method.Builder methodBuilder,
            BasicBlock.Builder builder,
            AsAligned asAligned)
        {
            // Preserve the asAligned operation
            if (!EnableAssertions)
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
                        IntPointerType.BasicValueType),
                    builder.CreateConvert(
                        location,
                        asAligned.AlignmentInBytes,
                        IntPointerType.BasicValueType),
                    BinaryArithmeticKind.Rem),
                builder.CreatePrimitiveValue(
                    location,
                    IntPointerType.BasicValueType,
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
                Implement(context, methodBuilder, builder, assertOperation);
        }

        #endregion
    }
}

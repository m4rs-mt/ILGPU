// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: PTXAcceleratorSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend;
using ILGPU.IR;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;
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
        /// Represents the intrinsic Cuda assertion failed function.
        /// </summary>
        /// <remarks>
        /// All strings must be in the generic address space.
        /// </remarks>
        private static void DebugAssertImplementation(
            bool condition,
            string message,
            string file,
            int line,
            string function,
            int charSize)
        {
            /* Implementation
            if (!condition)
            {
                AssertFailed(
                    message,
                    file,
                    line,
                    function,
                    charSize);
            }
            */
        }

        /// <summary>
        /// A handle to the <see cref="PrintF(string, void*)"/> method.
        /// </summary>
        private static readonly MethodInfo PrintFMethod =
            typeof(PTXAcceleratorSpecializer).GetMethod(
                nameof(PrintF),
                BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// A handle to the <see cref="AssertFailed(string, string, int, string, int)"/>
        /// method.
        /// </summary>
        private static readonly MethodInfo AssertFailedMethod =
            typeof(PTXAcceleratorSpecializer).GetMethod(
                nameof(AssertFailed),
                BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// A handle to the <see cref="DebugAssertImplementation(bool, string, string,
        /// int, string, int)"/> method.
        /// </summary>
        private static readonly MethodInfo DebugAssertMethod =
            typeof(PTXAcceleratorSpecializer).GetMethod(
                nameof(DebugAssertImplementation),
                BindingFlags.Static | BindingFlags.NonPublic);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new PTX accelerator specializer.
        /// </summary>
        /// <param name="pointerType">The actual pointer type to use.</param>
        /// <param name="enableAssertions">True, if the assertions are enabled.</param>
        public PTXAcceleratorSpecializer(
            PrimitiveType pointerType,
            bool enableAssertions)
            : base(
                  AcceleratorType.Cuda,
                  PTXBackend.WarpSize,
                  pointerType,
                  enableAssertions)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Maps internal debug assertions to <see cref="AssertFailed(string, string,
        /// int, string, int)"/> method calls.
        /// </summary>
        protected override void Specialize(
            in RewriterContext context,
            IRContext irContext,
            DebugAssertOperation debugAssert)
        {
            var builder = context.Builder;
            var location = debugAssert.Location;

            // Create a call to the debug-implementation wrapper while taking the
            // current source location into account
            var debugAssertMethod = BuildDebugAssertImplementation(
                irContext,
                DebugAssertMethod,
                AssertFailedMethod);

            // Create a call to the assert implementation
            var callBuilder = builder.CreateCall(location, debugAssertMethod);
            callBuilder.Add(debugAssert.Condition);
            callBuilder.Add(debugAssert.Message);

            // Append source location information
            var debugLocation = debugAssert.GetLocationInfo();
            callBuilder.Add(
                builder.CreatePrimitiveValue(location, debugLocation.FileName));
            callBuilder.Add(
                builder.CreatePrimitiveValue(location, debugLocation.Line));
            callBuilder.Add(
                builder.CreatePrimitiveValue(location, debugLocation.Method));
            callBuilder.Add(
                builder.CreatePrimitiveValue(location, 1));

            callBuilder.Seal();

            // Remove the debug assertion value
            context.Remove(debugAssert);
        }

        /// <summary>
        /// Maps internal <see cref="WriteToOutput"/> values to
        /// <see cref="PrintF(string, void*)"/> method calls.
        /// </summary>
        protected override void Specialize(
            in RewriterContext context,
            IRContext irContext,
            WriteToOutput writeToOutput)
        {
            var builder = context.Builder;
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
            var printFMethod = irContext.Declare(PrintFMethod, out bool _);
            var callBuilder = builder.CreateCall(location, printFMethod);
            callBuilder.Add(expression);
            callBuilder.Add(alloca);

            // Replace the write node with the call
            var call = callBuilder.Seal();
            context.ReplaceAndRemove(writeToOutput, call);
        }

        #endregion
    }
}

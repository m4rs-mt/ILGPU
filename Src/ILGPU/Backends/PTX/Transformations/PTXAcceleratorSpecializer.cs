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
        /// A handle to the <see cref="PrintF(string, void*)"/> method.
        /// </summary>
        private static readonly MethodInfo PrintFMethod =
            typeof(PTXAcceleratorSpecializer).GetMethod(
                nameof(PrintF),
                BindingFlags.Static | BindingFlags.NonPublic);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new PTX accelerator specializer.
        /// </summary>
        /// <param name="pointerType">The actual pointer type to use.</param>
        public PTXAcceleratorSpecializer(PrimitiveType pointerType)
            : base(
                  AcceleratorType.Cuda,
                  PTXBackend.WarpSize,
                  pointerType)
        { }

        #endregion

        #region Methods

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

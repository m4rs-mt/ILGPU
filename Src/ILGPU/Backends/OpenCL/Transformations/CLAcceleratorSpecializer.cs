// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLAcceleratorSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend;
using ILGPU.IR;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime;
using System.Reflection;

namespace ILGPU.Backends.OpenCL.Transformations
{
    /// <summary>
    /// The OpenCL accelerator specializer.
    /// </summary>
    public sealed class CLAcceleratorSpecializer : AcceleratorSpecializer
    {
        #region External Functions

        /// <summary>
        /// Represents the native OpenCL printf function.
        /// </summary>
        /// <param name="str">The string format.</param>
        /// <remarks>
        /// The variable number of arguments are not reflected in this declaration.
        /// </remarks>
        [External("printf")]
        private static unsafe void PrintF(string str)
        { }

        /// <summary>
        /// A handle to the <see cref="PrintF(string)"/> method.
        /// </summary>
        private static readonly MethodInfo PrintFMethod =
            typeof(CLAcceleratorSpecializer).GetMethod(
                nameof(PrintF),
                BindingFlags.Static | BindingFlags.NonPublic);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new OpenCL accelerator specializer.
        /// </summary>
        /// <param name="pointerType">The actual pointer type to use.</param>
        /// <param name="enableIOOperations">True, if the IO is enabled.</param>
        public CLAcceleratorSpecializer(
            PrimitiveType pointerType,
            bool enableIOOperations)
            : base(
                  AcceleratorType.OpenCL,
                  null,
                  pointerType,
                  enableAssertions: false,
                  enableIOOperations)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Maps internal <see cref="WriteToOutput"/> values to
        /// <see cref="PrintF(string)"/> method calls.
        /// </summary>
        protected override void Implement(
            IRContext context,
            Method.Builder methodBuilder,
            BasicBlock.Builder builder,
            WriteToOutput writeToOutput)
        {
            var location = writeToOutput.Location;

            // Convert to format string constant
            var expressionString = writeToOutput.ToEscapedPrintFExpression();
            var expression = builder.CreatePrimitiveValue(
                location,
                expressionString);

            // Create a call to the native printf
            var printFMethod = context.Declare(PrintFMethod, out bool _);
            var callBuilder = builder.CreateCall(location, printFMethod);
            callBuilder.Add(expression);
            foreach (Value argument in writeToOutput.Arguments)
            {
                var converted = WriteToOutput.ConvertToPrintFArgument(
                    builder,
                    location,
                    argument);
                callBuilder.Add(converted);
            }

            // Replace the write node with the call
            callBuilder.Seal();
            builder.Remove(writeToOutput);
        }

        #endregion
    }
}

// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: DisassembledMethod.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.DebugInformation;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace ILGPU.Frontend
{
    /// <summary>
    /// Represents a disassembled method.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class DisassembledMethod
    {
        #region Instance

        internal DisassembledMethod(
            MethodBase method,
            ImmutableArray<ILInstruction> instructions,
            int maxStackSize)
        {
            Debug.Assert(method != null, "Invalid method");
            Debug.Assert(
                instructions != null && instructions.Length > 0,
                "Invalid instructions");
            Method = method;
            Instructions = instructions;
            MaxStackSize = maxStackSize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the method that was disassembled.
        /// </summary>
        public MethodBase Method { get; }

        /// <summary>
        /// Returns the first disassembled instruction.
        /// </summary>
        public ILInstruction FirstInstruction => Instructions[0];

        /// <summary>
        /// Returns the first sequence point of this function.
        /// </summary>
        public SequencePoint FirstSequencePoint => FirstInstruction.SequencePoint;

        /// <summary>
        /// Returns the disassembled instructions.
        /// </summary>
        public ImmutableArray<ILInstruction> Instructions { get; }

        /// <summary>
        /// Returns the maximum stack size.
        /// </summary>
        public int MaxStackSize { get; }

        /// <summary>
        /// Returns the number of instructions.
        /// </summary>
        public int Count => Instructions.Length;

        /// <summary>
        /// Returns the instruction at the given index.
        /// </summary>
        /// <param name="index">The instruction index.</param>
        /// <returns>The instruction at the given index.</returns>
        public ILInstruction this[int index] => Instructions[index];

        #endregion

        #region Methods

        /// <summary>
        /// Returns an instruction enumerator.
        /// </summary>
        /// <returns>An instruction enumerator.</returns>
        public ImmutableArray<ILInstruction>.Enumerator GetEnumerator() =>
            Instructions.GetEnumerator();

        /// <summary>
        /// Disassembles the given method.
        /// </summary>
        /// <param name="method">The method to disassemble.</param>
        /// <returns>The disassembled method.</returns>
        public static Task<DisassembledMethod> DisassembleAsync(MethodBase method) =>
            DisassembleAsync(method, SequencePointEnumerator.Empty);

        /// <summary>
        /// Disassembles the given method.
        /// </summary>
        /// <param name="method">The method to disassemble.</param>
        /// <param name="sequencePointEnumerator">
        /// The associated sequence-point enumerator.
        /// </param>
        /// <returns>The disassembled method.</returns>
        public static Task<DisassembledMethod> DisassembleAsync(
            MethodBase method,
            SequencePointEnumerator sequencePointEnumerator) =>
            Task.Run(() =>
            {
                var context = new Disassembler(method, sequencePointEnumerator);
                return context.Disassemble();
            });

        #endregion
    }
}

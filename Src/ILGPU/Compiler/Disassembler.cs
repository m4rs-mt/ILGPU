// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Disassembler.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Compiler.DebugInformation;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPU.Compiler
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
            IReadOnlyList<ILInstruction> instructions,
            int maxStackSize)
        {
            Debug.Assert(method != null, "Invalid method");
            Debug.Assert(instructions != null && instructions.Count > 0, "Invalid instructions");
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
        /// Returns the disassembled instructions.
        /// </summary>
        public IReadOnlyList<ILInstruction> Instructions { get; }

        /// <summary>
        /// Returns the disassembled direct call instructions (call/jump).
        /// </summary>
        public IEnumerable<ILInstruction> DirectCallInstructions
        {
            get
            {
                foreach (var instruction in Instructions)
                {
                    switch (instruction.InstructionType)
                    {
                        case ILInstructionType.Jmp:
                        case ILInstructionType.Call:
                            yield return instruction;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Returns all disassembled call instructions (including calli/callvirt).
        /// </summary>
        public IEnumerable<ILInstruction> CallInstructions
        {
            get
            {
                foreach (var instruction in Instructions)
                {
                    switch (instruction.InstructionType)
                    {
                        case ILInstructionType.Jmp:
                        case ILInstructionType.Call:
                        case ILInstructionType.Calli:
                        case ILInstructionType.Callvirt:
                            yield return instruction;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the maximum stack size.
        /// </summary>
        public int MaxStackSize { get; }

        /// <summary>
        /// Returns the number of instructions.
        /// </summary>
        public int Count => Instructions.Count;

        /// <summary>
        /// Returns the instruction at the given index.
        /// </summary>
        /// <param name="index">The instruction index.</param>
        /// <returns>The instruction at the given index.</returns>
        public ILInstruction this[int index] => Instructions[index];

        #endregion

        #region Methods

        /// <summary>
        /// Disassembles the given method.
        /// </summary>
        /// <param name="method">The method to disassemble.</param>
        /// <returns>The disassembled method.</returns>
        public static DisassembledMethod Disassemble(MethodBase method)
        {
            return Disassemble(method, SequencePointEnumerator.Empty);
        }

        /// <summary>
        /// Disassembles the given method.
        /// </summary>
        /// <param name="method">The method to disassemble.</param>
        /// <param name="sequencePointEnumerator">The assocated sequence-point enumerator.</param>
        /// <returns>The disassembled method.</returns>
        public static DisassembledMethod Disassemble(MethodBase method, SequencePointEnumerator sequencePointEnumerator)
        {
            var context = new DisassemblerContext(method, sequencePointEnumerator);
            return context.Disassemble();
        }

        /// <summary>
        /// Disassembles the given method.
        /// </summary>
        /// <param name="method">The method to disassemble.</param>
        /// <param name="notSupportedILinstructionHandler">Event handler for IL instructions that are not supported.</param>
        /// <returns>The disassembled method.</returns>
        internal static DisassembledMethod Disassemble(
            MethodBase method,
            EventHandler<ILOpCode> notSupportedILinstructionHandler)
        {
            return Disassemble(method, notSupportedILinstructionHandler, SequencePointEnumerator.Empty);
        }

        /// <summary>
        /// Disassembles the given method.
        /// </summary>
        /// <param name="method">The method to disassemble.</param>
        /// <param name="notSupportedILinstructionHandler">Event handler for IL instructions that are not supported.</param>
        /// <param name="sequencePointEnumerator">The assocated sequence-point enumerator.</param>
        /// <returns>The disassembled method.</returns>
        internal static DisassembledMethod Disassemble(
            MethodBase method,
            EventHandler<ILOpCode> notSupportedILinstructionHandler,
            SequencePointEnumerator sequencePointEnumerator)
        {
            var context = new DisassemblerContext(method, sequencePointEnumerator);
            context.NotSupportedILInstruction += notSupportedILinstructionHandler;
            return context.Disassemble();
        }

        #endregion
    }

    /// <summary>
    /// Represents a disassembler for .Net methods.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    sealed partial class DisassemblerContext
    {
        #region Constants

        /// <summary>
        /// Represents the native pointer type that is used during the
        /// disassembling process.
        /// </summary>
        public static readonly Type NativePtrType = typeof(void).MakePointerType();

        #endregion

        #region Events

        /// <summary>
        /// Will be raised iff a method without a not supported IL instruction is detected.
        /// </summary>
        public event EventHandler<ILOpCode> NotSupportedILInstruction;

        #endregion

        #region Instance

        /// <summary>
        /// The current il byte code.
        /// </summary>
        private readonly byte[] il;

        /// <summary>
        /// The current offset within the byte code.
        /// </summary>
        private int ilOffset = 0;

        /// <summary>
        /// The current instruction type.
        /// </summary>
        private int instructionOffset = 0;

        /// <summary>
        /// The current flags that are applied to the next instruction.
        /// </summary>
        private ILInstructionFlags flags;

        /// <summary>
        /// The current flags argument.
        /// </summary>
        private object flagsArgument;

        /// <summary>
        /// Represents the current list of instructions.
        /// </summary>
        private readonly List<ILInstruction> instructions;

        /// <summary>
        /// Represents the associated sequence-point enumerator.
        /// </summary>
        private SequencePointEnumerator debugInformationEnumerator;

        /// <summary>
        /// Constructs a new disassembler.
        /// </summary>
        /// <param name="methodBase">The target method.</param>
        /// <param name="sequencePointEnumerator">The assocated sequence-point enumerator.</param>
        public DisassemblerContext(MethodBase methodBase, SequencePointEnumerator sequencePointEnumerator)
        {
            MethodBase = methodBase ?? throw new ArgumentNullException(nameof(methodBase));
            if (MethodBase is MethodInfo)
                MethodGenericArguments = MethodBase.GetGenericArguments();
            else
                MethodGenericArguments = new Type[0];
            TypeGenericArguments = MethodBase.DeclaringType.GetGenericArguments();
            MethodBody = MethodBase.GetMethodBody();
            Debug.Assert(MethodBody != null, "Invalid method body");
            debugInformationEnumerator = sequencePointEnumerator ?? SequencePointEnumerator.Empty;
            il = MethodBody.GetILAsByteArray();
            instructions = new List<ILInstruction>(il.Length);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current method base.
        /// </summary>
        public MethodBase MethodBase { get; }

        /// <summary>
        /// Returns the current method body.
        /// </summary>
        public MethodBody MethodBody { get; }

        /// <summary>
        /// Returns the declaring type of the method.
        /// </summary>
        public Type DeclaringType => MethodBase.DeclaringType;

        /// <summary>
        /// Returns the associated managed module.
        /// </summary>
        public Module AssociatedModule => DeclaringType.Module;

        /// <summary>
        /// Returns the generic arguments of the method.
        /// </summary>
        public Type[] MethodGenericArguments { get; }

        /// <summary>
        /// Returns the generic arguments of the declaring type.
        /// </summary>
        public Type[] TypeGenericArguments { get; }

        /// <summary>
        /// Returns the current sequence point.
        /// </summary>
        public SequencePoint? CurrentSequencePoint { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Disassembles the current method and returns a list of
        /// disassembled instructions.
        /// </summary>
        /// <returns>The list of disassembled instructions.</returns>
        public DisassembledMethod Disassemble()
        {
            while (ilOffset < il.Length)
            {
                instructionOffset = ilOffset;
                var opCode = ReadOpCode();
                if (debugInformationEnumerator.MoveTo(instructionOffset))
                    CurrentSequencePoint = debugInformationEnumerator.Current;
                if (PrefixHandlers.TryGetValue(opCode, out PrefixHandler prefixHandler))
                    prefixHandler(this);
                else if (OpCodeHandlers.TryGetValue(opCode, out OpCodeHandler opCodeHandler))
                {
                    // Handle operation
                    opCodeHandler(this);

                    // Reset flags
                    flags = ILInstructionFlags.None;
                    flagsArgument = null;
                }
                else
                {
                    if (NotSupportedILInstruction == null)
                        throw new NotSupportedException(string.Format(
                            ErrorMessages.NotSupportedILInstruction, MethodBase.Name, opCode));
                    else
                        NotSupportedILInstruction(this, opCode);
                }
            }

            return new DisassembledMethod(MethodBase, instructions, MethodBody.MaxStackSize);
        }

        /// <summary>
        /// Disassembles a call to the given method.
        /// </summary>
        /// <param name="type">The instruction type.</param>
        /// <param name="methodToken">The token of the method to be disassembled.</param>
        private void DisassembleCall(ILInstructionType type, int methodToken)
        {
            var method = ResolveMethod(methodToken);
            var popCount = method.GetParameters().Length;
            var methodInfo = method as MethodInfo;
            int pushCount = 0;
            if (methodInfo != null)
            {
                popCount += method.GetParameterOffset();
                if (methodInfo.ReturnType != typeof(void))
                    pushCount = 1;
            }
            else if (method is ConstructorInfo)
                pushCount = 1;
            AppendInstruction(type, (ushort)popCount, (ushort)pushCount, method);
        }

        /// <summary>
        /// Adds the given flags to the current instruction flags.
        /// </summary>
        /// <param name="flagsToAdd">The flags to be added.</param>
        private void AddFlags(ILInstructionFlags flagsToAdd)
        {
            flags |= flagsToAdd;
        }

        /// <summary>
        /// Appends an instruction to the current instruction list.
        /// </summary>
        /// <param name="type">The instruction type.</param>
        /// <param name="popCount">The number of elements to pop from the stack.</param>
        /// <param name="pushCount">The number of elements to push onto the stack.</param>
        /// <param name="argument">The argument of the instruction.</param>
        private void AppendInstruction(
            ILInstructionType type,
            ushort popCount,
            ushort pushCount,
            object argument = null)
        {
            AppendInstructionWithFlags(
                type,
                popCount,
                pushCount,
                ILInstructionFlags.None,
                argument);
        }

        /// <summary>
        /// Appends an instruction to the current instruction list.
        /// </summary>
        /// <param name="type">The instruction type.</param>
        /// <param name="popCount">The number of elements to pop from the stack.</param>
        /// <param name="pushCount">The number of elements to push onto the stack.</param>
        /// <param name="additionalFlags">Additional instruction flags.</param>
        /// <param name="argument">The argument of the instruction.</param>
        private void AppendInstructionWithFlags(
            ILInstructionType type,
            ushort popCount,
            ushort pushCount,
            ILInstructionFlags additionalFlags,
            object argument = null)
        {
            // Merge with current flags
            instructions.Add(new ILInstruction(
                instructionOffset,
                type,
                new ILInstructionFlagsContext(additionalFlags | flags, flagsArgument),
                popCount,
                pushCount,
                argument,
                CurrentSequencePoint));
        }

        #region Metadata

        /// <summary>
        /// Resolves the type for the given token using
        /// the current generic information.
        /// </summary>
        /// <param name="token">The token of the type to resolve.</param>
        /// <returns>The resolved type.</returns>
        private Type ResolveType(int token)
        {
            return AssociatedModule.ResolveType(
                token,
                TypeGenericArguments,
                MethodGenericArguments);
        }

        /// <summary>
        /// Resolves the method for the given token using
        /// the current generic information.
        /// </summary>
        /// <param name="token">The token of the method to resolve.</param>
        /// <returns>The resolved method.</returns>
        private MethodBase ResolveMethod(int token)
        {
            return AssociatedModule.ResolveMethod(
                token,
                TypeGenericArguments,
                MethodGenericArguments);
        }

        /// <summary>
        /// Resolves the field for the given token using
        /// the current generic information.
        /// </summary>
        /// <param name="token">The token of the field to resolve.</param>
        /// <returns>The resolved field.</returns>
        private FieldInfo ResolveField(int token)
        {
            return AssociatedModule.ResolveField(
                token,
                TypeGenericArguments,
                MethodGenericArguments);
        }

        #endregion

        #region Read Methods

        /// <summary>
        /// Reads an op-code from the current instruction data.
        /// </summary>
        /// <returns>The decoded op-code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ILOpCode ReadOpCode()
        {
            // Setup target block (if found)
            int instructionCode = il[ilOffset];
            if (instructionCode == OpCodes.Prefix1.Value)
            {
                // This is a two-byte command
                ++ilOffset;
                Debug.Assert(il.Length > ilOffset, "Invalid two-byte instruction");
                instructionCode = (instructionCode << 8) | il[ilOffset];
            }
            else
            {
                // This is a single-byte command
            }
            ++ilOffset;
            return (ILOpCode)instructionCode;
        }

        /// <summary>
        /// Reads a short branch target from the current instruction data.
        /// </summary>
        /// <returns>The decoded short branch target.</returns>
        private int ReadShortBranchTarget()
        {
            return ReadSByteArg() + ilOffset;
        }

        /// <summary>
        /// Reads a branch target from the current instruction data.
        /// </summary>
        /// <returns>The decoded branch target.</returns>
        private int ReadBranchTarget()
        {
            return ReadIntArg() + ilOffset;
        }

        /// <summary>
        /// Reads a byte from the current instruction data.
        /// </summary>
        /// <returns>The decoded byte.</returns>
        private int ReadByteArg()
        {
            return il[ilOffset++];
        }

        /// <summary>
        /// Reads a type reference from the current instruction data.
        /// </summary>
        /// <returns>The decoded type reference.</returns>
        private Type ReadTypeArg()
        {
            var token = ReadIntArg();
            return ResolveType(token);
        }

        /// <summary>
        /// Reads a field reference from the current instruction data.
        /// </summary>
        /// <returns>The decoded field reference.</returns>
        private FieldInfo ReadFieldArg()
        {
            var token = ReadIntArg();
            return ResolveField(token);
        }

        /// <summary>
        /// Reads a sbyte from the current instruction data.
        /// </summary>
        /// <returns>The decoded sbyte.</returns>
        private unsafe int ReadSByteArg()
        {
            fixed (byte* p = &il[ilOffset++])
            {
                return *(sbyte*)p;
            }
        }

        /// <summary>
        /// Reads an ushort from the current instruction data.
        /// </summary>
        /// <returns>The decoded ushort.</returns>
        private int ReadUShortArg()
        {
            var result = BitConverter.ToUInt16(il, ilOffset);
            ilOffset += sizeof(ushort);
            return result;
        }

        /// <summary>
        /// Reads an int from the current instruction data.
        /// </summary>
        /// <returns>The decoded int.</returns>
        private int ReadIntArg()
        {
            var result = BitConverter.ToInt32(il, ilOffset);
            ilOffset += sizeof(int);
            return result;
        }

        /// <summary>
        /// Reads an uint from the current instruction data.
        /// </summary>
        /// <returns>The decoded uint.</returns>
        private uint ReadUIntArg()
        {
            var result = BitConverter.ToUInt32(il, ilOffset);
            ilOffset += sizeof(uint);
            return result;
        }

        /// <summary>
        /// Reads a string from the current instruction data.
        /// </summary>
        /// <returns>The decoded string.</returns>
        private float ReadSingleArg()
        {
            var result = BitConverter.ToSingle(il, ilOffset);
            ilOffset += sizeof(float);
            return result;
        }

        /// <summary>
        /// Reads a long from the current instruction data.
        /// </summary>
        /// <returns>The decoded long.</returns>
        private long ReadLongArg()
        {
            var result = BitConverter.ToInt64(il, ilOffset);
            ilOffset += sizeof(long);
            return result;
        }

        /// <summary>
        /// Reads a double from the current instruction data.
        /// </summary>
        /// <returns>The decoded double.</returns>
        private double ReadDoubleArg()
        {
            var result = BitConverter.ToDouble(il, ilOffset);
            ilOffset += sizeof(double);
            return result;
        }

        #endregion

        #endregion
    }
}

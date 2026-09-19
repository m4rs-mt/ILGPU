// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Disassembler.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Intrinsic;
using ILGPU.Resources;
using ILGPU.Util;
using ILGPUC.IR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPUC.Frontend;

/// <summary>
/// Represents a disassembler for .Net methods.
/// </summary>
/// <remarks>Members of this class are not thread safe.</remarks>
sealed partial class Disassembler
{
    #region Constants

    /// <summary>
    /// Represents the native pointer type that is used during the
    /// disassembling process.
    /// </summary>
    public static readonly Type NativePtrType = typeof(void).MakePointerType();

    #endregion

    #region Static

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisassembledMethod? TryDisassemble(
        MethodBase methodBase,
        CompilationStackLocation? compilationStackLocation = null)
    {
        var body = methodBase.GetMethodBody();
        if (body is null) return null;

        var disassembler = new Disassembler(methodBase, body, compilationStackLocation);
        return disassembler.Disassemble();
    }

    /// <summary>
    /// Returns true if the given exception reports metadata that cannot be loaded:
    /// an assembly, type or member that the IL code, locals or attributes of a
    /// method refer to. Other exceptions are internal errors.
    /// </summary>
    /// <param name="exception">The exception to test.</param>
    /// <returns>True if the exception reports metadata that cannot be loaded.</returns>
    public static bool IsLoadError(Exception exception) =>
        exception is BadImageFormatException
            or FileNotFoundException
            or FileLoadException
            or TypeLoadException
            or MissingMemberException;

    #endregion

    #region Instance

    /// <summary>
    /// The current IL byte code.
    /// </summary>
    private readonly byte[] _il;

    /// <summary>
    /// The current offset within the byte code.
    /// </summary>
    private int _ilOffset;

    /// <summary>
    /// The current instruction type.
    /// </summary>
    private int _instructionOffset;

    /// <summary>
    /// The current flags that are applied to the next instruction.
    /// </summary>
    private ILInstructionFlags _flags;

    /// <summary>
    /// The current flags argument.
    /// </summary>
    private object? _flagsArgument;

    /// <summary>
    /// Represents the current list of instructions.
    /// </summary>
    private InlineList<ILInstruction> _instructions;

    /// <summary>
    /// Returns the source location.
    /// </summary>
    private readonly CompilationStackLocation? _compilationStackLocation;

    /// <summary>
    /// Constructs a new disassembler.
    /// </summary>
    /// <param name="methodBase">The target method.</param>
    /// <param name="methodBody">The method of the target method.</param>
    /// <param name="compilationStackLocation">The source location (optional).</param>
    private Disassembler(
        MethodBase methodBase,
        MethodBody methodBody,
        CompilationStackLocation? compilationStackLocation = null)
    {
        MethodBase = methodBase;
        MethodBody = methodBody;
        MethodGenericArguments = MethodBase is MethodInfo
            ? MethodBase.GetGenericArguments()
            : [];
        TypeGenericArguments = MethodBase.DeclaringType.AsNotNull().GetGenericArguments();
        _il = MethodBody.GetILAsByteArray() ?? [];
        _instructions = InlineList<ILInstruction>.Create(_il.Length);
        _compilationStackLocation = compilationStackLocation;
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
    public Type DeclaringType => MethodBase.DeclaringType.AsNotNull();

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

    #endregion

    #region Methods

    readonly struct MapOffsetToIndexComparer(int offset) : IComparable<ILInstruction>
    {
        public int CompareTo(ILInstruction? other) =>
            offset.CompareTo(other.AsNotNull().Offset);
    }

    /// <summary>
    /// Disassembles the current method and returns a list of
    /// disassembled instructions.
    /// </summary>
    /// <returns>The list of disassembled instructions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public DisassembledMethod Disassemble()
    {
        // Disassemble all instructions
        while (_ilOffset < _il.Length)
        {
            _instructionOffset = _ilOffset;
            try
            {
                var opCode = ReadOpCode();

                if (TryDisassemblePrefix(opCode))
                    continue;

                if (!TryDisassembleInstruction(opCode))
                {
                    throw new DisassemblerException(
                        string.Format(ErrorMessages.NotSupportedILInstruction, opCode),
                        _instructionOffset);
                }
            }
            catch (Exception exception) when (IsLoadError(exception))
            {
                // Tokens whose metadata cannot be loaded, while internal errors
                // propagate as they are
                throw new DisassemblerException(
                    exception.Message,
                    _instructionOffset,
                    exception);
            }

            // Reset flags
            _flags = ILInstructionFlags.None;
            _flagsArgument = null;
        }

        // Decode exception clauses
        var clauses = MethodBody.ExceptionHandlingClauses;
        var catchClauses = InlineList<CatchClause>.Create(clauses.Count);
        var finallyClauses = InlineList<FinallyClause>.Create(clauses.Count);
        DecodeExceptionClauses(clauses, ref catchClauses, ref finallyClauses);

        // Resolve method flags
        var flags = DisassembledMethodFlags.None;
        if (MethodBase.GetCustomAttribute<DelayCodeGenerationAttribute>() is not null)
            flags |= DisassembledMethodFlags.DelayCodeGeneration;
        if (MethodBase.GetCustomAttribute<ReplaceWithLauncherAttribute>() is not null)
            flags |= DisassembledMethodFlags.ReplaceWithLauncher;
        if (MethodBase.GetCustomAttribute<IsLauncherAttribute>() is not null)
            flags |= DisassembledMethodFlags.IsLauncher;
        if (MethodBase.GetCustomAttribute<IntrinsicAttribute>() is not null)
            flags |= DisassembledMethodFlags.InIntrinsic;

        return new DisassembledMethod(
            MethodBase,
            flags,
            _instructions.AsReadOnlyMemory(),
            catchClauses.AsReadOnlyMemory(),
            finallyClauses.AsReadOnlyMemory(),
            MethodBody.MaxStackSize);
    }

    /// <summary>
    /// Decodes all exception clauses to map entries to structures.
    /// </summary>
    /// <param name="clauses">The list of exception clauses.</param>
    /// <param name="catchClauses">The catch clauses.</param>
    /// <param name="finallyClauses">The finally clauses.</param>
    private void DecodeExceptionClauses(
        IList<ExceptionHandlingClause> clauses,
        ref InlineList<CatchClause> catchClauses,
        ref InlineList<FinallyClause> finallyClauses)
    {
        int MapOffsetToIndex(int offset) =>
            _instructions.AsSpan().BinarySearch(new MapOffsetToIndexComparer(offset));
        int MapLengthToIndex(int index, int offset, int binaryLength)
        {
            int i = index;
            int endOffset = offset + binaryLength;
            while (i < _instructions.Count)
            {
                if (endOffset == _instructions[i].Offset)
                    break;
                ++i;
            }
            return i - index;
        }

        // Disassemble all exception blocks
        foreach (var exceptionBlock in clauses)
        {
            // Map index data
            int tryIndex = MapOffsetToIndex(exceptionBlock.TryOffset);
            int tryLength = MapLengthToIndex(
                tryIndex,
                exceptionBlock.TryOffset,
                exceptionBlock.TryLength);
            int handlerIndex = MapOffsetToIndex(exceptionBlock.HandlerOffset);
            int handlerLength = MapLengthToIndex(
                handlerIndex,
                exceptionBlock.HandlerOffset,
                exceptionBlock.HandlerLength);
            switch (exceptionBlock.Flags)
            {
                case ExceptionHandlingClauseOptions.Clause:
                    catchClauses.Add(new(
                        tryIndex, tryLength,
                        handlerIndex, handlerLength));
                    break;
                case ExceptionHandlingClauseOptions.Filter:
                    catchClauses.Add(new(
                        tryIndex, tryLength,
                        handlerIndex, handlerLength,
                        MapOffsetToIndex(exceptionBlock.FilterOffset)));
                    break;
                case ExceptionHandlingClauseOptions.Finally:
                    finallyClauses.Add(new(
                        tryIndex, tryLength,
                        handlerIndex, handlerLength));
                    break;
                case ExceptionHandlingClauseOptions.Fault:
                    throw new DisassemblerException(
                        string.Format(
                            ErrorMessages.NotSupportedILInstruction,
                            exceptionBlock.Flags),
                        exceptionBlock.TryOffset);
            }
        }
    }

    /// <summary>
    /// Disassembles a call to the given method.
    /// </summary>
    /// <param name="type">The instruction type.</param>
    /// <param name="methodToken">The token of the method to be disassembled.</param>
    private void DisassembleCall(ILInstructionType type, int methodToken) =>
        AppendInstruction(type, ResolveMethod(methodToken).AsNotNull());

    /// <summary>
    /// Adds the given flags to the current instruction flags.
    /// </summary>
    /// <param name="flagsToAdd">The flags to be added.</param>
    private void AddFlags(ILInstructionFlags flagsToAdd) => _flags |= flagsToAdd;

    /// <summary>
    /// Appends an instruction to the current instruction list.
    /// </summary>
    /// <param name="type">The instruction type.</param>
    /// <param name="argument">The argument of the instruction.</param>
    private void AppendInstruction(ILInstructionType type, object? argument = null) =>
        AppendInstructionWithFlags(type, ILInstructionFlags.None, argument);

    /// <summary>
    /// Appends an instruction to the current instruction list.
    /// </summary>
    /// <param name="type">The instruction type.</param>
    /// <param name="additionalFlags">Additional instruction flags.</param>
    /// <param name="argument">The argument of the instruction.</param>
    private void AppendInstructionWithFlags(
        ILInstructionType type,
        ILInstructionFlags additionalFlags,
        object? argument = null) =>
        // Merge with current flags
        _instructions.Add(new ILInstruction(
            _instructionOffset,
            type,
            new ILInstructionFlagsContext(additionalFlags | _flags, _flagsArgument),
            argument));

    #region Metadata

    /// <summary>
    /// Resolves the given token with the given Module.Resolve* method using the
    /// current generic information. The ArgumentException with which it rejects
    /// an invalid token reports IL code that cannot be disassembled.
    /// </summary>
    /// <typeparam name="T">The type of the resolved metadata.</typeparam>
    /// <param name="token">The token to resolve.</param>
    /// <param name="resolve">The Module.Resolve* method to use.</param>
    /// <returns>The resolved metadata.</returns>
    private T Resolve<T>(int token, Func<Module, int, Type[], Type[], T> resolve)
    {
        try
        {
            return resolve(
                AssociatedModule,
                token,
                TypeGenericArguments,
                MethodGenericArguments);
        }
        catch (ArgumentException exception)
        {
            throw new DisassemblerException(
                exception.Message,
                _instructionOffset,
                exception);
        }
    }

    /// <summary>
    /// Resolves the type for the given token using
    /// the current generic information.
    /// </summary>
    /// <param name="token">The token of the type to resolve.</param>
    /// <returns>The resolved type.</returns>
    private Type ResolveType(int token) =>
        Resolve(token, static (module, t, types, methods) =>
            module.ResolveType(t, types, methods));

    /// <summary>
    /// Resolves the method for the given token using
    /// the current generic information.
    /// </summary>
    /// <param name="token">The token of the method to resolve.</param>
    /// <returns>The resolved method.</returns>
    private MethodBase? ResolveMethod(int token) =>
        Resolve(token, static (module, t, types, methods) =>
            module.ResolveMethod(t, types, methods));

    /// <summary>
    /// Resolves the field for the given token using
    /// the current generic information.
    /// </summary>
    /// <param name="token">The token of the field to resolve.</param>
    /// <returns>The resolved field.</returns>
    private FieldInfo? ResolveField(int token) =>
        Resolve(token, static (module, t, types, methods) =>
            module.ResolveField(t, types, methods));

    /// <summary>
    /// Resolves the member for the given token using
    /// the current generic information.
    /// </summary>
    /// <param name="token">The token of the member to resolve.</param>
    /// <returns>The resolved member.</returns>
    private MemberInfo? ResolveMember(int token) =>
        Resolve(token, static (module, t, types, methods) =>
            module.ResolveMember(t, types, methods));

    /// <summary>
    /// Resolves the string for the given token.
    /// </summary>
    /// <param name="token">The token of the string to resolve.</param>
    /// <returns>The resolved string.</returns>
    private string ResolveString(int token) =>
        Resolve(token, static (module, t, _, _) => module.ResolveString(t));

    #endregion

    #region Read Methods

    /// <summary>
    /// Skips the operand of the given size of the current instruction.
    /// </summary>
    /// <param name="size">The size of the operand in bytes.</param>
    /// <returns>The offset of the operand.</returns>
    private int ReadOperand(int size)
    {
        int offset = _ilOffset;
        if (_il.Length - offset < size)
        {
            throw new DisassemblerException(
                ErrorMessages.NotSupportedTruncatedILInstruction,
                _instructionOffset);
        }
        _ilOffset += size;
        return offset;
    }

    /// <summary>
    /// Reads an op-code from the current instruction data.
    /// </summary>
    /// <returns>The decoded op-code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ILOpCode ReadOpCode()
    {
        // Setup target block (if found)
        int instructionCode = _il[_ilOffset++];
        if (instructionCode == OpCodes.Prefix1.Value)
        {
            // This is a two-byte command
            instructionCode = (instructionCode << 8) | _il[ReadOperand(1)];
        }
        return (ILOpCode)instructionCode;
    }

    /// <summary>
    /// Reads a short branch target from the current instruction data.
    /// </summary>
    /// <returns>The decoded short branch target.</returns>
    private int ReadShortBranchTarget() => ReadSByteArg() + _ilOffset;

    /// <summary>
    /// Reads a branch target from the current instruction data.
    /// </summary>
    /// <returns>The decoded branch target.</returns>
    private int ReadBranchTarget() => ReadIntArg() + _ilOffset;

    /// <summary>
    /// Reads a byte from the current instruction data.
    /// </summary>
    /// <returns>The decoded byte.</returns>
    private int ReadByteArg() => _il[ReadOperand(1)];

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
    private FieldInfo? ReadFieldArg()
    {
        var token = ReadIntArg();
        return ResolveField(token);
    }

    /// <summary>
    /// Reads a sbyte from the current instruction data.
    /// </summary>
    /// <returns>The decoded sbyte.</returns>
    private int ReadSByteArg() => (sbyte)_il[ReadOperand(1)];

    /// <summary>
    /// Reads an ushort from the current instruction data.
    /// </summary>
    /// <returns>The decoded ushort.</returns>
    private int ReadUShortArg()
    {
        return BitConverter.ToUInt16(_il, ReadOperand(sizeof(ushort)));
    }

    /// <summary>
    /// Reads an int from the current instruction data.
    /// </summary>
    /// <returns>The decoded int.</returns>
    private int ReadIntArg()
    {
        return BitConverter.ToInt32(_il, ReadOperand(sizeof(int)));
    }

    /// <summary>
    /// Reads an uint from the current instruction data.
    /// </summary>
    /// <returns>The decoded uint.</returns>
    private uint ReadUIntArg()
    {
        return BitConverter.ToUInt32(_il, ReadOperand(sizeof(uint)));
    }

    /// <summary>
    /// Reads a string from the current instruction data.
    /// </summary>
    /// <returns>The decoded string.</returns>
    private float ReadSingleArg()
    {
        return BitConverter.ToSingle(_il, ReadOperand(sizeof(float)));
    }

    /// <summary>
    /// Reads a long from the current instruction data.
    /// </summary>
    /// <returns>The decoded long.</returns>
    private long ReadLongArg()
    {
        return BitConverter.ToInt64(_il, ReadOperand(sizeof(long)));
    }

    /// <summary>
    /// Reads a double from the current instruction data.
    /// </summary>
    /// <returns>The decoded double.</returns>
    private double ReadDoubleArg()
    {
        return BitConverter.ToDouble(_il, ReadOperand(sizeof(double)));
    }

    #endregion

    #endregion
}

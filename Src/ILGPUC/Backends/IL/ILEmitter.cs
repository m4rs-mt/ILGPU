// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPUC.Backends.IL;

/// <summary>
/// Represents a local variable in MSIL.
/// </summary>
/// <param name="Index">The variable index.</param>
/// <param name="VariableType">The variable type.</param>
readonly record struct ILLocal(int Index, Type VariableType);

/// <summary>
/// Represents a label in MSIL.
/// </summary>
/// <param name="Index">The label index.</param>
readonly record struct ILLabel(int Index);

/// <summary>
/// A local operation on a variable.
/// </summary>
enum LocalOperation
{
    /// <summary>
    /// Loads a local variable.
    /// </summary>
    Load,

    /// <summary>
    /// Loads the address of a local variable.
    /// </summary>
    LoadAddress,

    /// <summary>
    /// Stores a local variable.
    /// </summary>
    Store
}

/// <summary>
/// An operation on a function argument.
/// </summary>
enum ArgumentOperation
{
    /// <summary>
    /// Loads an argument.
    /// </summary>
    Load,

    /// <summary>
    /// Loads the address of an argument.
    /// </summary>
    LoadAddress,
}

/// <summary>
/// Represents an emitter for MSIL code.
/// </summary>
interface IILEmitter
{
    /// <summary>
    /// Declares a local variable.
    /// </summary>
    /// <param name="type">The variable type.</param>
    /// <returns>The variable reference.</returns>
    ILLocal DeclareLocal(Type type);

    /// <summary>
    /// Declares a pinned local variable.
    /// </summary>
    /// <param name="type">The variable type.</param>
    /// <returns>The variable reference.</returns>
    ILLocal DeclarePinnedLocal(Type type);

    /// <summary>
    /// Declares a new label.
    /// </summary>
    /// <returns>The label reference.</returns>
    ILLabel DeclareLabel();

    /// <summary>
    /// Marks the given label by associating the current
    /// instruction pointer with the jump label.
    /// </summary>
    /// <param name="label">The label to mark.</param>
    void MarkLabel(ILLabel label);

    /// <summary>
    /// Emits a new local-variable operation.
    /// </summary>
    /// <param name="operation">The operation type.</param>
    /// <param name="local">The local variable reference.</param>
    void Emit(LocalOperation operation, ILLocal local);

    /// <summary>
    /// Emits a new argument operation.
    /// </summary>
    /// <param name="operation">The operation type.</param>
    /// <param name="argumentIndex">The argument reference.</param>
    void Emit(ArgumentOperation operation, int argumentIndex);

    /// <summary>
    /// Emits a new call to the given method.
    /// </summary>
    /// <param name="target">The target to call.</param>
    void EmitCall(MethodInfo target);

    /// <summary>
    /// Emits a new object instruction.
    /// </summary>
    /// <param name="info">The constructor to call.</param>
    void EmitNewObject(ConstructorInfo info);

    /// <summary>
    /// Emits a local memory allocation.
    /// </summary>
    /// <param name="size">The size in bytes to allocate.</param>
    void EmitAlloca(int size);

    /// <summary>
    /// Emits a new constant.
    /// </summary>
    /// <param name="constant">The constant to emit.</param>
    void EmitConstant(string constant);

    /// <summary>
    /// Emits a new constant.
    /// </summary>
    /// <param name="constant">The constant to emit.</param>
    void EmitConstant(int constant);

    /// <summary>
    /// Emits a new constant.
    /// </summary>
    /// <param name="constant">The constant to emit.</param>
    void EmitConstant(long constant);

    /// <summary>
    /// Emits a new constant.
    /// </summary>
    /// <param name="constant">The constant to emit.</param>
    void EmitConstant(float constant);

    /// <summary>
    /// Emits a new constant.
    /// </summary>
    /// <param name="constant">The constant to emit.</param>
    void EmitConstant(double constant);

    /// <summary>
    /// Emits a new operation.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    void Emit(OpCode opCode);

    /// <summary>
    /// Emits a new operation.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    /// <param name="label">The label argument.</param>
    void Emit(OpCode opCode, ILLabel label);

    /// <summary>
    /// Emits a new operation.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    /// <param name="type">The type argument.</param>
    void Emit(OpCode opCode, Type type);

    /// <summary>
    /// Emits a new operation.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    /// <param name="field">The field argument.</param>
    void Emit(OpCode opCode, FieldInfo field);

    /// <summary>
    /// Emits a switch instruction.
    /// </summary>
    /// <param name="labels">The jump targets.</param>
    void EmitSwitch(ILLabel[] labels);

    /// <summary>
    /// Emits code to write something to the console.
    /// </summary>
    /// <param name="message">The message to write.</param>
    void EmitWriteLine(string message);

    /// <summary>
    /// Finishes the code generation process.
    /// </summary>
    void Finish();
}

/// <summary>
/// A default IL emitter.
/// </summary>
/// <remarks>
/// Constructs a new IL emitter.
/// </remarks>
/// <param name="generator">The associated IL generator.</param>
readonly struct ILEmitter(ILGenerator generator) : IILEmitter
{
    #region Instance

    private readonly List<LocalBuilder> _declaredLocals = [];
    private readonly List<Label> _declaredLabels = [];

    #endregion

    #region Methods

    /// <summary>
    /// Declares a internal local.
    /// </summary>
    /// <param name="type">The local type.</param>
    /// <param name="pinned">True if the local is pinned.</param>
    /// <returns>The declared local.</returns>
    private ILLocal DeclareLocalInternal(Type type, bool pinned)
    {
        var local = generator.DeclareLocal(type, pinned);
        var result = new ILLocal(_declaredLocals.Count, type);
        _declaredLocals.Add(local);
        return result;
    }

    /// <summary cref="IILEmitter.DeclareLocal(Type)"/>
    public ILLocal DeclareLocal(Type type) =>
        DeclareLocalInternal(type, false);

    /// <summary cref="IILEmitter.DeclarePinnedLocal(Type)"/>
    public ILLocal DeclarePinnedLocal(Type type) =>
        DeclareLocalInternal(type, true);

    /// <summary cref="IILEmitter.DeclareLabel"/>
    public ILLabel DeclareLabel()
    {
        var label = generator.DefineLabel();
        var result = new ILLabel(_declaredLabels.Count);
        _declaredLabels.Add(label);
        return result;
    }

    /// <summary cref="IILEmitter.MarkLabel(ILLabel)"/>
    public void MarkLabel(ILLabel label) =>
        generator.MarkLabel(_declaredLabels[label.Index]);

    /// <summary cref="IILEmitter.Emit(LocalOperation, ILLocal)"/>
    public void Emit(LocalOperation operation, ILLocal local)
    {
        var localBuilder = _declaredLocals[local.Index];
        switch (operation)
        {
            case LocalOperation.Load:
                generator.Emit(OpCodes.Ldloc, localBuilder);
                break;
            case LocalOperation.LoadAddress:
                generator.Emit(OpCodes.Ldloca, localBuilder);
                break;
            default:
                generator.Emit(OpCodes.Stloc, localBuilder);
                break;
        }
    }

    /// <summary cref="IILEmitter.Emit(ArgumentOperation, int)"/>
    public void Emit(ArgumentOperation operation, int argumentIndex)
    {
        switch (operation)
        {
            case ArgumentOperation.Load:
                generator.Emit(OpCodes.Ldarg, argumentIndex);
                break;
            default:
                generator.Emit(OpCodes.Ldarga, argumentIndex);
                break;
        }
    }

    /// <summary cref="IILEmitter.EmitCall(MethodInfo)"/>
    public void EmitCall(MethodInfo target) =>
        generator.Emit(target.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, target);

    /// <summary cref="IILEmitter.EmitNewObject(ConstructorInfo)"/>
    public void EmitNewObject(ConstructorInfo info) =>
        generator.Emit(OpCodes.Newobj, info);

    /// <summary cref="IILEmitter.Emit(OpCode)"/>
    public void Emit(OpCode opCode) =>
        generator.Emit(opCode);

    /// <summary cref="IILEmitter.Emit(OpCode, ILLabel)"/>
    public void Emit(OpCode opCode, ILLabel label) =>
        generator.Emit(opCode, _declaredLabels[label.Index]);

    /// <summary cref="IILEmitter.Emit(OpCode, Type)"/>
    public void Emit(OpCode opCode, Type type) =>
        generator.Emit(opCode, type);

    /// <summary cref="IILEmitter.Emit(OpCode, FieldInfo)"/>
    public void Emit(OpCode opCode, FieldInfo field) =>
        generator.Emit(opCode, field);

    /// <summary cref="IILEmitter.EmitAlloca(int)"/>
    public void EmitAlloca(int size) =>
        generator.Emit(OpCodes.Localloc, size);

    /// <summary cref="IILEmitter.EmitConstant(string)"/>
    public void EmitConstant(string constant) =>
        generator.Emit(OpCodes.Ldstr, constant);

    /// <summary cref="IILEmitter.EmitConstant(int)"/>
    public void EmitConstant(int constant) =>
        generator.Emit(OpCodes.Ldc_I4, constant);

    /// <summary cref="IILEmitter.EmitConstant(long)"/>
    public void EmitConstant(long constant) =>
        generator.Emit(OpCodes.Ldc_I8, constant);

    /// <summary cref="IILEmitter.EmitConstant(float)"/>
    public void EmitConstant(float constant) =>
        generator.Emit(OpCodes.Ldc_R4, constant);

    /// <summary cref="IILEmitter.EmitConstant(double)"/>
    public void EmitConstant(double constant) =>
        generator.Emit(OpCodes.Ldc_R8, constant);

    /// <summary cref="IILEmitter.EmitSwitch(ILLabel[])"/>
    public void EmitSwitch(params ILLabel[] labels)
    {
        var switchLabels = new Label[labels.Length];
        for (int i = 0; i < labels.Length; ++i)
            switchLabels[i] = _declaredLabels[labels[i].Index];
        generator.Emit(OpCodes.Switch, switchLabels);
    }

    /// <summary cref="IILEmitter.EmitWriteLine"/>
    public void EmitWriteLine(string message) =>
        generator.EmitWriteLine(message);

    /// <summary cref="IILEmitter.Finish"/>
    public void Finish() { }

    #endregion
}

/// <summary>
/// Represents an IL emitter for debugging purposes.
/// </summary>
/// <remarks>
/// Constructs a new IL emitter for debugging purposes.
/// </remarks>
/// <param name="writer">The associated text writer.</param>
readonly struct DebugILEmitter(TextWriter writer) : IILEmitter
{
    #region Instance

    private readonly List<ILLocal> _locals = [];
    private readonly List<ILLabel> _labels = [];

    #endregion

    #region Methods

    /// <summary>
    /// Declares a locally internal type.
    /// </summary>
    /// <param name="type">The allocation type.</param>
    /// <returns>The allocated local.</returns>
    private ILLocal DeclareLocalInternal(Type type)
    {
        var result = new ILLocal(_locals.Count, type);
        _locals.Add(result);
        return result;
    }

    /// <summary cref="IILEmitter.DeclareLocal(Type)"/>
    public ILLocal DeclareLocal(Type type) =>
        DeclareLocalInternal(type);

    /// <summary cref="IILEmitter.DeclarePinnedLocal(Type)"/>
    public ILLocal DeclarePinnedLocal(Type type) =>
        DeclareLocalInternal(type);

    /// <summary cref="IILEmitter.DeclareLabel"/>
    public ILLabel DeclareLabel()
    {
        var result = new ILLabel(_labels.Count);
        _labels.Add(result);
        return result;
    }

    /// <summary cref="IILEmitter.MarkLabel(ILLabel)"/>
    public void MarkLabel(ILLabel label)
    {
        writer.Write("Label_");
        writer.Write(label.Index);
        writer.WriteLine(":");
    }

    private void EmitPrefix() =>
        writer.Write("\t");

    /// <summary cref="IILEmitter.Emit(LocalOperation, ILLocal)"/>
    public void Emit(LocalOperation operation, ILLocal local)
    {
        EmitPrefix();
        switch (operation)
        {
            case LocalOperation.Load:
                writer.Write("ldloc ");
                break;
            case LocalOperation.LoadAddress:
                writer.Write("ldloca ");
                break;
            default:
                writer.Write("stloc ");
                break;
        }
        writer.WriteLine(local.Index);
    }

    /// <summary cref="IILEmitter.Emit(ArgumentOperation, int)"/>
    public void Emit(ArgumentOperation operation, int argumentIndex)
    {
        EmitPrefix();
        switch (operation)
        {
            case ArgumentOperation.Load:
                writer.Write("ldarg ");
                break;
            default:
                writer.Write("ldarga ");
                break;
        }
        writer.WriteLine(argumentIndex);
    }

    /// <summary cref="IILEmitter.EmitCall(MethodInfo)"/>
    public void EmitCall(MethodInfo target)
    {
        EmitPrefix();
        writer.Write(target.IsVirtual ? "callvirt " : "call ");
        if (target.DeclaringType is not null)
            writer.Write(target.DeclaringType.FullName);
        writer.Write('.');
        writer.WriteLine(target.Name);
    }

    /// <summary cref="IILEmitter.EmitNewObject(ConstructorInfo)"/>
    public void EmitNewObject(ConstructorInfo info)
    {
        EmitPrefix();
        writer.Write("newobj ");
        writer.Write(info.DeclaringType.AsNotNull().FullName);
        writer.Write('.');
        writer.WriteLine(info.Name);
    }

    /// <summary cref="IILEmitter.Emit(OpCode)"/>
    public void Emit(OpCode opCode)
    {
        EmitPrefix();
        writer.WriteLine(opCode.Name);
    }

    /// <summary cref="IILEmitter.Emit(OpCode, ILLabel)"/>
    public void Emit(OpCode opCode, ILLabel label)
    {
        EmitPrefix();
        writer.Write(opCode.Name);
        writer.Write(" Label_");
        writer.WriteLine(label.Index);
    }

    /// <summary cref="IILEmitter.Emit(OpCode, Type)"/>
    public void Emit(OpCode opCode, Type type)
    {
        EmitPrefix();
        writer.Write(opCode.Name);
        writer.Write(' ');
        writer.WriteLine(type.FullName);
    }

    /// <summary cref="IILEmitter.Emit(OpCode, FieldInfo)"/>
    public void Emit(OpCode opCode, FieldInfo field)
    {
        EmitPrefix();
        writer.Write(opCode.Name);
        writer.Write(' ');
        writer.WriteLine(field.Name);
    }

    /// <summary cref="IILEmitter.EmitAlloca(int)"/>
    public void EmitAlloca(int size)
    {
        EmitPrefix();
        writer.Write("localloc ");
        writer.WriteLine(size);
    }

    /// <summary cref="IILEmitter.EmitConstant(string)"/>
    public void EmitConstant(string constant)
    {
        EmitPrefix();
        writer.Write("ldstr ");
        writer.WriteLine(constant);
    }

    /// <summary cref="IILEmitter.EmitConstant(int)"/>
    public void EmitConstant(int constant)
    {
        EmitPrefix();
        writer.Write("ldc.i4 ");
        writer.WriteLine(constant);
    }

    /// <summary cref="IILEmitter.EmitConstant(long)"/>
    public void EmitConstant(long constant)
    {
        EmitPrefix();
        writer.Write("ldc.i8 ");
        writer.WriteLine(constant);
    }

    /// <summary cref="IILEmitter.EmitConstant(float)"/>
    public void EmitConstant(float constant)
    {
        EmitPrefix();
        writer.Write("ldc.r4 ");
        writer.WriteLine(constant);
    }

    /// <summary cref="IILEmitter.EmitConstant(double)"/>
    public void EmitConstant(double constant)
    {
        EmitPrefix();
        writer.Write("ldc.r8 ");
        writer.WriteLine(constant);
    }

    /// <summary cref="IILEmitter.EmitSwitch(ILLabel[])"/>
    public void EmitSwitch(params ILLabel[] labels)
    {
        EmitPrefix();
        writer.WriteLine("switch");
        foreach (var label in labels)
        {
            EmitPrefix();
            writer.Write("  Label_");
            writer.WriteLine(label.Index);
        }
    }

    /// <summary cref="IILEmitter.EmitWriteLine"/>
    public void EmitWriteLine(string message) =>
        writer.WriteLine($" => Write('{message}')");

    /// <summary cref="IILEmitter.Finish"/>
    public void Finish()
    {
        // Write all locals
        writer.WriteLine("Locals:");
        foreach (var local in _locals)
        {
            EmitPrefix();
            writer.Write("  local ");
            writer.Write(local.Index);
            writer.Write(": ");
            writer.WriteLine(local.VariableType.FullName);
        }
    }

    #endregion
}

/// <summary>
/// Represents a no-operation IL emitter.
/// </summary>
readonly struct NopILEmitter : IILEmitter
{
    #region Methods

    /// <summary cref="IILEmitter.DeclareLocal(Type)"/>
    public ILLocal DeclareLocal(Type type) => new ILLocal(0, type);

    /// <summary cref="IILEmitter.DeclarePinnedLocal(Type)"/>
    public ILLocal DeclarePinnedLocal(Type type) => new ILLocal(0, type);

    /// <summary cref="IILEmitter.DeclareLabel"/>
    public ILLabel DeclareLabel() => new ILLabel(-1);

    /// <summary cref="IILEmitter.MarkLabel(ILLabel)"/>
    public void MarkLabel(ILLabel label) { }

    /// <summary cref="IILEmitter.Emit(LocalOperation, ILLocal)"/>
    public void Emit(LocalOperation operation, ILLocal local) { }

    /// <summary cref="IILEmitter.Emit(ArgumentOperation, int)"/>
    public void Emit(ArgumentOperation operation, int argumentIndex) { }

    /// <summary cref="IILEmitter.EmitCall(MethodInfo)"/>
    public void EmitCall(MethodInfo target) { }

    /// <summary cref="IILEmitter.EmitNewObject(ConstructorInfo)"/>
    public void EmitNewObject(ConstructorInfo info) { }

    /// <summary cref="IILEmitter.Emit(OpCode)"/>
    public void Emit(OpCode opCode) { }

    /// <summary cref="IILEmitter.Emit(OpCode, ILLabel)"/>
    public void Emit(OpCode opCode, ILLabel label) { }

    /// <summary cref="IILEmitter.Emit(OpCode, Type)"/>
    public void Emit(OpCode opCode, Type type) { }

    /// <summary cref="IILEmitter.Emit(OpCode, FieldInfo)"/>
    public void Emit(OpCode opCode, FieldInfo field) { }

    /// <summary cref="IILEmitter.EmitAlloca(int)"/>
    public void EmitAlloca(int size) { }

    /// <summary cref="IILEmitter.EmitConstant(string)"/>
    public void EmitConstant(string constant) { }

    /// <summary cref="IILEmitter.EmitConstant(int)"/>
    public void EmitConstant(int constant) { }

    /// <summary cref="IILEmitter.EmitConstant(long)"/>
    public void EmitConstant(long constant) { }

    /// <summary cref="IILEmitter.EmitConstant(float)"/>
    public void EmitConstant(float constant) { }

    /// <summary cref="IILEmitter.EmitConstant(double)"/>
    public void EmitConstant(double constant) { }

    /// <summary cref="IILEmitter.EmitSwitch(ILLabel[])"/>
    public void EmitSwitch(params ILLabel[] labels) { }

    /// <summary cref="IILEmitter.EmitWriteLine"/>
    public void EmitWriteLine(string message) { }

    /// <summary cref="IILEmitter.Finish"/>
    public void Finish() { }

    #endregion
}

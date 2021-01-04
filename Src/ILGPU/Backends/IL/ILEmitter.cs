// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ILEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// Represents a local variable in MSIL.
    /// </summary>
    public readonly struct ILLocal
    {
        /// <summary>
        /// Constructs a new local variable in MSIL.
        /// </summary>
        /// <param name="index">The variable index.</param>
        /// <param name="type">The variable type.</param>
        public ILLocal(int index, Type type)
        {
            Debug.Assert(index >= 0 && index <= ushort.MaxValue, "Invalid local index");
            Debug.Assert(type != null, "Invalid type");
            Index = index;
            VariableType = type;
        }

        /// <summary>
        /// Returns the variable index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Returns the variable type.
        /// </summary>
        public Type VariableType { get; }
    }

    /// <summary>
    /// Represents a label in MSIL.
    /// </summary>
    public readonly struct ILLabel
    {
        /// <summary>
        /// Constructs a new label.
        /// </summary>
        /// <param name="index">The label index.</param>
        public ILLabel(int index)
        {
            Debug.Assert(index >= 0, "Invalid label index");
            Index = index;
        }

        /// <summary>
        /// Returns the assigned label index.
        /// </summary>
        public int Index { get; }
    }

    /// <summary>
    /// A local operation on a variable.
    /// </summary>
    public enum LocalOperation
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
    public enum ArgumentOperation
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
    public interface IILEmitter
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
        /// Finishes the code generation process.
        /// </summary>
        void Finish();
    }

    /// <summary>
    /// A default IL emitter.
    /// </summary>
    public readonly struct ILEmitter : IILEmitter
    {
        #region Instance

        private readonly List<LocalBuilder> declaredLocals;
        private readonly List<Label> declaredLabels;

        /// <summary>
        /// Constructs a new IL emitter.
        /// </summary>
        /// <param name="generator">The associated IL generator.</param>
        public ILEmitter(ILGenerator generator)
        {
            Debug.Assert(generator != null, "Invalid generator");
            Generator = generator;

            declaredLocals = new List<LocalBuilder>();
            declaredLabels = new List<Label>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the underlying generator.
        /// </summary>
        public ILGenerator Generator { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Declares a internal local.
        /// </summary>
        /// <param name="type">The local type.</param>
        /// <param name="pinned">True, if the local is pinned.</param>
        /// <returns>The declared local.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ILLocal DeclareLocalInternal(Type type, bool pinned)
        {
            var local = Generator.DeclareLocal(type);
            var result = new ILLocal(declaredLocals.Count, type);
            declaredLocals.Add(local);
            return result;
        }

        /// <summary cref="IILEmitter.DeclareLocal(Type)"/>
        public ILLocal DeclareLocal(Type type) =>
            DeclareLocalInternal(type, false);

        /// <summary cref="IILEmitter.DeclarePinnedLocal(Type)"/>
        public ILLocal DeclarePinnedLocal(Type type) =>
            DeclareLocalInternal(type, true);

        /// <summary cref="IILEmitter.DeclareLabel"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILLabel DeclareLabel()
        {
            var label = Generator.DefineLabel();
            var result = new ILLabel(declaredLabels.Count);
            declaredLabels.Add(label);
            return result;
        }

        /// <summary cref="IILEmitter.MarkLabel(ILLabel)"/>
        public void MarkLabel(ILLabel label) =>
            Generator.MarkLabel(declaredLabels[label.Index]);

        /// <summary cref="IILEmitter.Emit(LocalOperation, ILLocal)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Emit(LocalOperation operation, ILLocal local)
        {
            var localBuilder = declaredLocals[local.Index];
            switch (operation)
            {
                case LocalOperation.Load:
                    Generator.Emit(OpCodes.Ldloc, localBuilder);
                    break;
                case LocalOperation.LoadAddress:
                    Generator.Emit(OpCodes.Ldloca, localBuilder);
                    break;
                default:
                    Generator.Emit(OpCodes.Stloc, localBuilder);
                    break;
            }
        }

        /// <summary cref="IILEmitter.Emit(ArgumentOperation, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Emit(ArgumentOperation operation, int argumentIndex)
        {
            switch (operation)
            {
                case ArgumentOperation.Load:
                    Generator.Emit(OpCodes.Ldarg, argumentIndex);
                    break;
                default:
                    Generator.Emit(OpCodes.Ldarga, argumentIndex);
                    break;
            }
        }

        /// <summary cref="IILEmitter.EmitCall(MethodInfo)"/>
        public void EmitCall(MethodInfo target) =>
            Generator.Emit(target.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, target);

        /// <summary cref="IILEmitter.EmitNewObject(ConstructorInfo)"/>
        public void EmitNewObject(ConstructorInfo info) =>
            Generator.Emit(OpCodes.Newobj, info);

        /// <summary cref="IILEmitter.Emit(OpCode)"/>
        public void Emit(OpCode opCode) =>
            Generator.Emit(opCode);

        /// <summary cref="IILEmitter.Emit(OpCode, ILLabel)"/>
        public void Emit(OpCode opCode, ILLabel label) =>
            Generator.Emit(opCode, declaredLabels[label.Index]);

        /// <summary cref="IILEmitter.Emit(OpCode, Type)"/>
        public void Emit(OpCode opCode, Type type) =>
            Generator.Emit(opCode, type);

        /// <summary cref="IILEmitter.Emit(OpCode, FieldInfo)"/>
        public void Emit(OpCode opCode, FieldInfo field) =>
            Generator.Emit(opCode, field);

        /// <summary cref="IILEmitter.EmitAlloca(int)"/>
        public void EmitAlloca(int size) =>
            Generator.Emit(OpCodes.Localloc, size);

        /// <summary cref="IILEmitter.EmitConstant(string)"/>
        public void EmitConstant(string constant) =>
            Generator.Emit(OpCodes.Ldstr, constant);

        /// <summary cref="IILEmitter.EmitConstant(int)"/>
        public void EmitConstant(int constant) =>
            Generator.Emit(OpCodes.Ldc_I4, constant);

        /// <summary cref="IILEmitter.EmitConstant(long)"/>
        public void EmitConstant(long constant) =>
            Generator.Emit(OpCodes.Ldc_I8, constant);

        /// <summary cref="IILEmitter.EmitConstant(float)"/>
        public void EmitConstant(float constant) =>
            Generator.Emit(OpCodes.Ldc_R4, constant);

        /// <summary cref="IILEmitter.EmitConstant(double)"/>
        public void EmitConstant(double constant) =>
            Generator.Emit(OpCodes.Ldc_R8, constant);

        /// <summary cref="IILEmitter.EmitSwitch(ILLabel[])"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EmitSwitch(params ILLabel[] labels)
        {
            var switchLabels = new Label[labels.Length];
            for (int i = 0; i < labels.Length; ++i)
                switchLabels[i] = declaredLabels[labels[i].Index];
            Generator.Emit(OpCodes.Switch, switchLabels);
        }

        /// <summary cref="IILEmitter.Finish"/>
        public void Finish() { }

        #endregion
    }

    /// <summary>
    /// Represents an IL emitter for debugging purposes.
    /// </summary>
    public readonly struct DebugILEmitter : IILEmitter
    {
        #region Instance

        private readonly List<ILLocal> locals;
        private readonly List<ILLabel> labels;

        /// <summary>
        /// Constructs a new IL emitter for debugging purposes.
        /// </summary>
        /// <param name="writer">The associated text writer.</param>
        public DebugILEmitter(TextWriter writer)
        {
            Debug.Assert(writer != null, "Invalid stream writer");
            locals = new List<ILLocal>();
            labels = new List<ILLabel>();
            Writer = writer;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated stream writer.
        /// </summary>
        public TextWriter Writer { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Declares a locally internal type.
        /// </summary>
        /// <param name="type">The allocation type.</param>
        /// <returns>The allocated local.</returns>
        private ILLocal DeclareLocalInternal(Type type)
        {
            var result = new ILLocal(locals.Count, type);
            locals.Add(result);
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
            var result = new ILLabel(labels.Count);
            labels.Add(result);
            return result;
        }

        /// <summary cref="IILEmitter.MarkLabel(ILLabel)"/>
        public void MarkLabel(ILLabel label)
        {
            Writer.Write("Label_");
            Writer.Write(label.Index);
            Writer.WriteLine(":");
        }

        private void EmitPrefix() =>
            Writer.Write("\t");

        /// <summary cref="IILEmitter.Emit(LocalOperation, ILLocal)"/>
        public void Emit(LocalOperation operation, ILLocal local)
        {
            EmitPrefix();
            switch (operation)
            {
                case LocalOperation.Load:
                    Writer.Write("ldloc ");
                    break;
                case LocalOperation.LoadAddress:
                    Writer.Write("ldloca ");
                    break;
                default:
                    Writer.Write("stloc ");
                    break;
            }
            Writer.WriteLine(local.Index);
        }

        /// <summary cref="IILEmitter.Emit(ArgumentOperation, int)"/>
        public void Emit(ArgumentOperation operation, int argumentIndex)
        {
            EmitPrefix();
            switch (operation)
            {
                case ArgumentOperation.Load:
                    Writer.Write("ldarg ");
                    break;
                default:
                    Writer.Write("ldarga ");
                    break;
            }
            Writer.WriteLine(argumentIndex);
        }

        /// <summary cref="IILEmitter.EmitCall(MethodInfo)"/>
        public void EmitCall(MethodInfo target)
        {
            EmitPrefix();
            Writer.Write(target.IsVirtual ? "callvirt " : "call ");
            Writer.Write(target.DeclaringType.GetStringRepresentation());
            Writer.Write('.');
            Writer.WriteLine(target.Name);
        }

        /// <summary cref="IILEmitter.EmitNewObject(ConstructorInfo)"/>
        public void EmitNewObject(ConstructorInfo info)
        {
            EmitPrefix();
            Writer.Write("newobj ");
            Writer.Write(info.DeclaringType.GetStringRepresentation());
            Writer.Write('.');
            Writer.WriteLine(info.Name);
        }

        /// <summary cref="IILEmitter.Emit(OpCode)"/>
        public void Emit(OpCode opCode)
        {
            EmitPrefix();
            Writer.WriteLine(opCode.Name);
        }

        /// <summary cref="IILEmitter.Emit(OpCode, ILLabel)"/>
        public void Emit(OpCode opCode, ILLabel label)
        {
            EmitPrefix();
            Writer.Write(opCode.Name);
            Writer.Write(" Label_");
            Writer.WriteLine(label.Index);
        }

        /// <summary cref="IILEmitter.Emit(OpCode, Type)"/>
        public void Emit(OpCode opCode, Type type)
        {
            EmitPrefix();
            Writer.Write(opCode.Name);
            Writer.Write(' ');
            Writer.WriteLine(type.GetStringRepresentation());
        }

        /// <summary cref="IILEmitter.Emit(OpCode, FieldInfo)"/>
        public void Emit(OpCode opCode, FieldInfo field)
        {
            EmitPrefix();
            Writer.Write(opCode.Name);
            Writer.Write(' ');
            Writer.WriteLine(field.Name);
        }

        /// <summary cref="IILEmitter.EmitAlloca(int)"/>
        public void EmitAlloca(int size)
        {
            EmitPrefix();
            Writer.Write("localloc ");
            Writer.WriteLine(size);
        }

        /// <summary cref="IILEmitter.EmitConstant(string)"/>
        public void EmitConstant(string constant)
        {
            EmitPrefix();
            Writer.Write("ldstr ");
            Writer.WriteLine(constant);
        }

        /// <summary cref="IILEmitter.EmitConstant(int)"/>
        public void EmitConstant(int constant)
        {
            EmitPrefix();
            Writer.Write("ldc.i4 ");
            Writer.WriteLine(constant);
        }

        /// <summary cref="IILEmitter.EmitConstant(long)"/>
        public void EmitConstant(long constant)
        {
            EmitPrefix();
            Writer.Write("ldc.i8 ");
            Writer.WriteLine(constant);
        }

        /// <summary cref="IILEmitter.EmitConstant(float)"/>
        public void EmitConstant(float constant)
        {
            EmitPrefix();
            Writer.Write("ldc.r4 ");
            Writer.WriteLine(constant);
        }

        /// <summary cref="IILEmitter.EmitConstant(double)"/>
        public void EmitConstant(double constant)
        {
            EmitPrefix();
            Writer.Write("ldc.r8 ");
            Writer.WriteLine(constant);
        }

        /// <summary cref="IILEmitter.EmitSwitch(ILLabel[])"/>
        public void EmitSwitch(params ILLabel[] labels)
        {
            EmitPrefix();
            Writer.WriteLine("switch");
            foreach (var label in labels)
            {
                EmitPrefix();
                Writer.Write("  Label_");
                Writer.WriteLine(label.Index);
            }
        }

        /// <summary cref="IILEmitter.Finish"/>
        public void Finish()
        {
            // Write all locals
            Writer.WriteLine("Locals:");
            foreach (var local in locals)
            {
                EmitPrefix();
                Writer.Write("  local ");
                Writer.Write(local.Index);
                Writer.Write(": ");
                Writer.WriteLine(
                    local.VariableType.GetStringRepresentation());
            }
        }

        #endregion
    }
}

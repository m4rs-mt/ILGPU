// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityInstructions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Runtime.Velocity;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ILGPU.Backends.Velocity
{
    sealed class VelocityInstructions : VelocityOperations
    {
        #region Static

        /// <summary>
        /// Implements a debug assertion failure.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DebugAssert(
            VelocityLaneMask laneMask,
            VelocityWarp32 condition,
            string message)
        {
            // Check for failed lanes
            var conditionMask = VelocityWarp32.ToMask(condition);
            var failedConditionMask = VelocityLaneMask.Negate(conditionMask);
            var assertionMask = VelocityLaneMask.Intersect(laneMask, failedConditionMask);
            if (!assertionMask.HasAny)
                return;
            Debug.WriteLine(message);
            Debug.Fail(message);
        }

        /// <summary>
        /// Dumps the given velocity warp.
        /// </summary>
        public static void Dump32(VelocityWarp32 warp) =>
            Console.WriteLine(warp.ToString());

        /// <summary>
        /// Dumps the given velocity warp.
        /// </summary>
        public static void Dump64(VelocityWarp64 warp) =>
            Console.WriteLine(warp.ToString());

        /// <summary>
        /// Inspects the given value by dumping meta-level information.
        /// </summary>
        public static void InspectValue<T>(T value)
        {
            var type = value.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var fieldValue = field.GetValue(value);
                Console.WriteLine(fieldValue.ToString());
            }
            Console.WriteLine(value.ToString());
        }

        #endregion

        #region Instance

        /// <summary>
        /// The memory barrier method.
        /// </summary>
        private readonly MethodInfo memoryBarrierMethod;

        /// <summary>
        /// The default failed assertion method.
        /// </summary>
        private readonly MethodInfo assertMethod;

        /// <summary>
        /// The generic write method.
        /// </summary>
        private readonly MethodInfo writeMethod;

        /// <summary>
        /// Inspects a generic value.
        /// </summary>
        private readonly MethodInfo inspectValueMethod;

        /// <summary>
        /// Dumps a 32bit velocity warp.
        /// </summary>
        private readonly MethodInfo dump32Method;

        /// <summary>
        /// Dumps a 64bit velocity warp.
        /// </summary>
        private readonly MethodInfo dump64Method;

        /// <summary>
        /// Initializes all general runtime methods.
        /// </summary>
        public VelocityInstructions()
        {
            memoryBarrierMethod = GetMethod(
                typeof(Interlocked),
                nameof(Interlocked.MemoryBarrier));

            assertMethod = GetMethod(
                typeof(VelocityInstructions),
                nameof(DebugAssert));
            writeMethod = GetMethod(
                typeof(Interop),
                nameof(Interop.WriteImplementation));
            inspectValueMethod = GetMethod(
                typeof(VelocityInstructions),
                nameof(InspectValue));
            dump32Method = GetMethod(
                typeof(VelocityInstructions),
                nameof(Dump32));
            dump64Method = GetMethod(
                typeof(VelocityInstructions),
                nameof(Dump64));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets load and store operations for the given basic value type. The IO
        /// operations automatically convert from vectorized types into scalar types
        /// while taking an additional mask parameter and the current warp size into
        /// account.
        /// </summary>
        /// <param name="basicValueType">The basic value type.</param>
        /// <param name="warpSize">The current warp size.</param>
        /// <returns>Load and store operations for the given basic value type.</returns>
        public (MethodInfo Load, MethodInfo Store) GetIOOperation(
            BasicValueType basicValueType,
            int warpSize) =>
            basicValueType.IsTreatedAs32Bit()
                ? GetIOOperation32(basicValueType, warpSize)
                : GetIOOperation64(basicValueType, warpSize);

        /// <summary>
        /// Creates code to load primitive values and pointers from memory while using
        /// the given mask to differentiate between active and inactive lanes.
        /// </summary>
        private void CreateNonStructureLoad<TILEmitter>(
            in TILEmitter emitter,
            ILLocal mask,
            TypeNode typeNode,
            int warpSize)
            where TILEmitter : struct, IILEmitter
        {
            emitter.Emit(LocalOperation.Load, mask);

            switch (typeNode)
            {
                case PrimitiveType primitiveType:
                    var basicValueType = primitiveType.BasicValueType;
                    var operations = GetIOOperation(basicValueType, warpSize);
                    emitter.EmitCall(operations.Load);
                    break;
                case PointerType _:
                    emitter.EmitCall(GetIOOperation64(
                        BasicValueType.Int64,
                        warpSize).Load);
                    break;
                default:
                    throw typeNode.GetNotSupportedException(
                        ErrorMessages.NotSupportedType);
            }
        }

        /// <summary>
        /// Creates a sequence of load instructions to load a vectorized value via
        /// specialized IO operations.
        /// </summary>
        public void CreateLoad<TILEmitter>(
            in TILEmitter emitter,
            ILLocal mask,
            ILLocal source,
            TypeNode typeNode,
            VelocityTypeGenerator typeGenerator)
            where TILEmitter : struct, IILEmitter
        {
            if (typeNode is StructureType structureType)
            {
                // Allocate a new temporary allocation to fill all fields
                var vectorizedType = typeGenerator.GetVectorizedType(structureType);
                var temporary = emitter.LoadNull(vectorizedType);

                // Fill the temporary structure instance with values
                foreach (var (fieldType, fieldAccess) in structureType)
                {
                    // Load the variable address
                    emitter.Emit(LocalOperation.LoadAddress, temporary);

                    // Adjust the actual source address based on offsets in the type
                    // definition
                    // Adjust the target offset
                    long fieldOffset = structureType.GetOffset(fieldAccess);
                    emitter.EmitConstant(fieldOffset);
                    emitter.EmitCall(GetConstValueOperation64(
                        VelocityWarpOperationMode.I));
                    emitter.Emit(LocalOperation.Load, source);
                    emitter.EmitCall(GetBinaryOperation64(
                        BinaryArithmeticKind.Add,
                        VelocityWarpOperationMode.U));

                    // Load the converted field type
                    CreateNonStructureLoad(
                        emitter,
                        mask,
                        fieldType,
                        typeGenerator.WarpSize);

                    // Store it into out structure field
                    emitter.StoreField(vectorizedType, fieldAccess.Index);
                }

                // Load local variable onto the stack containing all required information
                emitter.Emit(LocalOperation.Load, temporary);
            }
            else
            {
                // Load the type directly
                emitter.Emit(LocalOperation.Load, source);
                CreateNonStructureLoad(
                    emitter,
                    mask,
                    typeNode,
                    typeGenerator.WarpSize);
            }
        }

        /// <summary>
        /// Calls a typed method that is able to reinterpret the given value.
        /// </summary>
        public void CallMemoryBarrier<TILEmitter>(in TILEmitter emitter)
            where TILEmitter : struct, IILEmitter =>
            emitter.EmitCall(memoryBarrierMethod);

        /// <summary>
        /// Calls a method that triggers an assertion check.
        /// </summary>
        public void CallAssert<TILEmitter>(in TILEmitter emitter)
            where TILEmitter : struct, IILEmitter =>
            emitter.EmitCall(assertMethod);

        public void CallInspect<TILEmitter>(in TILEmitter emitter, Type type)
            where TILEmitter : struct, IILEmitter =>
            emitter.EmitCall(inspectValueMethod.MakeGenericMethod(new Type[] { type }));

        public void CallDump<TILEmitter>(
            in TILEmitter emitter,
            bool is32Bit,
            VelocityWarpOperationMode mode)
            where TILEmitter : struct, IILEmitter
        {
            emitter.Emit(OpCodes.Dup);
            if (is32Bit)
                emitter.EmitCall(dump32Method);
            else
                emitter.EmitCall(dump64Method);
        }

        #endregion
    }

    static class VelocityInstructionsHelpers
    {
        /// <summary>
        /// Returns true if the given value type is actually a 32bit value type.
        /// </summary>
        public static bool Is32Bit(this BasicValueType valueType) =>
            valueType switch
            {
                BasicValueType.Int32 => true,
                BasicValueType.Float32 => true,
                _ => false,
            };

        /// <summary>
        /// Returns true if the given value type is interpreted as a 32bit value type.
        /// </summary>
        public static bool IsTreatedAs32Bit(this BasicValueType valueType) =>
            valueType switch
            {
                BasicValueType.Float64 => false,
                BasicValueType.Int64 => false,
                _ => true,
            };

        /// <summary>
        /// Returns true if the given value is interpreted as a 32bit value type.
        /// </summary>
        public static bool IsTreatedAs32Bit(this Value value) =>
            value.BasicValueType.IsTreatedAs32Bit();

        /// <summary>
        /// Returns true if the given value is interpreted as a 32bit value type.
        /// </summary>
        public static bool IsTreatedAs32Bit(this ArithmeticValue value) =>
            value.ArithmeticBasicValueType switch
            {
                ArithmeticBasicValueType.Float64 => false,
                ArithmeticBasicValueType.Int64 => false,
                ArithmeticBasicValueType.UInt64 => false,
                _ => true,
            };

        /// <summary>
        /// Determines the current warp-operation mode for the given arithmetic basic
        /// value type.
        /// </summary>
        public static VelocityWarpOperationMode GetWarpMode(
            this ArithmeticBasicValueType valueType) =>
            valueType switch
            {
                ArithmeticBasicValueType.UInt1 => VelocityWarpOperationMode.U,
                ArithmeticBasicValueType.UInt8 => VelocityWarpOperationMode.U,
                ArithmeticBasicValueType.UInt16 => VelocityWarpOperationMode.U,
                ArithmeticBasicValueType.UInt32 => VelocityWarpOperationMode.U,
                ArithmeticBasicValueType.UInt64 => VelocityWarpOperationMode.U,

                ArithmeticBasicValueType.Int8 => VelocityWarpOperationMode.I,
                ArithmeticBasicValueType.Int16 => VelocityWarpOperationMode.I,
                ArithmeticBasicValueType.Int32 => VelocityWarpOperationMode.I,
                ArithmeticBasicValueType.Int64 => VelocityWarpOperationMode.I,

                ArithmeticBasicValueType.Float16 => VelocityWarpOperationMode.F,
                ArithmeticBasicValueType.Float32 => VelocityWarpOperationMode.F,
                ArithmeticBasicValueType.Float64 => VelocityWarpOperationMode.D,
                _ => throw new NotSupportedException()
            };

        /// <summary>
        /// Determines the current warp-operation mode for the given value.
        /// </summary>
        public static VelocityWarpOperationMode GetWarpMode(this ArithmeticValue value) =>
            value.ArithmeticBasicValueType.GetWarpMode();

        /// <summary>
        /// Determines the current warp-operation mode for the given value.
        /// </summary>
        public static VelocityWarpOperationMode GetWarpMode(this CompareValue value) =>
            value.CompareType.GetWarpMode();
    }
}

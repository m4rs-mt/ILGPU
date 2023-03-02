// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityCodeGenerator.Values.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime.Velocity;
using ILGPU.Util;
using System;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    partial class VelocityCodeGenerator<TILEmitter, TVerifier>
    {
        /// <inheritdoc/>
        public void GenerateCode(MethodCall methodCall)
        {
            // Load the current execution mask
            Emitter.Emit(LocalOperation.Load, blockMasks[methodCall.BasicBlock]);

            // Load all arguments onto the evaluation stack
            foreach (Value arg in methodCall)
                Load(arg);

            // Call the module method
            var method = Module[methodCall.Target];
            Emitter.EmitCall(method);

            if (!methodCall.Target.IsVoid)
                Store(methodCall);
        }

        /// <inheritdoc/>
        public void GenerateCode(Parameter parameter)
        {
            // Parameters have been bound in the beginning and do not need to be
            // processed here
        }

        /// <inheritdoc/>
        public void GenerateCode(PhiValue phiValue)
        {
            // Phi values need to be allocated in the beginning and do not need to be
            // handled here
        }

        /// <inheritdoc/>
        public void GenerateCode(UnaryArithmeticValue value)
        {
            Load(value.Value);

            // Determine the current warp mode and its bitness
            var warpMode = value.GetWarpMode();
            var method = value.IsTreatedAs32Bit()
                ? Instructions.GetUnaryOperation32(value.Kind, warpMode)
                : Instructions.GetUnaryOperation64(value.Kind, warpMode);
            Emitter.EmitCall(method);
            Store(value);
        }

        /// <inheritdoc/>
        public void GenerateCode(BinaryArithmeticValue value)
        {
            Load(value.Left);
            Load(value.Right);

            // Check for operation types
            switch (value.Kind)
            {
                case BinaryArithmeticKind.Shl:
                case BinaryArithmeticKind.Shr:
                    // We need to convert the rhs operations to int64
                    if (!value.IsTreatedAs32Bit())
                    {
                        Emitter.EmitCall(Instructions.GetConvertWidenOperation32(
                            VelocityWarpOperationMode.I));
                    }
                    break;
            }

            // Determine the current warp mode and its bitness
            var warpMode = value.GetWarpMode();
            var method = value.IsTreatedAs32Bit()
                ? Instructions.GetBinaryOperation32(value.Kind, warpMode)
                : Instructions.GetBinaryOperation64(value.Kind, warpMode);
            Emitter.EmitCall(method);
            Store(value);
        }

        /// <inheritdoc/>
        public void GenerateCode(TernaryArithmeticValue value)
        {
            Load(value.First);
            Load(value.Second);
            Load(value.Third);

            // Determine the current warp mode and its bitness
            var warpMode = value.GetWarpMode();
            var method = value.IsTreatedAs32Bit()
                ? Instructions.GetTernaryOperation32(value.Kind, warpMode)
                : Instructions.GetTernaryOperation64(value.Kind, warpMode);
            Emitter.EmitCall(method);
            Store(value);
        }

        /// <inheritdoc/>
        public void GenerateCode(CompareValue value)
        {
            Load(value.Left);
            Load(value.Right);

            // Determine the current warp mode and its bitness
            var warpMode = value.GetWarpMode();
            var method = value.CompareType.GetBasicValueType().IsTreatedAs32Bit()
                ? Instructions.GetCompareOperation32(value.Kind, warpMode)
                : Instructions.GetCompareOperation64(value.Kind, warpMode);
            Emitter.EmitCall(method);
            Store(value);
        }

        /// <inheritdoc/>
        public void GenerateCode(ConvertValue value)
        {
            // Check to which value we have to convert the current value
            var sourceMode = value.SourceType.GetWarpMode();
            var targetMode = value.TargetType.GetWarpMode();

            // Load source
            Load(value.Value);

            // Check whether have to expand or to narrow the current values on the stack
            var sourceBasicValueType = value.SourceType.GetBasicValueType();
            bool sourceIs32Bit = sourceBasicValueType.IsTreatedAs32Bit();
            bool targetIs32Bit = value.IsTreatedAs32Bit();

            if (sourceIs32Bit)
            {
                // The source value lives in the 32bit warp world

                // Check whether we have to widen first
                if (targetIs32Bit)
                {
                    // Use the local conversion functionality
                    Emitter.EmitCall(Instructions.GetSoftwareConvertOperation32(
                        value.SourceType,
                        value.TargetType,
                        WarpSize));
                }
                else
                {
                    // Use the local conversion mechanism in 32bit mode
                    ArithmeticBasicValueType targetType32;
                    if (sourceBasicValueType.IsFloat())
                    {
                        // Ensure 32bit float compatibility
                        targetType32 = ArithmeticBasicValueType.Float32;
                    }
                    else
                    {
                        // Extent types to 32bit only while preserving the sign
                        targetType32 = value.TargetType.ForceTo32Bit();
                        if (targetType32.IsFloat())
                        {
                            targetType32 = value.IsSourceUnsigned
                                ? ArithmeticBasicValueType.UInt32
                                : ArithmeticBasicValueType.Int32;
                        }
                    }
                    Emitter.EmitCall(Instructions.GetSoftwareConvertOperation32(
                        value.SourceType,
                        targetType32,
                        WarpSize));

                    // Widen first
                    Emitter.EmitCall(Instructions.GetConvertWidenOperation32(
                        sourceMode));

                    // Ensure valid data types in 64bit world
                    Emitter.EmitCall(Instructions.GetAcceleratedConvertOperation64(
                        sourceMode,
                        targetMode));
                }
            }
            else
            {
                // The source value lives in the 64bit warp world

                // Convert the values according to the 64bit type information
                Emitter.EmitCall(Instructions.GetAcceleratedConvertOperation64(
                    sourceMode,
                    targetMode));

                // We have to enter the 32bit world
                if (targetIs32Bit)
                {
                    // Narrow to 32bit world
                    Emitter.EmitCall(Instructions.GetConvertNarrowOperation64(
                        targetMode));

                    // Convert the remaining parts
                    Emitter.EmitCall(Instructions.GetSoftwareConvertOperation32(
                        value.TargetType.ForceTo32Bit(),
                        value.TargetType,
                        WarpSize));
                }
            }

            Store(value);
        }

        /// <inheritdoc/>
        public void GenerateCode(FloatAsIntCast value)
        {
            // Do nothing as this does not change any register contents
            var valueLocal = GetLocal(value.Value);
            Alias(value, valueLocal);
        }

        /// <inheritdoc/>
        public void GenerateCode(IntAsFloatCast value)
        {
            // Do nothing as this does not change any register contents
            var valueLocal = GetLocal(value.Value);
            Alias(value, valueLocal);
        }

        /// <summary>
        /// Emits a new merge operation working on arbitrary values.
        /// </summary>
        private ILLocal? EmitMerge(
            Value value,
            Func<Type> loadLeft,
            Func<Type> loadRight,
            Action loadCondition,
            Action merge32,
            Action merge64,
            Func<Type, ILLocal> getTempLocal)
        {
            // Merges values based on predicate masks
            void MergeLocal(BasicValueType basicValueType)
            {
                loadCondition();
                if (basicValueType.IsTreatedAs32Bit())
                {
                    // Merge 32bit values
                    merge32();
                }
                else
                {
                    // Expand condition to 64bit vector if required
                    Emitter.EmitCall(Instructions.GetConvertWidenOperation32(
                        VelocityWarpOperationMode.I));

                    // Merge 64bit values
                    merge64();
                }
            }

            // Merge the actual values from all lanes
            if (value.Type is StructureType structureType)
            {
                var targetType = TypeGenerator.GetVectorizedType(structureType);
                var target = getTempLocal(targetType);

                // Iterate over all field elements
                foreach (var (fieldType, fieldAccess) in structureType)
                {
                    // Load arguments
                    Emitter.Emit(LocalOperation.LoadAddress, target);

                    var leftType = loadLeft();
                    Emitter.LoadField(leftType, fieldAccess.Index);
                    var rightType = loadRight();
                    Emitter.LoadField(rightType, fieldAccess.Index);

                    // Merge
                    MergeLocal(fieldType.BasicValueType);

                    // Store field
                    Emitter.StoreField(targetType, fieldAccess.Index);
                }

                return target;
            }
            else
            {
                // A direct merge is possible
                loadLeft();
                loadRight();
                MergeLocal(value.BasicValueType);
                return null;
            }
        }

        /// <inheritdoc/>
        public void GenerateCode(Predicate predicate)
        {
            // Load true and false values in reverse order to match API spec
            var falseLocal = GetLocal(predicate.FalseValue);
            var trueLocal = GetLocal(predicate.TrueValue);

            // Emit the merge
            var local = EmitMerge(predicate,
                () =>
                {
                    Emitter.Emit(LocalOperation.Load, falseLocal);
                    return falseLocal.VariableType;
                },
                () =>
                {
                    Emitter.Emit(LocalOperation.Load, trueLocal);
                    return trueLocal.VariableType;
                },
                () => Load(predicate.Condition),
                () => Emitter.EmitCall(Instructions.MergeOperation32),
                () => Emitter.EmitCall(Instructions.MergeOperation64),
                type => Emitter.DeclareLocal(type));

            // Bind value result
            if (local.HasValue)
                Alias(predicate, local.Value);
            else
                Store(predicate);
        }

        /// <inheritdoc/>
        public void GenerateCode(Alloca alloca)
        {
            // All allocations have already been processed in the beginning.
        }

        /// <inheritdoc/>
        public void GenerateCode(MemoryBarrier barrier) =>
            Instructions.CallMemoryBarrier(Emitter);

        /// <inheritdoc/>
        public void GenerateCode(PrimitiveValue value)
        {
            switch (value.BasicValueType)
            {
                case BasicValueType.Int1:
                    Emitter.Emit(value.Int1Value ? OpCodes.Ldc_I4_M1 : OpCodes.Ldc_I4_0);
                    Emitter.EmitCall(
                        Instructions.GetConstValueOperation32(
                            VelocityWarpOperationMode.U));
                    break;
                case BasicValueType.Int8:
                    Emitter.LoadIntegerConstant(value.Int8Value);
                    Emitter.EmitCall(
                        Instructions.GetConstValueOperation32(
                            VelocityWarpOperationMode.U));
                    break;
                case BasicValueType.Int16:
                    Emitter.LoadIntegerConstant(value.Int16Value);
                    Emitter.EmitCall(
                        Instructions.GetConstValueOperation32(
                            VelocityWarpOperationMode.U));
                    break;
                case BasicValueType.Int32:
                    Emitter.LoadIntegerConstant(value.Int32Value);
                    Emitter.EmitCall(
                        Instructions.GetConstValueOperation32(
                            VelocityWarpOperationMode.U));
                    break;
                case BasicValueType.Int64:
                    Emitter.EmitConstant(value.Int64Value);
                    Emitter.EmitCall(
                        Instructions.GetConstValueOperation64(
                            VelocityWarpOperationMode.U));
                    break;
                case BasicValueType.Float16:
                    Emitter.EmitConstant(value.Float16Value.RawValue);
                    Emitter.EmitCall(
                        Instructions.GetConstValueOperation32(
                            VelocityWarpOperationMode.U));
                    break;
                case BasicValueType.Float32:
                    Emitter.EmitConstant(value.Float32Value);
                    Emitter.EmitCall(
                        Instructions.GetConstValueOperation32(
                            VelocityWarpOperationMode.F));
                    break;
                case BasicValueType.Float64:
                    Emitter.EmitConstant(value.Float64Value);
                    Emitter.EmitCall(
                        Instructions.GetConstValueOperation64(
                            VelocityWarpOperationMode.F));
                    break;
                default:
                    throw new NotSupportedIntrinsicException(
                        value.BasicValueType.ToString());
            }
            Store(value);
        }

        /// <inheritdoc/>
        public void GenerateCode(StringValue value)
        {
            Emitter.EmitConstant(value.String);
            Store(value);
        }

        /// <inheritdoc/>
        public void GenerateCode(NullValue value)
        {
            // Check whether we have already loaded a null value
            if (!nullLocals.TryGetValue(value.Type, out var local))
            {
                // If not... load the value
                local = Emitter.LoadNull(GetVectorizedType(value.Type));
                nullLocals.Add(value.Type, local);
            }
            Alias(value, local);
        }

        /// <inheritdoc/>
        public void GenerateCode(StructureValue value)
        {
            // Generate a local variable that contains the type
            var managedType = GetVectorizedType(value.Type);
            var local = Emitter.LoadNull(managedType);

            // Insert all fields
            for (int i = 0, e = value.Count; i < e; ++i)
            {
                Emitter.Emit(LocalOperation.LoadAddress, local);
                Load(value[i]);
                Emitter.StoreField(managedType, i);
            }

            Alias(value, local);
        }

        /// <inheritdoc/>
        public void GenerateCode(GetField value)
        {
            // Check the result type of the operation
            if (!value.FieldSpan.HasSpan)
            {
                // Extract the primitive value from the structure
                LoadRefAndType(value.ObjectValue, out var objectType);
                Emitter.LoadField(objectType, value.FieldSpan.Index);

                // Store field value
                Store(value);
            }
            else
            {
                // The result is a new structure value
                var newObjectType = GetVectorizedType(value.Type);
                var local = Emitter.DeclareLocal(newObjectType);
                // Extract all fields from the structure
                int span = value.FieldSpan.Span;
                for (int i = 0; i < span; ++i)
                {
                    Emitter.Emit(LocalOperation.LoadAddress, local);

                    LoadRefAndType(value.ObjectValue, out var objectType);
                    Emitter.LoadField(
                        objectType,
                        i + value.FieldSpan.Index);

                    Emitter.StoreField(newObjectType, i);
                }

                // Bind the current value
                Alias(value, local);
            }
        }

        /// <inheritdoc/>
        public void GenerateCode(SetField value)
        {
            var mask = GetBlockMask(value.BasicBlock);

            // The result operation will be another structure instance
            LoadVectorized(value.ObjectValue, out var type);
            // var vectorized = GetVectorizedType(value.ObjectValue.Type);
            var local = Emitter.DeclareLocal(type);

            // Copy object instance
            Emitter.Emit(LocalOperation.Store, local);

            var structureType = value.ObjectValue.Type.As<StructureType>(value);
            for (int i = 0, e = value.FieldSpan.Span; i < e; ++i)
            {
                int fieldOffset = value.FieldSpan.Index + i;

                // Load the source value
                Emitter.Emit(LocalOperation.LoadAddress, local);
                Emitter.Emit(OpCodes.Dup);
                Emitter.LoadField(type, fieldOffset);

                // Load the target value to store
                if (e > 1)
                {
                    LoadRef(value.Value);
                    Emitter.LoadField(type, i);
                }
                else
                {
                    // Load the whole value
                    Load(value.Value);
                }

                // Load the mask
                Emitter.Emit(LocalOperation.Load, mask);

                // Merge data
                var mergeMode = structureType[i].BasicValueType.IsTreatedAs32Bit()
                    ? Instructions.MergeWithMaskOperation32
                    : Instructions.MergeWithMaskOperation64;
                Emitter.EmitCall(mergeMode);

                // Store merged value
                Emitter.StoreField(type, fieldOffset);
            }

            Alias(value, local);
        }

        /// <inheritdoc/>
        public void GenerateCode(DebugAssertOperation debug)
        {
            // If the mask is active emit a failed debug assertion
            var blockMask = GetBlockMask(debug.BasicBlock);
            Emitter.Emit(LocalOperation.Load, blockMask);

            // Load the debug condition
            Load(debug.Condition);

            // Load the debug error message
            string errorMessage = debug.Message.Resolve() is StringValue stringValue
                ? debug.Location.FormatErrorMessage(stringValue.String)
                : "Assertion failed";
            Emitter.EmitConstant(errorMessage);

            // Call our assertion method
            Instructions.CallAssert(Emitter);
        }

        /// <inheritdoc/>
        public void GenerateCode(WriteToOutput output) =>
            throw new NotSupportedIntrinsicException();
    }
}

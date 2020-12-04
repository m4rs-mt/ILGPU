// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLCodeGenerator.Values.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Runtime.OpenCL;
using ILGPU.Util;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.OpenCL
{
    partial class CLCodeGenerator
    {
        /// <summary cref="IBackendCodeGenerator.GenerateCode(MethodCall)"/>
        public void GenerateCode(MethodCall methodCall)
        {
            var target = methodCall.Target;
            var returnType = target.ReturnType;

            StatementEmitter statementEmitter;
            if (!returnType.IsVoidType)
            {
                var returnValue = Allocate(methodCall);
                statementEmitter = BeginStatement(returnValue);
                statementEmitter.AppendCommand(GetMethodName(target));
            }
            else
            {
                statementEmitter = BeginStatement(GetMethodName(target));
            }

            // Append arguments
            statementEmitter.BeginArguments();
            foreach (var argument in methodCall)
            {
                var variable = Load(argument);
                statementEmitter.AppendArgument(variable);
            }
            statementEmitter.EndArguments();

            // End call
            statementEmitter.Finish();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Parameter)"/>
        public void GenerateCode(Parameter parameter)
        {
            // Parameters are already assigned to variables
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(PhiValue)"/>
        public void GenerateCode(PhiValue phiValue)
        {
            // Phi values are already assigned to variables
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(UnaryArithmeticValue)"/>
        public void GenerateCode(UnaryArithmeticValue value)
        {
            var argument = Load(value.Value);
            var target = Allocate(value, value.ArithmeticBasicValueType);

            using var statement = BeginStatement(target);
            statement.AppendCast(value.ArithmeticBasicValueType);
            var operation = CLInstructions.GetArithmeticOperation(
                value.Kind,
                value.ArithmeticBasicValueType,
                out bool isFunction);

            if (isFunction)
                statement.AppendCommand(operation);
            statement.BeginArguments();
            if (!isFunction)
                statement.AppendCommand(operation);

            statement.AppendCast(value.ArithmeticBasicValueType);
            statement.AppendArgument(argument);
            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(BinaryArithmeticValue)"/>
        public void GenerateCode(BinaryArithmeticValue value)
        {
            var left = Load(value.Left);
            var right = Load(value.Right);

            var target = Allocate(value, value.ArithmeticBasicValueType);
            using var statement = BeginStatement(target);
            statement.AppendCast(value.ArithmeticBasicValueType);
            var operation = CLInstructions.GetArithmeticOperation(
                value.Kind,
                value.BasicValueType.IsFloat(),
                out bool isFunction);

            if (isFunction)
            {
                statement.AppendCommand(operation);
                statement.BeginArguments();
            }
            else
            {
                statement.OpenParen();
            }

            statement.AppendCast(value.ArithmeticBasicValueType);
            statement.AppendArgument(left);

            if (!isFunction)
                statement.AppendCommand(operation);

            statement.AppendArgument();
            statement.AppendCast(value.ArithmeticBasicValueType);
            statement.Append(right);

            if (isFunction)
                statement.EndArguments();
            else
                statement.CloseParen();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(TernaryArithmeticValue)"/>
        public void GenerateCode(TernaryArithmeticValue value)
        {
            if (!CLInstructions.TryGetArithmeticOperation(
                value.Kind,
                value.BasicValueType.IsFloat(),
                out string operation))
            {
                throw new InvalidCodeGenerationException();
            }

            var first = Load(value.First);
            var second = Load(value.Second);
            var third = Load(value.Third);

            var target = Allocate(value, value.ArithmeticBasicValueType);
            using var statement = BeginStatement(target);
            statement.AppendCast(value.ArithmeticBasicValueType);
            statement.AppendCommand(operation);
            statement.BeginArguments();

            statement.AppendArgument();
            statement.AppendCast(value.ArithmeticBasicValueType);
            statement.Append(first);

            statement.AppendArgument();
            statement.AppendCast(value.ArithmeticBasicValueType);
            statement.Append(second);

            statement.AppendArgument();
            statement.AppendCast(value.ArithmeticBasicValueType);
            statement.Append(third);

            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(CompareValue)"/>
        public void GenerateCode(CompareValue value)
        {
            var left = Load(value.Left);
            var right = Load(value.Right);

            var target = Allocate(value);
            using var statement = BeginStatement(target);
            statement.AppendCast(value.CompareType);
            statement.AppendArgument(left);
            statement.AppendCommand(
                CLInstructions.GetCompareOperation(
                    value.Kind));
            statement.AppendCast(value.CompareType);
            statement.AppendArgument(right);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(ConvertValue)"/>
        public void GenerateCode(ConvertValue value)
        {
            var sourceValue = Load(value.Value);

            var target = Allocate(value, value.TargetType);
            using var statement = BeginStatement(target);
            statement.AppendCast(value.TargetType);
            statement.AppendCast(value.SourceType);
            statement.AppendArgument(sourceValue);
        }

        /// <summary>
        /// Generates code for the given cast value.
        /// </summary>
        /// <param name="cast">The cast value to generte code for.</param>
        private void GenerateCodeForCast(CastValue cast)
        {
            var sourceValue = Load(cast.Value);

            var target = Allocate(cast);
            using var statement = BeginStatement(target);
            statement.AppendCast(cast.TargetType);
            statement.AppendArgument(sourceValue);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(IntAsPointerCast)"/>
        public void GenerateCode(IntAsPointerCast cast) => GenerateCodeForCast(cast);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(IntAsPointerCast)"/>
        public void GenerateCode(PointerAsIntCast cast) => GenerateCodeForCast(cast);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(PointerCast)"/>
        public void GenerateCode(PointerCast value) => GenerateCodeForCast(value);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(FloatAsIntCast)"/>
        public void GenerateCode(FloatAsIntCast value)
        {
            var source = Load(value.Value);
            var target = Allocate(value);

            using var statement = BeginStatement(target);
            statement.AppendCommand(
                value.BasicValueType == BasicValueType.Int64 ?
                CLInstructions.DoubleAsLong :
                CLInstructions.FloatAsInt);
            statement.BeginArguments();
            statement.AppendArgument(source);
            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(IntAsFloatCast)"/>
        public void GenerateCode(IntAsFloatCast value)
        {
            var source = Load(value.Value);
            var target = Allocate(value);

            using var statement = BeginStatement(target);
            statement.AppendCommand(
                value.BasicValueType == BasicValueType.Float64 ?
                CLInstructions.LongAsDouble :
                CLInstructions.IntAsFloat);
            statement.BeginArguments();
            statement.AppendArgument(source);
            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Predicate)"/>
        public void GenerateCode(Predicate predicate)
        {
            var condition = Load(predicate.Condition);
            var trueValue = Load(predicate.TrueValue);
            var falseValue = Load(predicate.FalseValue);

            var target = Allocate(predicate);
            using var statement = BeginStatement(target);
            statement.AppendArgument(condition);
            statement.AppendCommand(CLInstructions.SelectOperation1);
            statement.AppendArgument(trueValue);
            statement.AppendCommand(CLInstructions.SelectOperation2);
            statement.AppendArgument(falseValue);
        }

        /// <summary>
        /// Throws an exception if the supplied atomic operation is not supported
        /// by the capabilities of the accelerator.
        /// </summary>
        /// <param name="atomic">The atomic operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfUnsupportedAtomicOperation(AtomicValue atomic)
        {
            if ((atomic.ArithmeticBasicValueType == ArithmeticBasicValueType.Int64 ||
                atomic.ArithmeticBasicValueType == ArithmeticBasicValueType.UInt64 ||
                atomic.ArithmeticBasicValueType == ArithmeticBasicValueType.Float64) &&
                !TypeGenerator.Capabilities.Int64_Atomics)
            {
                throw CLCapabilityContext.GetNotSupportedInt64_AtomicsException();
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GenericAtomic)"/>
        public void GenerateCode(GenericAtomic atomic)
        {
            ThrowIfUnsupportedAtomicOperation(atomic);

            var target = Load(atomic.Target);
            var value = Load(atomic.Value);
            var result = Allocate(atomic);

            var atomicOperation = CLInstructions.GetAtomicOperation(atomic.Kind);
            using var statement = BeginStatement(result, atomicOperation);
            statement.BeginArguments();
            statement.AppendAtomicCast(atomic.ArithmeticBasicValueType);
            statement.AppendArgument(target);
            statement.AppendArgument();
            statement.AppendCast(atomic.ArithmeticBasicValueType);
            statement.Append(value);
            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(AtomicCAS)"/>
        public void GenerateCode(AtomicCAS atomicCAS)
        {
            ThrowIfUnsupportedAtomicOperation(atomicCAS);

            var target = Load(atomicCAS.Target);
            var value = Load(atomicCAS.Value);
            var compare = Load(atomicCAS.CompareValue);

            // The internal AtomicCAS value "returns" the old value that was stored
            // at the memory location. If the emitted operation fails the comparison
            // check, we will "return" the updated value stored in "targetVariable". If
            // the operation succeeds we will return the old value stored in
            // "targetVariable". Consequently, we will always assign the value stored in
            // "targetVariable" the be the "result" of the computation.
            var targetVariable = Allocate(atomicCAS);

            // Copy the compare value into the target variable to avoid modifications of
            // the input value
            using (var statement = BeginStatement(targetVariable))
                statement.Append(value);

            // Perform the atomic operation and ignore the resulting bool value
            using (var statement = BeginStatement(CLInstructions.AtomicCASOperation))
            {
                statement.BeginArguments();
                statement.AppendAtomicCast(atomicCAS.ArithmeticBasicValueType);
                statement.AppendArgument(target);
                statement.AppendArgumentAddressWithCast(
                    targetVariable,
                    atomicCAS.ArithmeticBasicValueType);
                statement.AppendArgumentWithCast(
                    compare,
                    atomicCAS.ArithmeticBasicValueType);
                statement.EndArguments();
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Alloca)"/>
        public void GenerateCode(Alloca alloca)
        {
            // Ignore alloca
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(MemoryBarrier)"/>
        public void GenerateCode(MemoryBarrier barrier)
        {
            var fenceFlags = CLInstructions.GetMemoryFenceFlags(true);
            var command = CLInstructions.GetMemoryBarrier(
                barrier.Kind,
                out string memoryScope);
            using var statement = BeginStatement(command);
            statement.BeginArguments();

            statement.AppendArgument();
            statement.AppendCommand(fenceFlags);

            statement.AppendArgument();
            statement.AppendCommand(memoryScope);

            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Load)"/>
        public void GenerateCode(Load load)
        {
            var address = Load(load.Source);
            var target = Allocate(load);

            using var statement = BeginStatement(target);
            statement.AppendCommand(CLInstructions.DereferenceOperation);
            statement.AppendArgument(address);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Store)"/>
        public void GenerateCode(Store store)
        {
            var address = Load(store.Target);
            var value = Load(store.Value);

            using var statement = BeginStatement(CLInstructions.DereferenceOperation);
            statement.AppendArgument(address);
            statement.AppendCommand(CLInstructions.AssignmentOperation);
            statement.AppendArgument(value);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(LoadFieldAddress)"/>
        public void GenerateCode(LoadFieldAddress value)
        {
            var source = Load(value.Source);
            var target = Allocate(value);

            using var statement = BeginStatement(target);
            statement.AppendCommand(CLInstructions.AddressOfOperation);
            statement.AppendArgument(source);
            statement.AppendFieldViaPtr(value.FieldSpan.Access);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(PrimitiveValue)"/>
        public void GenerateCode(PrimitiveValue value) =>
            Allocate(value);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(StringValue)"/>
        public void GenerateCode(StringValue value)
        {
            var target = Allocate(value);
            using var statement = BeginStatement(target);
            statement.AppendConstant(value.String);
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(NullValue)"/>
        public void GenerateCode(NullValue value)
        {
            if (value.Type.IsVoidType)
                return;
            var target = Allocate(value);
            if (value.Type is StructureType structureType)
            {
                Declare(target);
                for (int i = 0, e = structureType.NumFields; i < e; ++i)
                {
                    using var statement = BeginStatement(target, i);
                    statement.AppendCast(structureType[i]);
                    statement.AppendConstant(0);
                }
            }
            else
            {
                using var statement = BeginStatement(target);
                statement.AppendCast(value.Type);
                statement.AppendConstant(0);
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(StructureValue)"/>
        public void GenerateCode(StructureValue value)
        {
            var target = Allocate(value);
            Declare(target);
            for (int i = 0, e = value.Count; i < e; ++i)
            {
                using var statement = BeginStatement(target, i);
                statement.AppendArgument(Load(value[i]));
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GetField)"/>
        public void GenerateCode(GetField value)
        {
            var source = Load(value.ObjectValue);
            var target = Allocate(value);

            var span = value.FieldSpan;
            if (!span.HasSpan)
            {
                // Extract primitive value from the given target
                using var statement = BeginStatement(target);
                statement.AppendArgument(source);
                statement.AppendField(span.Access);
            }
            else
            {
                // Result is a structure type
                Declare(target);
                for (int i = 0; i < span.Span; ++i)
                {
                    using var statement = BeginStatement(target, i);
                    statement.AppendArgument(source);
                    statement.AppendField(span.Access.Add(i));
                }
            }
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(SetField)"/>
        public void GenerateCode(SetField value)
        {
            var source = Load(value.ObjectValue);
            var set = Load(value.Value);
            var target = Allocate(value);

            // Copy value
            using (var statement = BeginStatement(target))
                statement.AppendArgument(source);

            var span = value.FieldSpan;
            if (!span.HasSpan)
            {
                // Update field value
                using var statement = BeginStatement(target, span.Access);
                statement.AppendArgument(set);
            }
            else
            {
                // Update field values
                for (int i = 0; i < span.Span; ++i)
                {
                    var targetAccess = span.Access.Add(i);
                    using var statement = BeginStatement(target, targetAccess);
                    statement.AppendArgument(set);
                    statement.AppendField(new FieldAccess(i));
                }
            }
        }

        private void MakeIntrinsicValue(
            Value value,
            string operation,
            string args = null)
        {
            var target = Allocate(value);
            using var statement = BeginStatement(target);
            statement.AppendCommand(operation);
            if (args != null)
            {
                statement.BeginArguments();
                statement.AppendCommand(args);
                statement.EndArguments();
            }
        }

        private void MakeIntrinsicValue(
            Value value,
            string operation,
            DeviceConstantDimension3D dimension) =>
            MakeIntrinsicValue(
                value,
                operation,
                ((int)dimension).ToString());

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GridIndexValue)"/>
        public void GenerateCode(GridIndexValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetGridIndex,
                value.Dimension);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GroupIndexValue)"/>
        public void GenerateCode(GroupIndexValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetGroupIndex,
                value.Dimension);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GridDimensionValue)"/>
        public void GenerateCode(GridDimensionValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetGridSize,
                value.Dimension);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(GroupDimensionValue)"/>
        public void GenerateCode(GroupDimensionValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetGroupSize,
                value.Dimension);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(WarpSizeValue)"/>
        public void GenerateCode(WarpSizeValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetWarpSize);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(LaneIdxValue)"/>
        public void GenerateCode(LaneIdxValue value) =>
            MakeIntrinsicValue(
                value,
                CLInstructions.GetLaneIndexOperation);

        /// <summary cref="IBackendCodeGenerator.GenerateCode(PredicateBarrier)"/>
        public void GenerateCode(PredicateBarrier barrier)
        {
            var sourcePredicate = Load(barrier.Predicate);
            var target = Allocate(barrier);

            if (!CLInstructions.TryGetPredicateBarrier(
                barrier.Kind,
                out string operation))
            {
                throw new InvalidCodeGenerationException();
            }

            using var statement = BeginStatement(target);
            statement.AppendCast(BasicValueType.Int1);
            statement.AppendCommand(operation);
            statement.BeginArguments();
            statement.AppendCast(BasicValueType.Int32);
            statement.AppendArgument(sourcePredicate);
            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Barrier)"/>
        public void GenerateCode(Barrier barrier)
        {
            using var statement = BeginStatement(
                CLInstructions.GetBarrier(barrier.Kind));
            statement.BeginArguments();
            statement.AppendCommand(
                CLInstructions.GetMemoryFenceFlags(true));
            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(Broadcast)"/>
        public void GenerateCode(Broadcast broadcast)
        {
            var source = Load(broadcast.Variable);
            var origin = Load(broadcast.Origin);
            var target = Allocate(broadcast);

            using var statement = BeginStatement(target);
            statement.AppendCommand(
                CLInstructions.GetBroadcastOperation(
                broadcast.Kind));
            statement.BeginArguments();
            statement.AppendArgument(source);
            statement.AppendArgument(origin);
            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(WarpShuffle)"/>
        public void GenerateCode(WarpShuffle shuffle)
        {
            if (!CLInstructions.TryGetShuffleOperation(
                Backend.Vendor,
                shuffle.Kind,
                out string operation))
            {
                throw new InvalidCodeGenerationException();
            }

            var source = Load(shuffle.Variable);
            var origin = Load(shuffle.Origin);
            var target = Allocate(shuffle);

            using var statement = BeginStatement(target);
            statement.AppendCommand(operation);
            statement.BeginArguments();

            statement.AppendArgument(source);
            // TODO: create a generic version that does not need this switch
            switch (shuffle.Kind)
            {
                case ShuffleKind.Down:
                case ShuffleKind.Up:
                    statement.AppendArgument(source);
                    break;
            }
            statement.AppendArgument(origin);

            statement.EndArguments();
        }

        /// <summary cref="IBackendCodeGenerator.GenerateCode(SubWarpShuffle)"/>
        public void GenerateCode(SubWarpShuffle shuffle) =>
            throw new InvalidCodeGenerationException();

        /// <summary cref="IBackendCodeGenerator.GenerateCode(DebugOperation)"/>
        public void GenerateCode(DebugOperation debug) =>
            // Invalid debug node -> should have been removed
            debug.Assert(false);
    }
}

// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXCodeGenerator.Views.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;

namespace ILGPU.Backends.PTX
{
    partial class PTXCodeGenerator
    {
        /// <summary cref="IValueVisitor.Visit(NewView)"/>
        public void Visit(NewView value)
        {
            var pointer = LoadPrimitive(value.Pointer);
            var length = LoadPrimitive(value.Length);

            var viewValue = new ViewImplementationRegister(
                value.Type as ViewType,
                pointer,
                length);
            Bind(value, viewValue);
        }

        /// <summary cref="IValueVisitor.Visit(GetViewLength)"/>
        public void Visit(GetViewLength value)
        {
            var viewSource = LoadAs<ViewImplementationRegister>(value.View);
            Bind(value, viewSource.Length);
        }

        /// <summary>
        /// Creates a new empty view.
        /// </summary>
        /// <param name="value">The source value.</param>
        /// <param name="viewType">The view type.</param>
        private void MakeNullView(NullValue value, ViewType viewType)
        {
            var viewRegister = AllocateViewRegisterAs<ViewImplementationRegister>(viewType);
            using (var command = BeginMove())
            {
                command.AppendSuffix(viewRegister.Pointer.BasicValueType);
                command.AppendArgument(viewRegister.Pointer);
                command.AppendConstant(0);
            }
            using (var command = BeginMove())
            {
                command.AppendSuffix(viewRegister.Length.BasicValueType);
                command.AppendArgument(viewRegister.Length);
                command.AppendConstant(0);
            }
            Bind(value, viewRegister);
        }

        /// <summary cref="IValueVisitor.Visit(ViewCast)"/>
        public void Visit(ViewCast value)
        {
            var source = LoadAs<ViewImplementationRegister>(value.Value);
            var pointer = source.Pointer;
            var length = source.Length;

            var sourceElementSize = ABI.GetSizeOf(value.SourceElementType);
            var targetElementSize = ABI.GetSizeOf(value.TargetElementType);

            // var newLength = length * sourceElementSize / targetElementSize;
            var lengthTimesSourceElementSize = AllocateRegister(length.Description);
            var newLength = AllocateRegister(length.Description);
            using (var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Mul,
                    ArithmeticBasicValueType.Int32,
                    FastMath)))
            {
                command.AppendArgument(lengthTimesSourceElementSize);
                command.AppendArgument(length);
                command.AppendConstant(sourceElementSize);
            }

            using (var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Div,
                    ArithmeticBasicValueType.Int32,
                    FastMath)))
            {
                command.AppendArgument(newLength);
                command.AppendArgument(lengthTimesSourceElementSize);
                command.AppendConstant(targetElementSize);
            }

            var newView = new ViewImplementationRegister(
                value.Type as ViewType,
                pointer,
                newLength);
            Bind(value, newView);

            FreeRegister(lengthTimesSourceElementSize);
        }

        /// <summary cref="IValueVisitor.Visit(SubViewValue)"/>
        public void Visit(SubViewValue value)
        {
            var viewType = value.Type as ViewType;
            var source = LoadAs<ViewImplementationRegister>(value.Source);
            var offset = LoadPrimitive(value.Offset);
            var length = LoadPrimitive(value.Length);

            var targetAddressRegister = AllocatePlatformRegister(value, out RegisterDescription _);
            MakeLoadElementAddress(
                viewType,
                offset,
                targetAddressRegister,
                source.Pointer);

            var newSubView = new ViewImplementationRegister(
                viewType,
                targetAddressRegister,
                length);
            Bind(value, newSubView);
        }

        /// <summary cref="IValueVisitor.Visit(LoadElementAddress)"/>
        public void Visit(LoadElementAddress value)
        {
            var elementIndex = LoadPrimitive(value.ElementIndex);
            var targetAddressRegister = AllocatePlatformRegister(value, out RegisterDescription _);

            PrimitiveRegister address;
            if (value.IsPointerAccess)
                address = LoadPrimitive(value.Source);
            else
            {
                var viewSource = LoadAs<ViewImplementationRegister>(value.Source);
                address = viewSource.Pointer;
            }

            MakeLoadElementAddress(
                value.Type as AddressSpaceType,
                elementIndex,
                targetAddressRegister,
                address);
        }

        /// <summary>
        /// Creates a set of instructions to realize a generic lea operation.
        /// </summary>
        /// <param name="sourceType">The source address type (pointer or view).</param>
        /// <param name="elementIndex">The current element index (the offset).</param>
        /// <param name="targetAddressRegister">The allocated target pointer register to write to.</param>
        /// <param name="address">The source address.</param>
        private void MakeLoadElementAddress(
            AddressSpaceType sourceType,
            PrimitiveRegister elementIndex,
            PrimitiveRegister targetAddressRegister,
            PrimitiveRegister address)
        {
            var elementSize = ABI.GetSizeOf(sourceType.ElementType);
            var offsetRegister = AllocatePlatformRegister(out RegisterDescription _);
            using (var command = BeginCommand(
                PTXInstructions.GetLEAMulOperation(ABI.PointerArithmeticType)))
            {
                command.AppendArgument(offsetRegister);
                command.AppendArgument(elementIndex);
                command.AppendConstant(elementSize);
            }

            using (var command = BeginCommand(
                PTXInstructions.GetArithmeticOperation(
                    BinaryArithmeticKind.Add,
                    ABI.PointerArithmeticType,
                    false)))
            {
                command.AppendArgument(targetAddressRegister);
                command.AppendArgument(address);
                command.AppendArgument(offsetRegister);
            }

            FreeRegister(offsetRegister);
        }

        /// <summary cref="IValueVisitor.Visit(AddressSpaceCast)"/>
        public void Visit(AddressSpaceCast value)
        {
            var sourceType = value.SourceType as AddressSpaceType;
            var targetAdressRegister = AllocatePlatformRegister(value, out RegisterDescription _);

            PrimitiveRegister address;
            if (value.IsPointerCast)
                address = LoadPrimitive(value.Value);
            else
            {
                var viewSource = LoadAs<ViewImplementationRegister>(value.Value);
                address = viewSource.Pointer;

                // Reuse the existing length register since we don't modify the result
                var viewTarget = new ViewImplementationRegister(
                    value.Type as ViewType,
                    targetAdressRegister,
                    viewSource.Length);
                Bind(value, viewTarget);
            }

            var toGeneric = value.TargetAddressSpace == MemoryAddressSpace.Generic;
            var addressSpaceOperation = PTXInstructions.GetAddressSpaceCast(toGeneric);
            var addressSpaceOperationSuffix = PTXInstructions.GetAddressSpaceCastSuffix(ABI);

            using (var command = BeginCommand(addressSpaceOperation))
            {
                command.AppendAddressSpace(
                    toGeneric ? sourceType.AddressSpace : value.TargetAddressSpace);
                command.AppendSuffix(addressSpaceOperationSuffix);
                command.AppendArgument(targetAdressRegister);
                command.AppendArgument(address);
            }
        }
    }
}

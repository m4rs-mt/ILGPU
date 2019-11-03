// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLCodeGenerator.Views.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;

namespace ILGPU.Backends.OpenCL
{
    partial class CLCodeGenerator
    {
        /// <summary cref="IValueVisitor.Visit(NewView)"/>
        public void Visit(NewView value)
        {
            var pointer = LoadAs<PointerVariable>(value.Pointer);
            var length = LoadAs<PrimitiveVariable>(value.Length);

            var viewValue = new ViewVariable(
                value.Type as ViewType,
                pointer,
                length);
            Bind(value, viewValue);
        }

        /// <summary cref="IValueVisitor.Visit(GetViewLength)"/>
        public void Visit(GetViewLength value)
        {
            var viewSource = LoadAs<ViewVariable>(value.View);
            Bind(value, viewSource.Length);
        }

        /// <summary>
        /// Creates a new empty view.
        /// </summary>
        /// <param name="value">The source value.</param>
        /// <param name="viewType">The view type.</param>
        private void MakeNullView(NullValue value, ViewType viewType)
        {
            var viewVariable = AllocateType(viewType) as ViewVariable;
            using (var statement = BeginStatement(viewVariable.Pointer))

                statement.AppendConstant(0);
            using (var statement = BeginStatement(viewVariable.Length))
                statement.AppendConstant(0);

            Bind(value, viewVariable);
        }

        /// <summary cref="IValueVisitor.Visit(ViewCast)"/>
        public void Visit(ViewCast value)
        {
            var source = LoadAs<ViewVariable>(value.Value);
            var pointer = source.Pointer;
            var length = source.Length;

            var sourceElementSize = ABI.GetSizeOf(value.SourceElementType);
            var targetElementSize = ABI.GetSizeOf(value.TargetElementType);

            // var newLength = length * sourceElementSize / targetElementSize;
            var newLength = AllocateType(BasicValueType.Int32) as PrimitiveVariable;

            using (var statement = BeginStatement(newLength))
            {
                statement.OpenParen();
                statement.Append(length);
                statement.AppendOperation(
                    CLInstructions.GetArithmeticOperation(
                        BinaryArithmeticKind.Mul,
                        false,
                        out bool _));
                statement.AppendConstant(sourceElementSize);
                statement.CloseParen();
                statement.AppendOperation(
                    CLInstructions.GetArithmeticOperation(
                        BinaryArithmeticKind.Div,
                        false,
                        out bool _));
                statement.AppendConstant(targetElementSize);
            }

            var newView = new ViewVariable(
                value.Type as ViewType,
                pointer,
                newLength);
            Bind(value, newView);
        }

        /// <summary cref="IValueVisitor.Visit(SubViewValue)"/>
        public void Visit(SubViewValue value)
        {
            var viewType = value.Type as ViewType;
            var source = LoadAs<ViewVariable>(value.Source);
            var offset = LoadAs<PrimitiveVariable>(value.Offset);
            var length = LoadAs<PrimitiveVariable>(value.Length);

            var target = AllocatePointerType(viewType.ElementType, viewType.AddressSpace);
            MakeLoadElementAddress(
                offset,
                target,
                source.Pointer);

            var newSubView = new ViewVariable(
                viewType,
                target,
                length);
            Bind(value, newSubView);
        }

        /// <summary cref="IValueVisitor.Visit(LoadElementAddress)"/>
        public void Visit(LoadElementAddress value)
        {
            var pointerType = value.Type as PointerType;
            var elementIndex = LoadAs<PrimitiveVariable>(value.ElementIndex);
            var target = AllocatePointerType(pointerType.ElementType, pointerType.AddressSpace);

            PointerVariable address;
            if (value.IsPointerAccess)
                address = LoadAs<PointerVariable>(value.Source);
            else
            {
                var viewSource = LoadAs<ViewVariable>(value.Source);
                address = viewSource.Pointer;
            }

            MakeLoadElementAddress(
                elementIndex,
                target,
                address);

            Bind(value, target);
        }

        /// <summary>
        /// Creates a set of operations to realize a generic lea operation.
        /// </summary>
        /// <param name="elementIndex">The current element index (the offset).</param>
        /// <param name="target">The allocated target variable to write to.</param>
        /// <param name="address">The source address.</param>
        private void MakeLoadElementAddress(
            PrimitiveVariable elementIndex,
            PointerVariable target,
            PointerVariable address)
        {
            using (var statement = BeginStatement(target))
            {
                statement.AppendCommand(CLInstructions.AddressOfOperation);
                statement.Append(address);
                statement.AppendIndexer(elementIndex);
            }
        }

        /// <summary cref="IValueVisitor.Visit(AddressSpaceCast)"/>
        public void Visit(AddressSpaceCast value)
        {
            var targetType = value.TargetType as AddressSpaceType;
            var target = AllocatePointerType(targetType.ElementType, value.TargetAddressSpace);

            PointerVariable address;
            if (value.IsPointerCast)
            {
                address = LoadAs<PointerVariable>(value.Value);
                Bind(value, target);
            }
            else
            {
                var viewSource = LoadAs<ViewVariable>(value.Value);
                address = viewSource.Pointer;

                var viewTarget = new ViewVariable(
                    value.Type as ViewType,
                    target,
                    viewSource.Length);
                Bind(value, viewTarget);
            }

            if (CLInstructions.TryGetAddressSpaceCast(
                value.TargetAddressSpace,
                out string operation))
            {
                // There is a specific cast operation
                using (var statement = BeginStatement(target))
                {
                    statement.AppendCommand(operation);
                    statement.BeginArguments();
                    statement.AppendArgument(address);
                    statement.EndArguments();
                }
            }
            else
            {
                // Use an unspecific generic pointer cast
                using (var statement = BeginStatement(target))
                {
                    statement.AppendCast(
                        TypeGenerator[targetType.ElementType] +
                        CLInstructions.DereferenceOperation);
                    statement.Append(address);
                }
            }
        }
    }
}

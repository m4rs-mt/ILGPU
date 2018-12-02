// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: ViewRegisterAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;

namespace ILGPU.Backends.PointerViews
{
    /// <summary>
    /// Represents a register allocator that uses the <see cref="ViewImplementation{T}"/>
    /// as native view representation.
    /// </summary>
    /// <typeparam name="TKind">The register kind.</typeparam>
    abstract class ViewRegisterAllocator<TKind> : RegisterAllocator<TKind>
        where TKind : struct
    {
        public sealed class ViewImplementationRegister : ViewRegister
        {
            internal ViewImplementationRegister(
                ViewType viewType,
                PrimitiveRegister pointer,
                PrimitiveRegister length)
                : base(viewType)
            {
                Pointer = pointer;
                Length = length;
            }

            /// <summary>
            /// The pointer register.
            /// </summary>
            public PrimitiveRegister Pointer { get; }

            /// <summary>
            /// The length register.
            /// </summary>
            public PrimitiveRegister Length { get; }
        }

        /// <summary>
        /// Constructs a new view allocator.
        /// </summary>
        /// <param name="abi">The source ABI.</param>
        protected ViewRegisterAllocator(ABI abi)
            : base(abi)
        { }

        /// <summary cref="RegisterAllocator{TKind}.AllocateViewRegister(ViewType)"/>
        public override Register AllocateViewRegister(ViewType viewType)
        {
            var ptrKind = ConvertTypeToKind(ABI.PointerType);
            var pointerRegister = AllocateRegister(ptrKind);

            var lengthKind = ConvertTypeToKind(
                ABI.TypeContext.GetPrimitiveType(BasicValueType.Int32));
            var lengthRegister = AllocateRegister(lengthKind);

            return new ViewImplementationRegister(viewType, pointerRegister, lengthRegister);
        }

        /// <summary cref="RegisterAllocator{TKind}.FreeViewRegister(RegisterAllocator{TKind}.ViewRegister)"/>
        public override void FreeViewRegister(ViewRegister register)
        {
            var viewRegister = register as ViewImplementationRegister;
            FreeRegister(viewRegister.Pointer);
            FreeRegister(viewRegister.Length);
        }
    }
}

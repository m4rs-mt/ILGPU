// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
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
    public abstract class ViewRegisterAllocator<TKind> : RegisterAllocator<TKind>
        where TKind : struct
    {
        /// <summary>
        /// Implements a view register.
        /// </summary>
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
            var ptrDesc = ResolveRegisterDescription(ABI.PointerType);
            var pointerRegister = AllocateRegister(ptrDesc);

            var lengthDesc = ResolveRegisterDescription(
                ABI.TypeContext.GetPrimitiveType(BasicValueType.Int32));
            var lengthRegister = AllocateRegister(lengthDesc);

            return new ViewImplementationRegister(viewType, pointerRegister, lengthRegister);
        }

        /// <summary cref="RegisterAllocator{TKind}.FreeViewRegister(RegisterAllocator{TKind}.ViewRegister)"/>
        public override void FreeViewRegister(ViewRegister viewRegister)
        {
            var implRegister = viewRegister as ViewImplementationRegister;
            FreeRegister(implRegister.Pointer);
            FreeRegister(implRegister.Length);
        }
    }
}

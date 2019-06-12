// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ITypeNodeVisitor.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

namespace ILGPU.IR.Types
{
    /// <summary>
    /// A generic interface to visit type nodes in the IR.
    /// </summary>
    public interface ITypeNodeVisitor
    {
        /// <summary>
        /// Visits the type.
        /// </summary>
        /// <param name="type">The type.</param>
        void Visit(VoidType type);

        /// <summary>
        /// Visits the type.
        /// </summary>
        /// <param name="type">The type.</param>
        void Visit(StringType type);

        /// <summary>
        /// Visits the type.
        /// </summary>
        /// <param name="type">The type.</param>
        void Visit(PrimitiveType type);

        /// <summary>
        /// Visits the type.
        /// </summary>
        /// <param name="type">The type.</param>
        void Visit(PointerType type);

        /// <summary>
        /// Visits the type.
        /// </summary>
        /// <param name="type">The type.</param>
        void Visit(ViewType type);

        /// <summary>
        /// Visits the type.
        /// </summary>
        /// <param name="type">The type.</param>
        void Visit(ArrayType type);

        /// <summary>
        /// Visits the type.
        /// </summary>
        /// <param name="type">The type.</param>
        void Visit(StructureType type);

        /// <summary>
        /// Visits the type.
        /// </summary>
        /// <param name="type">The type.</param>
        void Visit(HandleType type);
    }
}

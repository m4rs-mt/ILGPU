// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: GeneratedStructureOfArraysAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.CodeGeneration
{
    /// <summary>
    /// Generates a structure-of-arrays from a definition struct, for a given length.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GeneratedStructureOfArraysAttribute : Attribute
    {
        /// <summary>
        /// The structure type to use as a definition.
        /// </summary>
        public Type StructureType { get; }

        /// <summary>
        /// The number of elements in the array.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Constructs
        /// </summary>
        /// <param name="structureType">The definition struct.</param>
        /// <param name="length">The number of elements.</param>
        public GeneratedStructureOfArraysAttribute(Type structureType, int length)
        {
            StructureType = structureType;
            Length = length;
        }
    }
}

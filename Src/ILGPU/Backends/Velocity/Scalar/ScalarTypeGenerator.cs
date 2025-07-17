// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: ScalarTypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Runtime.Velocity;
using System;

namespace ILGPU.Backends.Velocity.Scalar
{
    /// <summary>
    /// A scalar type generator to be used with the Velocity backend.
    /// </summary>
    sealed class ScalarTypeGenerator : VelocityTypeGenerator
    {
        #region Static

        /// <summary>
        /// Maps basic types to vectorized basic types.
        /// </summary>
        private static readonly Type[] VectorizedBasicTypeMapping = new Type[]
        {
            ScalarOperations2.WarpType32, // None/Unknown

            ScalarOperations2.WarpType32, // Int1
            ScalarOperations2.WarpType32, // Int8
            ScalarOperations2.WarpType32, // Int16
            ScalarOperations2.WarpType32, // Int32
            ScalarOperations2.WarpType64, // Int64
            ScalarOperations2.WarpType32, // Float8E4M3
            ScalarOperations2.WarpType32, // Float8E5M2
            ScalarOperations2.WarpType32, // BFloat16
            ScalarOperations2.WarpType32, // Float16
            ScalarOperations2.WarpType32, // Float32
            ScalarOperations2.WarpType64, // Float64
        };

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new IL scalar code generator.
        /// </summary>
        /// <param name="capabilityContext">The parent capability context.</param>
        /// <param name="runtimeSystem">The parent runtime system.</param>
        public ScalarTypeGenerator(
            VelocityCapabilityContext capabilityContext,
            RuntimeSystem runtimeSystem)
            : base(capabilityContext, runtimeSystem, ScalarOperations2.WarpSize)
        { }

        #endregion

        #region Type System

        public override Type GetVectorizedBasicType(BasicValueType basicValueType)
        {
            if (basicValueType == BasicValueType.Float8E4M3
                && !CapabilityContext.Float8E4M3)
                throw VelocityCapabilityContext.GetNotSupportedFloat8E4M3Exception();
            if (basicValueType == BasicValueType.Float8E5M2
                && !CapabilityContext.Float8E5M2)
                throw VelocityCapabilityContext.GetNotSupportedFloat8E5M2Exception();
            if (basicValueType == BasicValueType.BFloat16
                && !CapabilityContext.BFloat16)
                throw VelocityCapabilityContext.GetNotSupportedBFloat16Exception();
            if (basicValueType == BasicValueType.Float16
                && !CapabilityContext.Float16)
                throw VelocityCapabilityContext.GetNotSupportedFloat16Exception();
            return VectorizedBasicTypeMapping[(int)basicValueType];
        }

        #endregion
    }
}

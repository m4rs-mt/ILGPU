// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Vec256TypeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Runtime.Velocity;
using System;
using System.Numerics;

#if NET7_0_OR_GREATER

namespace ILGPU.Backends.Velocity.Vec256
{
    /// <summary>
    /// A vector type generator of 256bit vectors to be used with the Velocity backend.
    /// </summary>
    sealed class Vec256TypeGenerator : VelocityTypeGenerator
    {
        #region Static

        /// <summary>
        /// Maps basic types to vectorized basic types.
        /// </summary>
        private static readonly Type[] VectorizedBasicTypeMapping = new Type[]
        {
            Vec256Operations.WarpType32, // None/Unknown

            Vec256Operations.WarpType32, // Int1
            Vec256Operations.WarpType32, // Int8
            Vec256Operations.WarpType32, // Int16
            Vec256Operations.WarpType32, // Int32
            Vec256Operations.WarpType64, // Int64

            Vec256Operations.WarpType32, // Float16
            Vec256Operations.WarpType32, // Float32
            Vec256Operations.WarpType64, // Float64
        };

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new IL vector type generator.
        /// </summary>
        /// <param name="capabilityContext">The parent capability context.</param>
        /// <param name="runtimeSystem">The parent runtime system.</param>
        public Vec256TypeGenerator(
            VelocityCapabilityContext capabilityContext,
            RuntimeSystem runtimeSystem)
            : base(capabilityContext, runtimeSystem, Vector<int>.Count)
        { }

        #endregion

        #region Type System

        public override Type GetVectorizedBasicType(BasicValueType basicValueType)
        {
            if (basicValueType == BasicValueType.Float16 && !CapabilityContext.Float16)
                throw VelocityCapabilityContext.GetNotSupportedFloat16Exception();
            return VectorizedBasicTypeMapping[(int)basicValueType];
        }

        #endregion
    }
}

#endif

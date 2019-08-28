// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: AcceleratorSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// The basic configuration interface for all intrinsic specializers.
    /// </summary>
    public interface IAcceleratorSpecializerConfiguration
    {
        /// <summary>
        /// Returns the current warp size.
        /// </summary>
        int WarpSize { get; }

        /// <summary>
        /// Tries to resolve the native size in bytes of the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="size">The native size in bytes.</param>
        /// <returns>True, if the size could be resolved.</returns>
        bool TryGetSizeOf(TypeNode type, out int size);
    }

    /// <summary>
    /// Represents a device specializer that instantiates device-specific constants
    /// and updates device-specific functionality.
    /// </summary>
    /// <remarks>
    /// Note that this class does not perform recursive specialization operations.
    /// </remarks>
    /// <typeparam name="TConfiguration">The actual configuration type.</typeparam>
    public sealed class AcceleratorSpecializer<TConfiguration> : UnorderedTransformation
        where TConfiguration : IAcceleratorSpecializerConfiguration
    {
        private readonly TConfiguration configuration;

        /// <summary>
        /// Constructs a new device specializer.
        /// </summary>
        public AcceleratorSpecializer(in TConfiguration specializerConfiguration)
        {
            configuration = specializerConfiguration;
        }

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var scope = builder.CreateScope();
            bool applied = false;

            var nativeWarpSize = configuration.WarpSize;
            Debug.Assert(nativeWarpSize > 0, "Invalid native warp size");

            foreach (Value value in scope.Values)
            {
                switch (value)
                {
                    case WarpSizeValue warpSizeValue:
                        SpecializeConstant(builder, warpSizeValue, nativeWarpSize, ref applied);
                        break;
                    case SizeOfValue sizeOfValue when configuration.TryGetSizeOf(sizeOfValue.TargetType, out int size):
                        SpecializeConstant(builder, sizeOfValue, size, ref applied);
                        break;
                }
            }

            return applied;
        }

        /// <summary>
        /// Specializes a single constant value.
        /// </summary>
        /// <param name="builder">The parent method builder.</param>
        /// <param name="value">The method to specialize.</param>
        /// <param name="constant">The constant integer value.</param>
        /// <param name="applied">The applied value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SpecializeConstant(
            Method.Builder builder,
            Value value,
            int constant,
            ref bool applied)
        {
            var blockBuilder = builder[value.BasicBlock];
            var primitiveSize = blockBuilder.CreatePrimitiveValue(constant);
            value.Replace(primitiveSize);

            applied = true;
        }
    }
}


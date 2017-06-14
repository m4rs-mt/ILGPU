// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2017 Marcel Koester
//                                www.ilgpu.net
//
// File: MathIntrinsicAttribute.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Compiler.Intrinsic
{
    enum MathIntrinsicKind
    {
        IsNaNF32,
        IsNaNF64,

        IsInfF32,
        IsInfF64,

        MulF32,
        MulF64,

        DivF32,
        DivF64,

        RemF32,
        RemF64,

        AbsF32,
        AbsF64,

        SqrtF32,
        SqrtF64,

        RsqrtF32,
        RsqrtF64,

        CbrtF32,
        CbrtF64,

        RcbrtF32,
        RcbrtF64,

        AsinF32,
        AsinF64,

        SinF32,
        SinF64,

        SinHF32,
        SinHF64,

        AcosF32,
        AcosF64,

        CosF32,
        CosF64,

        CosHF32,
        CosHF64,

        AtanF32,
        AtanF64,

        Atan2F32,
        Atan2F64,

        TanF32,
        TanF64,

        TanhF32,
        TanhF64,

        SinCosF32,
        SinCosF64,
        
        PowF32,
        PowF64,
        
        ExpF32,
        ExpF64,

        Exp10F32,
        Exp10F64,

        FloorF32,
        FloorF64,

        CeilingF32,
        CeilingF64,

        LogF32,
        LogF64,

        Log2F32,
        Log2F64,

        Log10F32,
        Log10F64,

        MinF32,
        MinF64,

        MaxF32,
        MaxF64,

        TruncateF32,
        TruncateF64,

        RoundToEvenF32,
        RoundToEvenF64,

        RoundAwayFromZeroF32,
        RoundAwayFromZeroF64,

        __IntIntrinsics,

        AbsI32,
        AbsI64,

        MinI32,
        MinI64,
        MinUI32,
        MinUI64,

        MaxI32,
        MaxI64,
        MaxUI32,
        MaxUI64,
    }

    static class MethodIntrinsicKindExtensions
    {
        /// <summary>
        /// Returns true iff the given intrinsic kind represents an intrinsic operation
        /// that works on floats.
        /// </summary>
        /// <param name="kind">The intrinsic kind.</param>
        /// <returns>
        /// True, iff the given intrinsic kind represents an intrinsic operation
        /// that works on floats.
        /// </returns>
        public static bool IsFloatIntrinsic(this MathIntrinsicKind kind)
        {
            return kind < MathIntrinsicKind.__IntIntrinsics;
        }

        /// <summary>
        /// Returns true iff the given intrinsic kind represents an intrinsic operation
        /// that works on 32bit floats.
        /// </summary>
        /// <param name="kind">The intrinsic kind.</param>
        /// <returns>
        /// True, iff the given intrinsic kind represents an intrinsic operation
        /// that works on 32bit floats.
        /// </returns>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Might be required in the future")]
        public static bool IsF32Intrinsic(this MathIntrinsicKind kind)
        {
            return kind.IsFloatIntrinsic() && ((int)kind % 2) == 1;
        }

        /// <summary>
        /// Returns true iff the given intrinsic kind represents an intrinsic operation
        /// that works on 6432bit floats.
        /// </summary>
        /// <param name="kind">The intrinsic kind.</param>
        /// <returns>
        /// True, iff the given intrinsic kind represents an intrinsic operation
        /// that works on 64bit floats.
        /// </returns>
        public static bool IsF64Intrinsic(this MathIntrinsicKind kind)
        {
            return kind.IsFloatIntrinsic() && ((int)kind % 2) == 1;
        }

        /// <summary>
        /// Returns true iff the given intrinsic kind represents an intrinsic operation
        /// that works on integers.
        /// </summary>
        /// <param name="kind">The intrinsic kind.</param>
        /// <returns>
        /// True, iff the given intrinsic kind represents an intrinsic operation
        /// that works on 32bit floats.
        /// </returns>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Might be required in the future")]
        public static bool IsIntIntrinsic(this MathIntrinsicKind kind)
        {
            return kind > MathIntrinsicKind.__IntIntrinsics;
        }

        /// <summary>
        /// Resolves the appropriate intrinsic kind for the desired scenario.
        /// </summary>
        /// <param name="kind">The intrinsic kind.</param>
        /// <param name="force32BitMath">True, iff the intrinsic should treat f64 as f32.</param>
        /// <returns>The resolved intrinsic kind.</returns>
        public static MathIntrinsicKind ResolveIntrinsicKind(this MathIntrinsicKind kind, bool force32BitMath)
        {
            if (force32BitMath && kind.IsF64Intrinsic())
                return (MathIntrinsicKind)((int)kind - 1);
            return kind;
        }
    }

    /// <summary>
    /// Marks math methods that are builtin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    sealed class MathIntrinsicAttribute : IntrinsicAttribute
    {
        public MathIntrinsicAttribute(MathIntrinsicKind kind)
        {
            IntrinsicKind = kind;
        }

        public override IntrinsicType Type => IntrinsicType.Math;

        /// <summary>
        /// Returns the assigned intrinsic kind.
        /// </summary>
        public MathIntrinsicKind IntrinsicKind { get; }
    }
}

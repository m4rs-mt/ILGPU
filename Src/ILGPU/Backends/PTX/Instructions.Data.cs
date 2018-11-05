// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Instructions.Data.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System.Collections.Generic;

namespace ILGPU.Backends.PTX
{
    partial class Instructions
    {
        public const string ReturnOperation = "ret";
        public const string MoveOperation = "mov";
        public const string LoadOperation = "ld";
        public const string LoadParamOperation = "ld.param";
        public const string LoadLocalOperation = "ld.local";
        public const string StoreOperation = "st";
        public const string StoreParamOperation = "st.param";
        public const string BranchOperation = "bra";
        public const string BranchIndexOperation = "brx.idx";
        public const string BranchIndexRangeComparison = "setp.ge.or.s32";
        public const string BranchTargetsDeclaration = ".branchtargets";
        public const string FMAOperationLo = "mad.lo";
        public const string AtomicCASOperation = "atom.cas";
        public const uint AllThreadsInAWarpMemberMask = 0xffffffff;

        private static readonly Dictionary<ArithmeticBasicValueType, string> LEAMulOperations =
            new Dictionary<ArithmeticBasicValueType, string>()
            {
                { ArithmeticBasicValueType.UInt32, "mul.u32" },
                { ArithmeticBasicValueType.UInt64, "mul.wide.u32" },
            };

        private static readonly string[] AddressSpaceCastOperations =
        {
            "cvta",
            "cvta.to",
        };

        private static readonly string[] BarrierOperations =
        {
            "bar.warp.sync",
            "bar.sync",
        };

        private static readonly string[] PredicateBarrierOperations =
        {
            "bar.red.popc.u32",
            "bar.red.and.pred",
            "bar.red.or.pred",
        };

        private static readonly string[] MemoryBarrierOperations =
        {
            "membar.cta",
            "membar.gl",
            "membar.sys",
        };

        private static readonly string[] ShuffleOperations =
        {
            "shfl.sync.idx.b32",
            "shfl.sync.down.b32",
            "shfl.sync.up.b32",
            "shfl.sync.bfly.b32",
        };

        private static readonly Dictionary<BasicValueType, string> SelectValueOperations =
            new Dictionary<BasicValueType, string>()
            {
                { BasicValueType.Int8, "selp.b16" },
                { BasicValueType.Int16, "selp.b16" },
                { BasicValueType.Int32, "selp.b32" },
                { BasicValueType.Int64, "selp.b64" },

                { BasicValueType.Float32, "selp.b32" },
                { BasicValueType.Float64, "selp.b64" },
            };

        private static readonly Dictionary<(CompareKind, ArithmeticBasicValueType), string> CompareOperations =
            new Dictionary<(CompareKind, ArithmeticBasicValueType), string>()
            {
                { (CompareKind.Equal, ArithmeticBasicValueType.Int16), "setp.eq.s16" },
                { (CompareKind.Equal, ArithmeticBasicValueType.Int32), "setp.eq.s32" },
                { (CompareKind.Equal, ArithmeticBasicValueType.Int64), "setp.eq.s64" },
                { (CompareKind.Equal, ArithmeticBasicValueType.UInt16), "setp.eq.u16" },
                { (CompareKind.Equal, ArithmeticBasicValueType.UInt32), "setp.eq.u32" },
                { (CompareKind.Equal, ArithmeticBasicValueType.UInt64), "setp.eq.u64" },
                { (CompareKind.Equal, ArithmeticBasicValueType.Float32), "setp.eq.f32" },
                { (CompareKind.Equal, ArithmeticBasicValueType.Float64), "setp.eq.f64" },

                { (CompareKind.NotEqual, ArithmeticBasicValueType.Int16), "setp.ne.s16" },
                { (CompareKind.NotEqual, ArithmeticBasicValueType.Int32), "setp.ne.s32" },
                { (CompareKind.NotEqual, ArithmeticBasicValueType.Int64), "setp.ne.s64" },
                { (CompareKind.NotEqual, ArithmeticBasicValueType.UInt16), "setp.ne.u16" },
                { (CompareKind.NotEqual, ArithmeticBasicValueType.UInt32), "setp.ne.u32" },
                { (CompareKind.NotEqual, ArithmeticBasicValueType.UInt64), "setp.ne.u64" },
                { (CompareKind.NotEqual, ArithmeticBasicValueType.Float32), "setp.ne.f32" },
                { (CompareKind.NotEqual, ArithmeticBasicValueType.Float64), "setp.ne.f64" },

                { (CompareKind.LessThan, ArithmeticBasicValueType.Int16), "setp.lt.s16" },
                { (CompareKind.LessThan, ArithmeticBasicValueType.Int32), "setp.lt.s32" },
                { (CompareKind.LessThan, ArithmeticBasicValueType.Int64), "setp.lt.s64" },
                { (CompareKind.LessThan, ArithmeticBasicValueType.UInt16), "setp.lo.u16" },
                { (CompareKind.LessThan, ArithmeticBasicValueType.UInt32), "setp.lo.u32" },
                { (CompareKind.LessThan, ArithmeticBasicValueType.UInt64), "setp.lo.u64" },
                { (CompareKind.LessThan, ArithmeticBasicValueType.Float32), "setp.lt.f32" },
                { (CompareKind.LessThan, ArithmeticBasicValueType.Float64), "setp.lt.f64" },

                { (CompareKind.LessEqual, ArithmeticBasicValueType.Int16), "setp.le.s16" },
                { (CompareKind.LessEqual, ArithmeticBasicValueType.Int32), "setp.le.s32" },
                { (CompareKind.LessEqual, ArithmeticBasicValueType.Int64), "setp.le.s64" },
                { (CompareKind.LessEqual, ArithmeticBasicValueType.UInt16), "setp.ls.u16" },
                { (CompareKind.LessEqual, ArithmeticBasicValueType.UInt32), "setp.ls.u32" },
                { (CompareKind.LessEqual, ArithmeticBasicValueType.UInt64), "setp.ls.u64" },
                { (CompareKind.LessEqual, ArithmeticBasicValueType.Float32), "setp.le.f32" },
                { (CompareKind.LessEqual, ArithmeticBasicValueType.Float64), "setp.le.f64" },

                { (CompareKind.GreaterThan, ArithmeticBasicValueType.Int16), "setp.gt.s16" },
                { (CompareKind.GreaterThan, ArithmeticBasicValueType.Int32), "setp.gt.s32" },
                { (CompareKind.GreaterThan, ArithmeticBasicValueType.Int64), "setp.gt.s64" },
                { (CompareKind.GreaterThan, ArithmeticBasicValueType.UInt16), "setp.hi.u16" },
                { (CompareKind.GreaterThan, ArithmeticBasicValueType.UInt32), "setp.hi.u32" },
                { (CompareKind.GreaterThan, ArithmeticBasicValueType.UInt64), "setp.hi.u64" },
                { (CompareKind.GreaterThan, ArithmeticBasicValueType.Float32), "setp.gt.f32" },
                { (CompareKind.GreaterThan, ArithmeticBasicValueType.Float64), "setp.gt.f64" },

                { (CompareKind.GreaterEqual, ArithmeticBasicValueType.Int16), "setp.ge.s16" },
                { (CompareKind.GreaterEqual, ArithmeticBasicValueType.Int32), "setp.ge.s32" },
                { (CompareKind.GreaterEqual, ArithmeticBasicValueType.Int64), "setp.ge.s64" },
                { (CompareKind.GreaterEqual, ArithmeticBasicValueType.UInt16), "setp.hs.u16" },
                { (CompareKind.GreaterEqual, ArithmeticBasicValueType.UInt32), "setp.hs.u32" },
                { (CompareKind.GreaterEqual, ArithmeticBasicValueType.UInt64), "setp.hs.u64" },
                { (CompareKind.GreaterEqual, ArithmeticBasicValueType.Float32), "setp.ge.f32" },
                { (CompareKind.GreaterEqual, ArithmeticBasicValueType.Float64), "setp.ge.f64" },
            };

        private static readonly Dictionary<(ArithmeticBasicValueType, ArithmeticBasicValueType), string> ConvertOperations =
            new Dictionary<(ArithmeticBasicValueType, ArithmeticBasicValueType), string>()
            {
                { (ArithmeticBasicValueType.Int8, ArithmeticBasicValueType.Int16), "cvt.s16.s8" },
                { (ArithmeticBasicValueType.Int8, ArithmeticBasicValueType.Int32), "cvt.s32.s8" },
                { (ArithmeticBasicValueType.Int8, ArithmeticBasicValueType.Int64), "cvt.s64.s8" },
                { (ArithmeticBasicValueType.Int8, ArithmeticBasicValueType.UInt8), "cvt.u8.s8" },
                { (ArithmeticBasicValueType.Int8, ArithmeticBasicValueType.UInt16), "cvt.u16.s8" },
                { (ArithmeticBasicValueType.Int8, ArithmeticBasicValueType.UInt32), "cvt.u32.s8" },
                { (ArithmeticBasicValueType.Int8, ArithmeticBasicValueType.UInt64), "cvt.u64.s8" },
                { (ArithmeticBasicValueType.Int8, ArithmeticBasicValueType.Float32), "cvt.rn.f32.s8" },
                { (ArithmeticBasicValueType.Int8, ArithmeticBasicValueType.Float64), "cvt.rn.f64.s8" },

                { (ArithmeticBasicValueType.Int16, ArithmeticBasicValueType.Int8), "cvt.s8.s16" },
                { (ArithmeticBasicValueType.Int16, ArithmeticBasicValueType.Int32), "cvt.s32.s16" },
                { (ArithmeticBasicValueType.Int16, ArithmeticBasicValueType.Int64), "cvt.s64.s16" },
                { (ArithmeticBasicValueType.Int16, ArithmeticBasicValueType.UInt8), "cvt.u8.s16" },
                { (ArithmeticBasicValueType.Int16, ArithmeticBasicValueType.UInt16), "cvt.u16.s16" },
                { (ArithmeticBasicValueType.Int16, ArithmeticBasicValueType.UInt32), "cvt.u32.s16" },
                { (ArithmeticBasicValueType.Int16, ArithmeticBasicValueType.UInt64), "cvt.u64.s16" },
                { (ArithmeticBasicValueType.Int16, ArithmeticBasicValueType.Float32), "cvt.rn.f32.s16" },
                { (ArithmeticBasicValueType.Int16, ArithmeticBasicValueType.Float64), "cvt.rn.f64.s16" },

                { (ArithmeticBasicValueType.Int32, ArithmeticBasicValueType.Int8), "cvt.s8.s32" },
                { (ArithmeticBasicValueType.Int32, ArithmeticBasicValueType.Int16), "cvt.s16.s32" },
                { (ArithmeticBasicValueType.Int32, ArithmeticBasicValueType.Int64), "cvt.s64.s32" },
                { (ArithmeticBasicValueType.Int32, ArithmeticBasicValueType.UInt8), "cvt.u8.s32" },
                { (ArithmeticBasicValueType.Int32, ArithmeticBasicValueType.UInt16), "cvt.u16.s32" },
                { (ArithmeticBasicValueType.Int32, ArithmeticBasicValueType.UInt32), "cvt.u32.s32" },
                { (ArithmeticBasicValueType.Int32, ArithmeticBasicValueType.UInt64), "cvt.u64.s32" },
                { (ArithmeticBasicValueType.Int32, ArithmeticBasicValueType.Float32), "cvt.rn.f32.s32" },
                { (ArithmeticBasicValueType.Int32, ArithmeticBasicValueType.Float64), "cvt.rn.f64.s32" },

                { (ArithmeticBasicValueType.Int64, ArithmeticBasicValueType.Int8), "cvt.s8.s64" },
                { (ArithmeticBasicValueType.Int64, ArithmeticBasicValueType.Int16), "cvt.s16.s64" },
                { (ArithmeticBasicValueType.Int64, ArithmeticBasicValueType.Int32), "cvt.s32.s64" },
                { (ArithmeticBasicValueType.Int64, ArithmeticBasicValueType.UInt8), "cvt.u8.s64" },
                { (ArithmeticBasicValueType.Int64, ArithmeticBasicValueType.UInt16), "cvt.u16.s64" },
                { (ArithmeticBasicValueType.Int64, ArithmeticBasicValueType.UInt32), "cvt.u32.s64" },
                { (ArithmeticBasicValueType.Int64, ArithmeticBasicValueType.UInt64), "cvt.u64.s64" },
                { (ArithmeticBasicValueType.Int64, ArithmeticBasicValueType.Float32), "cvt.rn.f32.s64" },
                { (ArithmeticBasicValueType.Int64, ArithmeticBasicValueType.Float64), "cvt.rn.f64.s64" },

                { (ArithmeticBasicValueType.UInt8, ArithmeticBasicValueType.Int8), "cvt.s8.u8" },
                { (ArithmeticBasicValueType.UInt8, ArithmeticBasicValueType.Int16), "cvt.s16.u8" },
                { (ArithmeticBasicValueType.UInt8, ArithmeticBasicValueType.Int32), "cvt.s32.u8" },
                { (ArithmeticBasicValueType.UInt8, ArithmeticBasicValueType.Int64), "cvt.s64.u8" },
                { (ArithmeticBasicValueType.UInt8, ArithmeticBasicValueType.UInt16), "cvt.u16.u8" },
                { (ArithmeticBasicValueType.UInt8, ArithmeticBasicValueType.UInt32), "cvt.u32.u8" },
                { (ArithmeticBasicValueType.UInt8, ArithmeticBasicValueType.UInt64), "cvt.u64.u8" },
                { (ArithmeticBasicValueType.UInt8, ArithmeticBasicValueType.Float32), "cvt.rn.f32.u8" },
                { (ArithmeticBasicValueType.UInt8, ArithmeticBasicValueType.Float64), "cvt.rn.f64.u8" },

                { (ArithmeticBasicValueType.UInt16, ArithmeticBasicValueType.Int8), "cvt.s8.u16" },
                { (ArithmeticBasicValueType.UInt16, ArithmeticBasicValueType.Int16), "cvt.s16.u16" },
                { (ArithmeticBasicValueType.UInt16, ArithmeticBasicValueType.Int32), "cvt.s32.u16" },
                { (ArithmeticBasicValueType.UInt16, ArithmeticBasicValueType.Int64), "cvt.s64.u16" },
                { (ArithmeticBasicValueType.UInt16, ArithmeticBasicValueType.UInt8), "cvt.u8.u16" },
                { (ArithmeticBasicValueType.UInt16, ArithmeticBasicValueType.UInt32), "cvt.u32.u16" },
                { (ArithmeticBasicValueType.UInt16, ArithmeticBasicValueType.UInt64), "cvt.u64.u16" },
                { (ArithmeticBasicValueType.UInt16, ArithmeticBasicValueType.Float32), "cvt.rn.f32.u16" },
                { (ArithmeticBasicValueType.UInt16, ArithmeticBasicValueType.Float64), "cvt.rn.f64.u16" },

                { (ArithmeticBasicValueType.UInt32, ArithmeticBasicValueType.Int8), "cvt.s8.u32" },
                { (ArithmeticBasicValueType.UInt32, ArithmeticBasicValueType.Int16), "cvt.s16.u32" },
                { (ArithmeticBasicValueType.UInt32, ArithmeticBasicValueType.Int32), "cvt.s32.u32" },
                { (ArithmeticBasicValueType.UInt32, ArithmeticBasicValueType.Int64), "cvt.s64.u32" },
                { (ArithmeticBasicValueType.UInt32, ArithmeticBasicValueType.UInt8), "cvt.u8.u32" },
                { (ArithmeticBasicValueType.UInt32, ArithmeticBasicValueType.UInt16), "cvt.u16.u32" },
                { (ArithmeticBasicValueType.UInt32, ArithmeticBasicValueType.UInt64), "cvt.u64.u32" },
                { (ArithmeticBasicValueType.UInt32, ArithmeticBasicValueType.Float32), "cvt.rn.f32.u32" },
                { (ArithmeticBasicValueType.UInt32, ArithmeticBasicValueType.Float64), "cvt.rn.f64.u32" },

                { (ArithmeticBasicValueType.UInt64, ArithmeticBasicValueType.Int8), "cvt.s8.u64" },
                { (ArithmeticBasicValueType.UInt64, ArithmeticBasicValueType.Int16), "cvt.s16.u64" },
                { (ArithmeticBasicValueType.UInt64, ArithmeticBasicValueType.Int32), "cvt.s32.u64" },
                { (ArithmeticBasicValueType.UInt64, ArithmeticBasicValueType.Int64), "cvt.s64.u64" },
                { (ArithmeticBasicValueType.UInt64, ArithmeticBasicValueType.UInt8), "cvt.u8.u64" },
                { (ArithmeticBasicValueType.UInt64, ArithmeticBasicValueType.UInt16), "cvt.u16.u64" },
                { (ArithmeticBasicValueType.UInt64, ArithmeticBasicValueType.UInt32), "cvt.u32.u64" },
                { (ArithmeticBasicValueType.UInt64, ArithmeticBasicValueType.Float32), "cvt.rn.f32.u64" },
                { (ArithmeticBasicValueType.UInt64, ArithmeticBasicValueType.Float64), "cvt.rn.f64.u64" },

                { (ArithmeticBasicValueType.Float32, ArithmeticBasicValueType.Int8), "cvt.rzi.s8.f32" },
                { (ArithmeticBasicValueType.Float32, ArithmeticBasicValueType.Int16), "cvt.rzi.s16.f32" },
                { (ArithmeticBasicValueType.Float32, ArithmeticBasicValueType.Int32), "cvt.rzi.s32.f32" },
                { (ArithmeticBasicValueType.Float32, ArithmeticBasicValueType.Int64), "cvt.rzi.s64.f32" },
                { (ArithmeticBasicValueType.Float32, ArithmeticBasicValueType.UInt8), "cvt.rzi.u8.f32" },
                { (ArithmeticBasicValueType.Float32, ArithmeticBasicValueType.UInt16), "cvt.rzi.u16.f32" },
                { (ArithmeticBasicValueType.Float32, ArithmeticBasicValueType.UInt32), "cvt.rzi.u32.f32" },
                { (ArithmeticBasicValueType.Float32, ArithmeticBasicValueType.UInt64), "cvt.rzi.u64.f32" },
                { (ArithmeticBasicValueType.Float32, ArithmeticBasicValueType.Float64), "cvt.f64.f32" },

                { (ArithmeticBasicValueType.Float64, ArithmeticBasicValueType.Int8), "cvt.rzi.s8.f64" },
                { (ArithmeticBasicValueType.Float64, ArithmeticBasicValueType.Int16), "cvt.rzi.s16.f64" },
                { (ArithmeticBasicValueType.Float64, ArithmeticBasicValueType.Int32), "cvt.rzi.s32.f64" },
                { (ArithmeticBasicValueType.Float64, ArithmeticBasicValueType.Int64), "cvt.rzi.s64.f64" },
                { (ArithmeticBasicValueType.Float64, ArithmeticBasicValueType.UInt8), "cvt.rzi.u8.f64" },
                { (ArithmeticBasicValueType.Float64, ArithmeticBasicValueType.UInt16), "cvt.rzi.u16.f64" },
                { (ArithmeticBasicValueType.Float64, ArithmeticBasicValueType.UInt32), "cvt.rzi.u32.f64" },
                { (ArithmeticBasicValueType.Float64, ArithmeticBasicValueType.UInt64), "cvt.rzi.u64.f64" },
                { (ArithmeticBasicValueType.Float64, ArithmeticBasicValueType.Float32), "cvt.rn.f32.f64" },
            };

        private static readonly Dictionary<(UnaryArithmeticKind, ArithmeticBasicValueType), string> UnaryArithmeticOperations =
            new Dictionary<(UnaryArithmeticKind, ArithmeticBasicValueType), string>()
            {
                // Basic arithmetic
                
                { (UnaryArithmeticKind.Neg, ArithmeticBasicValueType.Int16), "neg.s16" },
                { (UnaryArithmeticKind.Neg, ArithmeticBasicValueType.Int32), "neg.s32" },
                { (UnaryArithmeticKind.Neg, ArithmeticBasicValueType.Int64), "neg.s64" },
                { (UnaryArithmeticKind.Neg, ArithmeticBasicValueType.Float32), "neg.f32" },
                { (UnaryArithmeticKind.Neg, ArithmeticBasicValueType.Float64), "neg.f64" },

                { (UnaryArithmeticKind.Not, ArithmeticBasicValueType.UInt1), "not.pred" },
                { (UnaryArithmeticKind.Not, ArithmeticBasicValueType.Int16), "not.b16" },
                { (UnaryArithmeticKind.Not, ArithmeticBasicValueType.Int32), "not.b32" },
                { (UnaryArithmeticKind.Not, ArithmeticBasicValueType.Int64), "not.b64" },
                { (UnaryArithmeticKind.Not, ArithmeticBasicValueType.UInt16), "not.b16" },
                { (UnaryArithmeticKind.Not, ArithmeticBasicValueType.UInt32), "not.b32" },
                { (UnaryArithmeticKind.Not, ArithmeticBasicValueType.UInt64), "not.b64" },
                { (UnaryArithmeticKind.Not, ArithmeticBasicValueType.Float32), "not.b32" },
                { (UnaryArithmeticKind.Not, ArithmeticBasicValueType.Float64), "not.b64" },

                // Functions

                { (UnaryArithmeticKind.Abs, ArithmeticBasicValueType.Int16), "abs.s16" },
                { (UnaryArithmeticKind.Abs, ArithmeticBasicValueType.Int32), "abs.s32" },
                { (UnaryArithmeticKind.Abs, ArithmeticBasicValueType.Int64), "abs.s64" },
                { (UnaryArithmeticKind.Abs, ArithmeticBasicValueType.Float32), "abs.f32" },
                { (UnaryArithmeticKind.Abs, ArithmeticBasicValueType.Float64), "abs.f64" },

                { (UnaryArithmeticKind.RcpF, ArithmeticBasicValueType.Float32), "rcp.rn.f32" },
                { (UnaryArithmeticKind.RcpF, ArithmeticBasicValueType.Float64), "rcp.rn.f64" },

                { (UnaryArithmeticKind.IsNaNF, ArithmeticBasicValueType.Float32), "testp.notanumber.f32" },
                { (UnaryArithmeticKind.IsNaNF, ArithmeticBasicValueType.Float64), "testp.notanumber.f64" },

                { (UnaryArithmeticKind.IsInfF, ArithmeticBasicValueType.Float32), "testp.infinite.f32" },
                { (UnaryArithmeticKind.IsInfF, ArithmeticBasicValueType.Float64), "testp.infinite.f64" },

                { (UnaryArithmeticKind.SqrtF, ArithmeticBasicValueType.Float32), "sqrt.rn.f32" },
                { (UnaryArithmeticKind.SqrtF, ArithmeticBasicValueType.Float64), "sqrt.rn.f64" },

                { (UnaryArithmeticKind.RsqrtF, ArithmeticBasicValueType.Float32), "rsqrt.approx.f32" },
                { (UnaryArithmeticKind.RsqrtF, ArithmeticBasicValueType.Float64), "rsqrt.approx.f64" },

                { (UnaryArithmeticKind.SinF, ArithmeticBasicValueType.Float32), "sin.approx.f32" },
                { (UnaryArithmeticKind.CosF, ArithmeticBasicValueType.Float32), "cos.approx.f32" },

                { (UnaryArithmeticKind.Log2F, ArithmeticBasicValueType.Float32), "lg2.approx.f32" },
                { (UnaryArithmeticKind.Exp2F, ArithmeticBasicValueType.Float32), "ex2.approx.f32" },

                { (UnaryArithmeticKind.FloorF, ArithmeticBasicValueType.Float32), "cvt.rmi.f32.f32" },
                { (UnaryArithmeticKind.FloorF, ArithmeticBasicValueType.Float64), "cvt.rmi.f64.f64" },

                { (UnaryArithmeticKind.CeilingF, ArithmeticBasicValueType.Float32), "cvt.rpi.f32.f32" },
                { (UnaryArithmeticKind.CeilingF, ArithmeticBasicValueType.Float64), "cvt.rpi.f64.f64" },
            };

        private static readonly Dictionary<(UnaryArithmeticKind, ArithmeticBasicValueType), string> UnaryArithmeticOperationsFastMath =
            new Dictionary<(UnaryArithmeticKind, ArithmeticBasicValueType), string>()
            {
                { (UnaryArithmeticKind.RcpF, ArithmeticBasicValueType.Float32), "rcp.approx.ftz.f32" },
                { (UnaryArithmeticKind.RcpF, ArithmeticBasicValueType.Float64), "rcp.approx.ftz.f64" },

                { (UnaryArithmeticKind.SinF, ArithmeticBasicValueType.Float32), "sin.approx.ftz.f32" },
                { (UnaryArithmeticKind.CosF, ArithmeticBasicValueType.Float32), "cos.approx.ftz.f32" },

                { (UnaryArithmeticKind.Log2F, ArithmeticBasicValueType.Float32), "lg2.approx.ftz.f32" },
                { (UnaryArithmeticKind.Exp2F, ArithmeticBasicValueType.Float32), "ex2.approx.ftz.f32" },

                { (UnaryArithmeticKind.SqrtF, ArithmeticBasicValueType.Float32), "sqrt.approx.ftz.f32" },

                { (UnaryArithmeticKind.RsqrtF, ArithmeticBasicValueType.Float32), "rsqrt.approx.ftz.f32" },
                { (UnaryArithmeticKind.RsqrtF, ArithmeticBasicValueType.Float64), "rsqrt.approx.ftz.f64" },
            };

        private static readonly Dictionary<(BinaryArithmeticKind, ArithmeticBasicValueType), string> BinaryArithmeticOperations =
            new Dictionary<(BinaryArithmeticKind, ArithmeticBasicValueType), string>()
            {
                // Basic arithmetic
                
                { (BinaryArithmeticKind.Add, ArithmeticBasicValueType.Int16), "add.s16" },
                { (BinaryArithmeticKind.Add, ArithmeticBasicValueType.Int32), "add.s32" },
                { (BinaryArithmeticKind.Add, ArithmeticBasicValueType.Int64), "add.s64" },
                { (BinaryArithmeticKind.Add, ArithmeticBasicValueType.UInt16), "add.u16" },
                { (BinaryArithmeticKind.Add, ArithmeticBasicValueType.UInt32), "add.u32" },
                { (BinaryArithmeticKind.Add, ArithmeticBasicValueType.UInt64), "add.u64" },
                { (BinaryArithmeticKind.Add, ArithmeticBasicValueType.Float32), "add.f32" },
                { (BinaryArithmeticKind.Add, ArithmeticBasicValueType.Float64), "add.f64" },

                { (BinaryArithmeticKind.Sub, ArithmeticBasicValueType.Int16), "sub.s16" },
                { (BinaryArithmeticKind.Sub, ArithmeticBasicValueType.Int32), "sub.s32" },
                { (BinaryArithmeticKind.Sub, ArithmeticBasicValueType.Int64), "sub.s64" },
                { (BinaryArithmeticKind.Sub, ArithmeticBasicValueType.UInt16), "sub.u16" },
                { (BinaryArithmeticKind.Sub, ArithmeticBasicValueType.UInt32), "sub.u32" },
                { (BinaryArithmeticKind.Sub, ArithmeticBasicValueType.UInt64), "sub.u64" },
                { (BinaryArithmeticKind.Sub, ArithmeticBasicValueType.Float32), "sub.f32" },
                { (BinaryArithmeticKind.Sub, ArithmeticBasicValueType.Float64), "sub.f64" },

                { (BinaryArithmeticKind.Mul, ArithmeticBasicValueType.Int16), "mul.lo.s16" },
                { (BinaryArithmeticKind.Mul, ArithmeticBasicValueType.Int32), "mul.lo.s32" },
                { (BinaryArithmeticKind.Mul, ArithmeticBasicValueType.Int64), "mul.lo.s64" },
                { (BinaryArithmeticKind.Mul, ArithmeticBasicValueType.UInt16), "mul.lo.u16" },
                { (BinaryArithmeticKind.Mul, ArithmeticBasicValueType.UInt32), "mul.lo.u32" },
                { (BinaryArithmeticKind.Mul, ArithmeticBasicValueType.UInt64), "mul.lo.u64" },
                { (BinaryArithmeticKind.Mul, ArithmeticBasicValueType.Float32), "mul.f32" },
                { (BinaryArithmeticKind.Mul, ArithmeticBasicValueType.Float64), "mul.f64" },

                { (BinaryArithmeticKind.Div, ArithmeticBasicValueType.Int16), "div.s16" },
                { (BinaryArithmeticKind.Div, ArithmeticBasicValueType.Int32), "div.s32" },
                { (BinaryArithmeticKind.Div, ArithmeticBasicValueType.Int64), "div.s64" },
                { (BinaryArithmeticKind.Div, ArithmeticBasicValueType.UInt16), "div.u16" },
                { (BinaryArithmeticKind.Div, ArithmeticBasicValueType.UInt32), "div.u32" },
                { (BinaryArithmeticKind.Div, ArithmeticBasicValueType.UInt64), "div.u64" },
                { (BinaryArithmeticKind.Div, ArithmeticBasicValueType.Float32), "div.f32" },
                { (BinaryArithmeticKind.Div, ArithmeticBasicValueType.Float64), "div.f64" },

                { (BinaryArithmeticKind.Rem, ArithmeticBasicValueType.Int16), "rem.s16" },
                { (BinaryArithmeticKind.Rem, ArithmeticBasicValueType.Int32), "rem.s32" },
                { (BinaryArithmeticKind.Rem, ArithmeticBasicValueType.Int64), "rem.s64" },
                { (BinaryArithmeticKind.Rem, ArithmeticBasicValueType.UInt16), "rem.u16" },
                { (BinaryArithmeticKind.Rem, ArithmeticBasicValueType.UInt32), "rem.u32" },
                { (BinaryArithmeticKind.Rem, ArithmeticBasicValueType.UInt64), "rem.u64" },

                // Logic

                { (BinaryArithmeticKind.And, ArithmeticBasicValueType.UInt1), "and.pred" },
                { (BinaryArithmeticKind.And, ArithmeticBasicValueType.Int16), "and.b16" },
                { (BinaryArithmeticKind.And, ArithmeticBasicValueType.Int32), "and.b32" },
                { (BinaryArithmeticKind.And, ArithmeticBasicValueType.Int64), "and.b64" },
                { (BinaryArithmeticKind.And, ArithmeticBasicValueType.UInt16), "and.b16" },
                { (BinaryArithmeticKind.And, ArithmeticBasicValueType.UInt32), "and.b32" },
                { (BinaryArithmeticKind.And, ArithmeticBasicValueType.UInt64), "and.b64" },
                { (BinaryArithmeticKind.And, ArithmeticBasicValueType.Float32), "and.b32" },
                { (BinaryArithmeticKind.And, ArithmeticBasicValueType.Float64), "and.b64" },

                { (BinaryArithmeticKind.Or, ArithmeticBasicValueType.UInt1), "or.pred" },
                { (BinaryArithmeticKind.Or, ArithmeticBasicValueType.Int16), "or.b16" },
                { (BinaryArithmeticKind.Or, ArithmeticBasicValueType.Int32), "or.b32" },
                { (BinaryArithmeticKind.Or, ArithmeticBasicValueType.Int64), "or.b64" },
                { (BinaryArithmeticKind.Or, ArithmeticBasicValueType.UInt16), "or.b16" },
                { (BinaryArithmeticKind.Or, ArithmeticBasicValueType.UInt32), "or.b32" },
                { (BinaryArithmeticKind.Or, ArithmeticBasicValueType.UInt64), "or.b64" },
                { (BinaryArithmeticKind.Or, ArithmeticBasicValueType.Float32), "or.b32" },
                { (BinaryArithmeticKind.Or, ArithmeticBasicValueType.Float64), "or.b64" },

                { (BinaryArithmeticKind.Xor, ArithmeticBasicValueType.UInt1), "xor.pred" },
                { (BinaryArithmeticKind.Xor, ArithmeticBasicValueType.Int16), "xor.b16" },
                { (BinaryArithmeticKind.Xor, ArithmeticBasicValueType.Int32), "xor.b32" },
                { (BinaryArithmeticKind.Xor, ArithmeticBasicValueType.Int64), "xor.b64" },
                { (BinaryArithmeticKind.Xor, ArithmeticBasicValueType.UInt16), "xor.b16" },
                { (BinaryArithmeticKind.Xor, ArithmeticBasicValueType.UInt32), "xor.b32" },
                { (BinaryArithmeticKind.Xor, ArithmeticBasicValueType.UInt64), "xor.b64" },
                { (BinaryArithmeticKind.Xor, ArithmeticBasicValueType.Float32), "xor.b32" },
                { (BinaryArithmeticKind.Xor, ArithmeticBasicValueType.Float64), "xor.b64" },

                { (BinaryArithmeticKind.Shl, ArithmeticBasicValueType.Int16), "shl.b16" },
                { (BinaryArithmeticKind.Shl, ArithmeticBasicValueType.Int32), "shl.b32" },
                { (BinaryArithmeticKind.Shl, ArithmeticBasicValueType.Int64), "shl.b64" },
                { (BinaryArithmeticKind.Shl, ArithmeticBasicValueType.UInt16), "shl.b16" },
                { (BinaryArithmeticKind.Shl, ArithmeticBasicValueType.UInt32), "shl.b32" },
                { (BinaryArithmeticKind.Shl, ArithmeticBasicValueType.UInt64), "shl.b64" },
                { (BinaryArithmeticKind.Shl, ArithmeticBasicValueType.Float32), "shl.b32" },
                { (BinaryArithmeticKind.Shl, ArithmeticBasicValueType.Float64), "shl.b64" },

                { (BinaryArithmeticKind.Shr, ArithmeticBasicValueType.Int16), "shr.s16" },
                { (BinaryArithmeticKind.Shr, ArithmeticBasicValueType.Int32), "shr.s32" },
                { (BinaryArithmeticKind.Shr, ArithmeticBasicValueType.Int64), "shr.s64" },
                { (BinaryArithmeticKind.Shr, ArithmeticBasicValueType.UInt16), "shr.u16" },
                { (BinaryArithmeticKind.Shr, ArithmeticBasicValueType.UInt32), "shr.u32" },
                { (BinaryArithmeticKind.Shr, ArithmeticBasicValueType.UInt64), "shr.u64" },
                { (BinaryArithmeticKind.Shr, ArithmeticBasicValueType.Float32), "shr.b32" },
                { (BinaryArithmeticKind.Shr, ArithmeticBasicValueType.Float64), "shr.b64" },

                // Functions

                { (BinaryArithmeticKind.Max, ArithmeticBasicValueType.Int16), "max.s16" },
                { (BinaryArithmeticKind.Max, ArithmeticBasicValueType.Int32), "max.s32" },
                { (BinaryArithmeticKind.Max, ArithmeticBasicValueType.Int64), "max.s64" },
                { (BinaryArithmeticKind.Max, ArithmeticBasicValueType.UInt16), "max.u16" },
                { (BinaryArithmeticKind.Max, ArithmeticBasicValueType.UInt32), "max.u32" },
                { (BinaryArithmeticKind.Max, ArithmeticBasicValueType.UInt64), "max.u64" },
                { (BinaryArithmeticKind.Max, ArithmeticBasicValueType.Float32), "max.f32" },
                { (BinaryArithmeticKind.Max, ArithmeticBasicValueType.Float64), "max.f64" },

                { (BinaryArithmeticKind.Min, ArithmeticBasicValueType.Int16), "min.s16" },
                { (BinaryArithmeticKind.Min, ArithmeticBasicValueType.Int32), "min.s32" },
                { (BinaryArithmeticKind.Min, ArithmeticBasicValueType.Int64), "min.s64" },
                { (BinaryArithmeticKind.Min, ArithmeticBasicValueType.UInt16), "min.u16" },
                { (BinaryArithmeticKind.Min, ArithmeticBasicValueType.UInt32), "min.u32" },
                { (BinaryArithmeticKind.Min, ArithmeticBasicValueType.UInt64), "min.u64" },
                { (BinaryArithmeticKind.Min, ArithmeticBasicValueType.Float32), "min.f32" },
                { (BinaryArithmeticKind.Min, ArithmeticBasicValueType.Float64), "min.f64" },
            };

        private static readonly Dictionary<(BinaryArithmeticKind, ArithmeticBasicValueType), string> BinaryArithmeticOperationsFastMath =
            new Dictionary<(BinaryArithmeticKind, ArithmeticBasicValueType), string>()
            {
                // Basic arithmetic
                
                { (BinaryArithmeticKind.Add, ArithmeticBasicValueType.Float32), "add.ftz.f32" },
                { (BinaryArithmeticKind.Sub, ArithmeticBasicValueType.Float32), "sub.ftz.f32" },
                { (BinaryArithmeticKind.Mul, ArithmeticBasicValueType.Float32), "mul.ftz.f32" },
                { (BinaryArithmeticKind.Div, ArithmeticBasicValueType.Float32), "div.approx.ftz.f32" },

                // Functions

                { (BinaryArithmeticKind.Max, ArithmeticBasicValueType.Float32), "max.ftz.f32" },
                { (BinaryArithmeticKind.Min, ArithmeticBasicValueType.Float32), "min.ftz.f32" },
            };

        private static readonly Dictionary<(TernaryArithmeticKind, ArithmeticBasicValueType), string> TernaryArithmeticOperations =
            new Dictionary<(TernaryArithmeticKind, ArithmeticBasicValueType), string>()
            {
                { (TernaryArithmeticKind.MultiplyAdd, ArithmeticBasicValueType.Int16), "mad.lo.s32" },
                { (TernaryArithmeticKind.MultiplyAdd, ArithmeticBasicValueType.Int32), "mad.lo.s32" },
                { (TernaryArithmeticKind.MultiplyAdd, ArithmeticBasicValueType.Int64), "mad.lo.s64" },

                { (TernaryArithmeticKind.MultiplyAdd, ArithmeticBasicValueType.UInt16), "mad.lo.u32" },
                { (TernaryArithmeticKind.MultiplyAdd, ArithmeticBasicValueType.UInt32), "mad.lo.u32" },
                { (TernaryArithmeticKind.MultiplyAdd, ArithmeticBasicValueType.UInt64), "mad.lo.u64" },

                { (TernaryArithmeticKind.MultiplyAdd, ArithmeticBasicValueType.Float32), "fma.rn.f32" },
                { (TernaryArithmeticKind.MultiplyAdd, ArithmeticBasicValueType.Float64), "fma.rn.f64" },
            };

        private static readonly Dictionary<(AtomicKind, bool), string> AtomicOperations =
            new Dictionary<(AtomicKind, bool), string>()
            {
                { (AtomicKind.Add, false), "red.add" },
                { (AtomicKind.And, false), "red.and" },
                { (AtomicKind.Or, false), "red.or" },
                { (AtomicKind.Xor, false), "red.xor" },
                { (AtomicKind.Max, false), "red.max" },
                { (AtomicKind.Min, false), "red.min" },

                { (AtomicKind.Exchange, true), "atom.exch" },
                { (AtomicKind.Add, true), "atom.add" },
                { (AtomicKind.And, true), "atom.and" },
                { (AtomicKind.Or, true), "atom.or" },
                { (AtomicKind.Xor, true), "atom.xor" },
                { (AtomicKind.Max, true), "atom.max" },
                { (AtomicKind.Min, true), "atom.min" },
            };

        private static readonly Dictionary<(AtomicKind, ArithmeticBasicValueType), string> AtomicOperationsTypes =
            new Dictionary<(AtomicKind, ArithmeticBasicValueType), string>()
            {
                { (AtomicKind.Exchange, ArithmeticBasicValueType.Int32), "b32" },
                { (AtomicKind.Exchange, ArithmeticBasicValueType.Int64), "b64" },
                { (AtomicKind.Exchange, ArithmeticBasicValueType.UInt32), "b32" },
                { (AtomicKind.Exchange, ArithmeticBasicValueType.UInt64), "b64" },
                { (AtomicKind.Exchange, ArithmeticBasicValueType.Float32), "b32" },
                { (AtomicKind.Exchange, ArithmeticBasicValueType.Float64), "b64" },

                { (AtomicKind.Add, ArithmeticBasicValueType.Int32), "u32" },
                { (AtomicKind.Add, ArithmeticBasicValueType.Int64), "u64" },
                { (AtomicKind.Add, ArithmeticBasicValueType.UInt32), "u32" },
                { (AtomicKind.Add, ArithmeticBasicValueType.UInt64), "u64" },
                { (AtomicKind.Add, ArithmeticBasicValueType.Float32), "f32" },
                { (AtomicKind.Add, ArithmeticBasicValueType.Float64), "f64" },

                { (AtomicKind.And, ArithmeticBasicValueType.Int32), "b32" },
                { (AtomicKind.And, ArithmeticBasicValueType.Int64), "b64" },
                { (AtomicKind.And, ArithmeticBasicValueType.UInt32), "b32" },
                { (AtomicKind.And, ArithmeticBasicValueType.UInt64), "b64" },
                { (AtomicKind.And, ArithmeticBasicValueType.Float32), "b32" },
                { (AtomicKind.And, ArithmeticBasicValueType.Float64), "b64" },

                { (AtomicKind.Or, ArithmeticBasicValueType.Int32), "b32" },
                { (AtomicKind.Or, ArithmeticBasicValueType.Int64), "b64" },
                { (AtomicKind.Or, ArithmeticBasicValueType.UInt32), "b32" },
                { (AtomicKind.Or, ArithmeticBasicValueType.UInt64), "b64" },
                { (AtomicKind.Or, ArithmeticBasicValueType.Float32), "b32" },
                { (AtomicKind.Or, ArithmeticBasicValueType.Float64), "b64" },

                { (AtomicKind.Xor, ArithmeticBasicValueType.Int32), "b32" },
                { (AtomicKind.Xor, ArithmeticBasicValueType.Int64), "b64" },
                { (AtomicKind.Xor, ArithmeticBasicValueType.UInt32), "b32" },
                { (AtomicKind.Xor, ArithmeticBasicValueType.UInt64), "b64" },
                { (AtomicKind.Xor, ArithmeticBasicValueType.Float32), "b32" },
                { (AtomicKind.Xor, ArithmeticBasicValueType.Float64), "b64" },

                { (AtomicKind.Min, ArithmeticBasicValueType.Int32), "s32" },
                { (AtomicKind.Min, ArithmeticBasicValueType.Int64), "s64" },
                { (AtomicKind.Min, ArithmeticBasicValueType.UInt32), "u32" },
                { (AtomicKind.Min, ArithmeticBasicValueType.UInt64), "u64" },

                { (AtomicKind.Max, ArithmeticBasicValueType.Int32), "s32" },
                { (AtomicKind.Max, ArithmeticBasicValueType.Int64), "s64" },
                { (AtomicKind.Max, ArithmeticBasicValueType.UInt32), "u32" },
                { (AtomicKind.Max, ArithmeticBasicValueType.UInt64), "u64" },
            };
    }
}

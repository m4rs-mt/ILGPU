// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: SPIRVTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

// disable: max_line_length

namespace ILGPU.Backends.SPIRV.Types
{
    internal struct ImageOperands : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public ImageOperands(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly ImageOperands None =
            new ImageOperands(0, "None");

        public static readonly ImageOperands Bias =
            new ImageOperands(1, "Bias");

        public static readonly ImageOperands Lod =
            new ImageOperands(2, "Lod");

        public static readonly ImageOperands Grad =
            new ImageOperands(4, "Grad");

        public static readonly ImageOperands ConstOffset =
            new ImageOperands(8, "ConstOffset");

        public static readonly ImageOperands Offset =
            new ImageOperands(16, "Offset");

        public static readonly ImageOperands ConstOffsets =
            new ImageOperands(32, "ConstOffsets");

        public static readonly ImageOperands Sample =
            new ImageOperands(64, "Sample");

        public static readonly ImageOperands MinLod =
            new ImageOperands(128, "MinLod");

        public static readonly ImageOperands MakeTexelAvailable =
            new ImageOperands(256, "MakeTexelAvailable");

        public static readonly ImageOperands MakeTexelAvailableKHR =
            new ImageOperands(256, "MakeTexelAvailableKHR");

        public static readonly ImageOperands MakeTexelVisible =
            new ImageOperands(512, "MakeTexelVisible");

        public static readonly ImageOperands MakeTexelVisibleKHR =
            new ImageOperands(512, "MakeTexelVisibleKHR");

        public static readonly ImageOperands NonPrivateTexel =
            new ImageOperands(1024, "NonPrivateTexel");

        public static readonly ImageOperands NonPrivateTexelKHR =
            new ImageOperands(1024, "NonPrivateTexelKHR");

        public static readonly ImageOperands VolatileTexel =
            new ImageOperands(2048, "VolatileTexel");

        public static readonly ImageOperands VolatileTexelKHR =
            new ImageOperands(2048, "VolatileTexelKHR");

        public static readonly ImageOperands SignExtend =
            new ImageOperands(4096, "SignExtend");

        public static readonly ImageOperands ZeroExtend =
            new ImageOperands(8192, "ZeroExtend");

        public static readonly ImageOperands Nontemporal =
            new ImageOperands(16384, "Nontemporal");

        public static readonly ImageOperands Offsets =
            new ImageOperands(65536, "Offsets");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct FPFastMathMode : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public FPFastMathMode(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly FPFastMathMode None =
            new FPFastMathMode(0, "None");

        public static readonly FPFastMathMode NotNaN =
            new FPFastMathMode(1, "NotNaN");

        public static readonly FPFastMathMode NotInf =
            new FPFastMathMode(2, "NotInf");

        public static readonly FPFastMathMode NSZ =
            new FPFastMathMode(4, "NSZ");

        public static readonly FPFastMathMode AllowRecip =
            new FPFastMathMode(8, "AllowRecip");

        public static readonly FPFastMathMode Fast =
            new FPFastMathMode(16, "Fast");

        public static readonly FPFastMathMode AllowContractFastINTEL =
            new FPFastMathMode(65536, "AllowContractFastINTEL");

        public static readonly FPFastMathMode AllowReassocINTEL =
            new FPFastMathMode(131072, "AllowReassocINTEL");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct SelectionControl : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public SelectionControl(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly SelectionControl None =
            new SelectionControl(0, "None");

        public static readonly SelectionControl Flatten =
            new SelectionControl(1, "Flatten");

        public static readonly SelectionControl DontFlatten =
            new SelectionControl(2, "DontFlatten");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct LoopControl : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public LoopControl(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly LoopControl None =
            new LoopControl(0, "None");

        public static readonly LoopControl Unroll =
            new LoopControl(1, "Unroll");

        public static readonly LoopControl DontUnroll =
            new LoopControl(2, "DontUnroll");

        public static readonly LoopControl DependencyInfinite =
            new LoopControl(4, "DependencyInfinite");

        public static readonly LoopControl DependencyLength =
            new LoopControl(8, "DependencyLength");

        public static readonly LoopControl MinIterations =
            new LoopControl(16, "MinIterations");

        public static readonly LoopControl MaxIterations =
            new LoopControl(32, "MaxIterations");

        public static readonly LoopControl IterationMultiple =
            new LoopControl(64, "IterationMultiple");

        public static readonly LoopControl PeelCount =
            new LoopControl(128, "PeelCount");

        public static readonly LoopControl PartialCount =
            new LoopControl(256, "PartialCount");

        public static readonly LoopControl InitiationIntervalINTEL =
            new LoopControl(65536, "InitiationIntervalINTEL");

        public static readonly LoopControl MaxConcurrencyINTEL =
            new LoopControl(131072, "MaxConcurrencyINTEL");

        public static readonly LoopControl DependencyArrayINTEL =
            new LoopControl(262144, "DependencyArrayINTEL");

        public static readonly LoopControl PipelineEnableINTEL =
            new LoopControl(524288, "PipelineEnableINTEL");

        public static readonly LoopControl LoopCoalesceINTEL =
            new LoopControl(1048576, "LoopCoalesceINTEL");

        public static readonly LoopControl MaxInterleavingINTEL =
            new LoopControl(2097152, "MaxInterleavingINTEL");

        public static readonly LoopControl SpeculatedIterationsINTEL =
            new LoopControl(4194304, "SpeculatedIterationsINTEL");

        public static readonly LoopControl NoFusionINTEL =
            new LoopControl(8388608, "NoFusionINTEL");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct FunctionControl : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public FunctionControl(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly FunctionControl None =
            new FunctionControl(0, "None");

        public static readonly FunctionControl Inline =
            new FunctionControl(1, "Inline");

        public static readonly FunctionControl DontInline =
            new FunctionControl(2, "DontInline");

        public static readonly FunctionControl Pure =
            new FunctionControl(4, "Pure");

        public static readonly FunctionControl Const =
            new FunctionControl(8, "Const");

        public static readonly FunctionControl OptNoneINTEL =
            new FunctionControl(65536, "OptNoneINTEL");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct MemorySemantics : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public MemorySemantics(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly MemorySemantics Relaxed =
            new MemorySemantics(0, "Relaxed");

        public static readonly MemorySemantics None =
            new MemorySemantics(0, "None");

        public static readonly MemorySemantics Acquire =
            new MemorySemantics(2, "Acquire");

        public static readonly MemorySemantics Release =
            new MemorySemantics(4, "Release");

        public static readonly MemorySemantics AcquireRelease =
            new MemorySemantics(8, "AcquireRelease");

        public static readonly MemorySemantics SequentiallyConsistent =
            new MemorySemantics(16, "SequentiallyConsistent");

        public static readonly MemorySemantics UniformMemory =
            new MemorySemantics(64, "UniformMemory");

        public static readonly MemorySemantics SubgroupMemory =
            new MemorySemantics(128, "SubgroupMemory");

        public static readonly MemorySemantics WorkgroupMemory =
            new MemorySemantics(256, "WorkgroupMemory");

        public static readonly MemorySemantics CrossWorkgroupMemory =
            new MemorySemantics(512, "CrossWorkgroupMemory");

        public static readonly MemorySemantics AtomicCounterMemory =
            new MemorySemantics(1024, "AtomicCounterMemory");

        public static readonly MemorySemantics ImageMemory =
            new MemorySemantics(2048, "ImageMemory");

        public static readonly MemorySemantics OutputMemory =
            new MemorySemantics(4096, "OutputMemory");

        public static readonly MemorySemantics OutputMemoryKHR =
            new MemorySemantics(4096, "OutputMemoryKHR");

        public static readonly MemorySemantics MakeAvailable =
            new MemorySemantics(8192, "MakeAvailable");

        public static readonly MemorySemantics MakeAvailableKHR =
            new MemorySemantics(8192, "MakeAvailableKHR");

        public static readonly MemorySemantics MakeVisible =
            new MemorySemantics(16384, "MakeVisible");

        public static readonly MemorySemantics MakeVisibleKHR =
            new MemorySemantics(16384, "MakeVisibleKHR");

        public static readonly MemorySemantics Volatile =
            new MemorySemantics(32768, "Volatile");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct MemoryAccess : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public MemoryAccess(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly MemoryAccess None =
            new MemoryAccess(0, "None");

        public static readonly MemoryAccess Volatile =
            new MemoryAccess(1, "Volatile");

        public static readonly MemoryAccess Aligned =
            new MemoryAccess(2, "Aligned");

        public static readonly MemoryAccess Nontemporal =
            new MemoryAccess(4, "Nontemporal");

        public static readonly MemoryAccess MakePointerAvailable =
            new MemoryAccess(8, "MakePointerAvailable");

        public static readonly MemoryAccess MakePointerAvailableKHR =
            new MemoryAccess(8, "MakePointerAvailableKHR");

        public static readonly MemoryAccess MakePointerVisible =
            new MemoryAccess(16, "MakePointerVisible");

        public static readonly MemoryAccess MakePointerVisibleKHR =
            new MemoryAccess(16, "MakePointerVisibleKHR");

        public static readonly MemoryAccess NonPrivatePointer =
            new MemoryAccess(32, "NonPrivatePointer");

        public static readonly MemoryAccess NonPrivatePointerKHR =
            new MemoryAccess(32, "NonPrivatePointerKHR");

        public static readonly MemoryAccess AliasScopeINTELMask =
            new MemoryAccess(65536, "AliasScopeINTELMask");

        public static readonly MemoryAccess NoAliasINTELMask =
            new MemoryAccess(131072, "NoAliasINTELMask");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct KernelProfilingInfo : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public KernelProfilingInfo(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly KernelProfilingInfo None =
            new KernelProfilingInfo(0, "None");

        public static readonly KernelProfilingInfo CmdExecTime =
            new KernelProfilingInfo(1, "CmdExecTime");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct RayFlags : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public RayFlags(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly RayFlags NoneKHR =
            new RayFlags(0, "NoneKHR");

        public static readonly RayFlags OpaqueKHR =
            new RayFlags(1, "OpaqueKHR");

        public static readonly RayFlags NoOpaqueKHR =
            new RayFlags(2, "NoOpaqueKHR");

        public static readonly RayFlags TerminateOnFirstHitKHR =
            new RayFlags(4, "TerminateOnFirstHitKHR");

        public static readonly RayFlags SkipClosestHitShaderKHR =
            new RayFlags(8, "SkipClosestHitShaderKHR");

        public static readonly RayFlags CullBackFacingTrianglesKHR =
            new RayFlags(16, "CullBackFacingTrianglesKHR");

        public static readonly RayFlags CullFrontFacingTrianglesKHR =
            new RayFlags(32, "CullFrontFacingTrianglesKHR");

        public static readonly RayFlags CullOpaqueKHR =
            new RayFlags(64, "CullOpaqueKHR");

        public static readonly RayFlags CullNoOpaqueKHR =
            new RayFlags(128, "CullNoOpaqueKHR");

        public static readonly RayFlags SkipTrianglesKHR =
            new RayFlags(256, "SkipTrianglesKHR");

        public static readonly RayFlags SkipAABBsKHR =
            new RayFlags(512, "SkipAABBsKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct FragmentShadingRate : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public FragmentShadingRate(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly FragmentShadingRate Vertical2Pixels =
            new FragmentShadingRate(1, "Vertical2Pixels");

        public static readonly FragmentShadingRate Vertical4Pixels =
            new FragmentShadingRate(2, "Vertical4Pixels");

        public static readonly FragmentShadingRate Horizontal2Pixels =
            new FragmentShadingRate(4, "Horizontal2Pixels");

        public static readonly FragmentShadingRate Horizontal4Pixels =
            new FragmentShadingRate(8, "Horizontal4Pixels");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct SourceLanguage : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public SourceLanguage(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly SourceLanguage Unknown =
            new SourceLanguage(0, "Unknown");

        public static readonly SourceLanguage ESSL =
            new SourceLanguage(1, "ESSL");

        public static readonly SourceLanguage GLSL =
            new SourceLanguage(2, "GLSL");

        public static readonly SourceLanguage OpenCL_C =
            new SourceLanguage(3, "OpenCL_C");

        public static readonly SourceLanguage OpenCL_CPP =
            new SourceLanguage(4, "OpenCL_CPP");

        public static readonly SourceLanguage HLSL =
            new SourceLanguage(5, "HLSL");

        public static readonly SourceLanguage CPP_for_OpenCL =
            new SourceLanguage(6, "CPP_for_OpenCL");

        public static readonly SourceLanguage SYCL =
            new SourceLanguage(7, "SYCL");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct ExecutionModel : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public ExecutionModel(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly ExecutionModel Vertex =
            new ExecutionModel(0, "Vertex");

        public static readonly ExecutionModel TessellationControl =
            new ExecutionModel(1, "TessellationControl");

        public static readonly ExecutionModel TessellationEvaluation =
            new ExecutionModel(2, "TessellationEvaluation");

        public static readonly ExecutionModel Geometry =
            new ExecutionModel(3, "Geometry");

        public static readonly ExecutionModel Fragment =
            new ExecutionModel(4, "Fragment");

        public static readonly ExecutionModel GLCompute =
            new ExecutionModel(5, "GLCompute");

        public static readonly ExecutionModel Kernel =
            new ExecutionModel(6, "Kernel");

        public static readonly ExecutionModel TaskNV =
            new ExecutionModel(5267, "TaskNV");

        public static readonly ExecutionModel MeshNV =
            new ExecutionModel(5268, "MeshNV");

        public static readonly ExecutionModel RayGenerationNV =
            new ExecutionModel(5313, "RayGenerationNV");

        public static readonly ExecutionModel RayGenerationKHR =
            new ExecutionModel(5313, "RayGenerationKHR");

        public static readonly ExecutionModel IntersectionNV =
            new ExecutionModel(5314, "IntersectionNV");

        public static readonly ExecutionModel IntersectionKHR =
            new ExecutionModel(5314, "IntersectionKHR");

        public static readonly ExecutionModel AnyHitNV =
            new ExecutionModel(5315, "AnyHitNV");

        public static readonly ExecutionModel AnyHitKHR =
            new ExecutionModel(5315, "AnyHitKHR");

        public static readonly ExecutionModel ClosestHitNV =
            new ExecutionModel(5316, "ClosestHitNV");

        public static readonly ExecutionModel ClosestHitKHR =
            new ExecutionModel(5316, "ClosestHitKHR");

        public static readonly ExecutionModel MissNV =
            new ExecutionModel(5317, "MissNV");

        public static readonly ExecutionModel MissKHR =
            new ExecutionModel(5317, "MissKHR");

        public static readonly ExecutionModel CallableNV =
            new ExecutionModel(5318, "CallableNV");

        public static readonly ExecutionModel CallableKHR =
            new ExecutionModel(5318, "CallableKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct AddressingModel : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public AddressingModel(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly AddressingModel Logical =
            new AddressingModel(0, "Logical");

        public static readonly AddressingModel Physical32 =
            new AddressingModel(1, "Physical32");

        public static readonly AddressingModel Physical64 =
            new AddressingModel(2, "Physical64");

        public static readonly AddressingModel PhysicalStorageBuffer64 =
            new AddressingModel(5348, "PhysicalStorageBuffer64");

        public static readonly AddressingModel PhysicalStorageBuffer64EXT =
            new AddressingModel(5348, "PhysicalStorageBuffer64EXT");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct MemoryModel : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public MemoryModel(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly MemoryModel Simple =
            new MemoryModel(0, "Simple");

        public static readonly MemoryModel GLSL450 =
            new MemoryModel(1, "GLSL450");

        public static readonly MemoryModel OpenCL =
            new MemoryModel(2, "OpenCL");

        public static readonly MemoryModel Vulkan =
            new MemoryModel(3, "Vulkan");

        public static readonly MemoryModel VulkanKHR =
            new MemoryModel(3, "VulkanKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct ExecutionMode : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public ExecutionMode(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly ExecutionMode Invocations =
            new ExecutionMode(0, "Invocations");

        public static readonly ExecutionMode SpacingEqual =
            new ExecutionMode(1, "SpacingEqual");

        public static readonly ExecutionMode SpacingFractionalEven =
            new ExecutionMode(2, "SpacingFractionalEven");

        public static readonly ExecutionMode SpacingFractionalOdd =
            new ExecutionMode(3, "SpacingFractionalOdd");

        public static readonly ExecutionMode VertexOrderCw =
            new ExecutionMode(4, "VertexOrderCw");

        public static readonly ExecutionMode VertexOrderCcw =
            new ExecutionMode(5, "VertexOrderCcw");

        public static readonly ExecutionMode PixelCenterInteger =
            new ExecutionMode(6, "PixelCenterInteger");

        public static readonly ExecutionMode OriginUpperLeft =
            new ExecutionMode(7, "OriginUpperLeft");

        public static readonly ExecutionMode OriginLowerLeft =
            new ExecutionMode(8, "OriginLowerLeft");

        public static readonly ExecutionMode EarlyFragmentTests =
            new ExecutionMode(9, "EarlyFragmentTests");

        public static readonly ExecutionMode PointMode =
            new ExecutionMode(10, "PointMode");

        public static readonly ExecutionMode Xfb =
            new ExecutionMode(11, "Xfb");

        public static readonly ExecutionMode DepthReplacing =
            new ExecutionMode(12, "DepthReplacing");

        public static readonly ExecutionMode DepthGreater =
            new ExecutionMode(14, "DepthGreater");

        public static readonly ExecutionMode DepthLess =
            new ExecutionMode(15, "DepthLess");

        public static readonly ExecutionMode DepthUnchanged =
            new ExecutionMode(16, "DepthUnchanged");

        public static readonly ExecutionMode LocalSize =
            new ExecutionMode(17, "LocalSize");

        public static readonly ExecutionMode LocalSizeHint =
            new ExecutionMode(18, "LocalSizeHint");

        public static readonly ExecutionMode InputPoints =
            new ExecutionMode(19, "InputPoints");

        public static readonly ExecutionMode InputLines =
            new ExecutionMode(20, "InputLines");

        public static readonly ExecutionMode InputLinesAdjacency =
            new ExecutionMode(21, "InputLinesAdjacency");

        public static readonly ExecutionMode Triangles =
            new ExecutionMode(22, "Triangles");

        public static readonly ExecutionMode InputTrianglesAdjacency =
            new ExecutionMode(23, "InputTrianglesAdjacency");

        public static readonly ExecutionMode Quads =
            new ExecutionMode(24, "Quads");

        public static readonly ExecutionMode Isolines =
            new ExecutionMode(25, "Isolines");

        public static readonly ExecutionMode OutputVertices =
            new ExecutionMode(26, "OutputVertices");

        public static readonly ExecutionMode OutputPoints =
            new ExecutionMode(27, "OutputPoints");

        public static readonly ExecutionMode OutputLineStrip =
            new ExecutionMode(28, "OutputLineStrip");

        public static readonly ExecutionMode OutputTriangleStrip =
            new ExecutionMode(29, "OutputTriangleStrip");

        public static readonly ExecutionMode VecTypeHint =
            new ExecutionMode(30, "VecTypeHint");

        public static readonly ExecutionMode ContractionOff =
            new ExecutionMode(31, "ContractionOff");

        public static readonly ExecutionMode Initializer =
            new ExecutionMode(33, "Initializer");

        public static readonly ExecutionMode Finalizer =
            new ExecutionMode(34, "Finalizer");

        public static readonly ExecutionMode SubgroupSize =
            new ExecutionMode(35, "SubgroupSize");

        public static readonly ExecutionMode SubgroupsPerWorkgroup =
            new ExecutionMode(36, "SubgroupsPerWorkgroup");

        public static readonly ExecutionMode SubgroupsPerWorkgroupId =
            new ExecutionMode(37, "SubgroupsPerWorkgroupId");

        public static readonly ExecutionMode LocalSizeId =
            new ExecutionMode(38, "LocalSizeId");

        public static readonly ExecutionMode LocalSizeHintId =
            new ExecutionMode(39, "LocalSizeHintId");

        public static readonly ExecutionMode SubgroupUniformControlFlowKHR =
            new ExecutionMode(4421, "SubgroupUniformControlFlowKHR");

        public static readonly ExecutionMode PostDepthCoverage =
            new ExecutionMode(4446, "PostDepthCoverage");

        public static readonly ExecutionMode DenormPreserve =
            new ExecutionMode(4459, "DenormPreserve");

        public static readonly ExecutionMode DenormFlushToZero =
            new ExecutionMode(4460, "DenormFlushToZero");

        public static readonly ExecutionMode SignedZeroInfNanPreserve =
            new ExecutionMode(4461, "SignedZeroInfNanPreserve");

        public static readonly ExecutionMode RoundingModeRTE =
            new ExecutionMode(4462, "RoundingModeRTE");

        public static readonly ExecutionMode RoundingModeRTZ =
            new ExecutionMode(4463, "RoundingModeRTZ");

        public static readonly ExecutionMode StencilRefReplacingEXT =
            new ExecutionMode(5027, "StencilRefReplacingEXT");

        public static readonly ExecutionMode OutputLinesNV =
            new ExecutionMode(5269, "OutputLinesNV");

        public static readonly ExecutionMode OutputPrimitivesNV =
            new ExecutionMode(5270, "OutputPrimitivesNV");

        public static readonly ExecutionMode DerivativeGroupQuadsNV =
            new ExecutionMode(5289, "DerivativeGroupQuadsNV");

        public static readonly ExecutionMode DerivativeGroupLinearNV =
            new ExecutionMode(5290, "DerivativeGroupLinearNV");

        public static readonly ExecutionMode OutputTrianglesNV =
            new ExecutionMode(5298, "OutputTrianglesNV");

        public static readonly ExecutionMode PixelInterlockOrderedEXT =
            new ExecutionMode(5366, "PixelInterlockOrderedEXT");

        public static readonly ExecutionMode PixelInterlockUnorderedEXT =
            new ExecutionMode(5367, "PixelInterlockUnorderedEXT");

        public static readonly ExecutionMode SampleInterlockOrderedEXT =
            new ExecutionMode(5368, "SampleInterlockOrderedEXT");

        public static readonly ExecutionMode SampleInterlockUnorderedEXT =
            new ExecutionMode(5369, "SampleInterlockUnorderedEXT");

        public static readonly ExecutionMode ShadingRateInterlockOrderedEXT =
            new ExecutionMode(5370, "ShadingRateInterlockOrderedEXT");

        public static readonly ExecutionMode ShadingRateInterlockUnorderedEXT =
            new ExecutionMode(5371, "ShadingRateInterlockUnorderedEXT");

        public static readonly ExecutionMode SharedLocalMemorySizeINTEL =
            new ExecutionMode(5618, "SharedLocalMemorySizeINTEL");

        public static readonly ExecutionMode RoundingModeRTPINTEL =
            new ExecutionMode(5620, "RoundingModeRTPINTEL");

        public static readonly ExecutionMode RoundingModeRTNINTEL =
            new ExecutionMode(5621, "RoundingModeRTNINTEL");

        public static readonly ExecutionMode FloatingPointModeALTINTEL =
            new ExecutionMode(5622, "FloatingPointModeALTINTEL");

        public static readonly ExecutionMode FloatingPointModeIEEEINTEL =
            new ExecutionMode(5623, "FloatingPointModeIEEEINTEL");

        public static readonly ExecutionMode MaxWorkgroupSizeINTEL =
            new ExecutionMode(5893, "MaxWorkgroupSizeINTEL");

        public static readonly ExecutionMode MaxWorkDimINTEL =
            new ExecutionMode(5894, "MaxWorkDimINTEL");

        public static readonly ExecutionMode NoGlobalOffsetINTEL =
            new ExecutionMode(5895, "NoGlobalOffsetINTEL");

        public static readonly ExecutionMode NumSIMDWorkitemsINTEL =
            new ExecutionMode(5896, "NumSIMDWorkitemsINTEL");

        public static readonly ExecutionMode SchedulerTargetFmaxMhzINTEL =
            new ExecutionMode(5903, "SchedulerTargetFmaxMhzINTEL");

        public static readonly ExecutionMode NamedBarrierCountINTEL =
            new ExecutionMode(6417, "NamedBarrierCountINTEL");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct StorageClass : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public StorageClass(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly StorageClass UniformConstant =
            new StorageClass(0, "UniformConstant");

        public static readonly StorageClass Input =
            new StorageClass(1, "Input");

        public static readonly StorageClass Uniform =
            new StorageClass(2, "Uniform");

        public static readonly StorageClass Output =
            new StorageClass(3, "Output");

        public static readonly StorageClass Workgroup =
            new StorageClass(4, "Workgroup");

        public static readonly StorageClass CrossWorkgroup =
            new StorageClass(5, "CrossWorkgroup");

        public static readonly StorageClass Private =
            new StorageClass(6, "Private");

        public static readonly StorageClass Function =
            new StorageClass(7, "Function");

        public static readonly StorageClass Generic =
            new StorageClass(8, "Generic");

        public static readonly StorageClass PushConstant =
            new StorageClass(9, "PushConstant");

        public static readonly StorageClass AtomicCounter =
            new StorageClass(10, "AtomicCounter");

        public static readonly StorageClass Image =
            new StorageClass(11, "Image");

        public static readonly StorageClass StorageBuffer =
            new StorageClass(12, "StorageBuffer");

        public static readonly StorageClass CallableDataNV =
            new StorageClass(5328, "CallableDataNV");

        public static readonly StorageClass CallableDataKHR =
            new StorageClass(5328, "CallableDataKHR");

        public static readonly StorageClass IncomingCallableDataNV =
            new StorageClass(5329, "IncomingCallableDataNV");

        public static readonly StorageClass IncomingCallableDataKHR =
            new StorageClass(5329, "IncomingCallableDataKHR");

        public static readonly StorageClass RayPayloadNV =
            new StorageClass(5338, "RayPayloadNV");

        public static readonly StorageClass RayPayloadKHR =
            new StorageClass(5338, "RayPayloadKHR");

        public static readonly StorageClass HitAttributeNV =
            new StorageClass(5339, "HitAttributeNV");

        public static readonly StorageClass HitAttributeKHR =
            new StorageClass(5339, "HitAttributeKHR");

        public static readonly StorageClass IncomingRayPayloadNV =
            new StorageClass(5342, "IncomingRayPayloadNV");

        public static readonly StorageClass IncomingRayPayloadKHR =
            new StorageClass(5342, "IncomingRayPayloadKHR");

        public static readonly StorageClass ShaderRecordBufferNV =
            new StorageClass(5343, "ShaderRecordBufferNV");

        public static readonly StorageClass ShaderRecordBufferKHR =
            new StorageClass(5343, "ShaderRecordBufferKHR");

        public static readonly StorageClass PhysicalStorageBuffer =
            new StorageClass(5349, "PhysicalStorageBuffer");

        public static readonly StorageClass PhysicalStorageBufferEXT =
            new StorageClass(5349, "PhysicalStorageBufferEXT");

        public static readonly StorageClass CodeSectionINTEL =
            new StorageClass(5605, "CodeSectionINTEL");

        public static readonly StorageClass DeviceOnlyINTEL =
            new StorageClass(5936, "DeviceOnlyINTEL");

        public static readonly StorageClass HostOnlyINTEL =
            new StorageClass(5937, "HostOnlyINTEL");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct Dim : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public Dim(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly Dim n1D =
            new Dim(0, "n1D");

        public static readonly Dim n2D =
            new Dim(1, "n2D");

        public static readonly Dim n3D =
            new Dim(2, "n3D");

        public static readonly Dim Cube =
            new Dim(3, "Cube");

        public static readonly Dim Rect =
            new Dim(4, "Rect");

        public static readonly Dim Buffer =
            new Dim(5, "Buffer");

        public static readonly Dim SubpassData =
            new Dim(6, "SubpassData");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct SamplerAddressingMode : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public SamplerAddressingMode(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly SamplerAddressingMode None =
            new SamplerAddressingMode(0, "None");

        public static readonly SamplerAddressingMode ClampToEdge =
            new SamplerAddressingMode(1, "ClampToEdge");

        public static readonly SamplerAddressingMode Clamp =
            new SamplerAddressingMode(2, "Clamp");

        public static readonly SamplerAddressingMode Repeat =
            new SamplerAddressingMode(3, "Repeat");

        public static readonly SamplerAddressingMode RepeatMirrored =
            new SamplerAddressingMode(4, "RepeatMirrored");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct SamplerFilterMode : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public SamplerFilterMode(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly SamplerFilterMode Nearest =
            new SamplerFilterMode(0, "Nearest");

        public static readonly SamplerFilterMode Linear =
            new SamplerFilterMode(1, "Linear");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct ImageFormat : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public ImageFormat(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly ImageFormat Unknown =
            new ImageFormat(0, "Unknown");

        public static readonly ImageFormat Rgba32f =
            new ImageFormat(1, "Rgba32f");

        public static readonly ImageFormat Rgba16f =
            new ImageFormat(2, "Rgba16f");

        public static readonly ImageFormat R32f =
            new ImageFormat(3, "R32f");

        public static readonly ImageFormat Rgba8 =
            new ImageFormat(4, "Rgba8");

        public static readonly ImageFormat Rgba8Snorm =
            new ImageFormat(5, "Rgba8Snorm");

        public static readonly ImageFormat Rg32f =
            new ImageFormat(6, "Rg32f");

        public static readonly ImageFormat Rg16f =
            new ImageFormat(7, "Rg16f");

        public static readonly ImageFormat R11fG11fB10f =
            new ImageFormat(8, "R11fG11fB10f");

        public static readonly ImageFormat R16f =
            new ImageFormat(9, "R16f");

        public static readonly ImageFormat Rgba16 =
            new ImageFormat(10, "Rgba16");

        public static readonly ImageFormat Rgb10A2 =
            new ImageFormat(11, "Rgb10A2");

        public static readonly ImageFormat Rg16 =
            new ImageFormat(12, "Rg16");

        public static readonly ImageFormat Rg8 =
            new ImageFormat(13, "Rg8");

        public static readonly ImageFormat R16 =
            new ImageFormat(14, "R16");

        public static readonly ImageFormat R8 =
            new ImageFormat(15, "R8");

        public static readonly ImageFormat Rgba16Snorm =
            new ImageFormat(16, "Rgba16Snorm");

        public static readonly ImageFormat Rg16Snorm =
            new ImageFormat(17, "Rg16Snorm");

        public static readonly ImageFormat Rg8Snorm =
            new ImageFormat(18, "Rg8Snorm");

        public static readonly ImageFormat R16Snorm =
            new ImageFormat(19, "R16Snorm");

        public static readonly ImageFormat R8Snorm =
            new ImageFormat(20, "R8Snorm");

        public static readonly ImageFormat Rgba32i =
            new ImageFormat(21, "Rgba32i");

        public static readonly ImageFormat Rgba16i =
            new ImageFormat(22, "Rgba16i");

        public static readonly ImageFormat Rgba8i =
            new ImageFormat(23, "Rgba8i");

        public static readonly ImageFormat R32i =
            new ImageFormat(24, "R32i");

        public static readonly ImageFormat Rg32i =
            new ImageFormat(25, "Rg32i");

        public static readonly ImageFormat Rg16i =
            new ImageFormat(26, "Rg16i");

        public static readonly ImageFormat Rg8i =
            new ImageFormat(27, "Rg8i");

        public static readonly ImageFormat R16i =
            new ImageFormat(28, "R16i");

        public static readonly ImageFormat R8i =
            new ImageFormat(29, "R8i");

        public static readonly ImageFormat Rgba32ui =
            new ImageFormat(30, "Rgba32ui");

        public static readonly ImageFormat Rgba16ui =
            new ImageFormat(31, "Rgba16ui");

        public static readonly ImageFormat Rgba8ui =
            new ImageFormat(32, "Rgba8ui");

        public static readonly ImageFormat R32ui =
            new ImageFormat(33, "R32ui");

        public static readonly ImageFormat Rgb10a2ui =
            new ImageFormat(34, "Rgb10a2ui");

        public static readonly ImageFormat Rg32ui =
            new ImageFormat(35, "Rg32ui");

        public static readonly ImageFormat Rg16ui =
            new ImageFormat(36, "Rg16ui");

        public static readonly ImageFormat Rg8ui =
            new ImageFormat(37, "Rg8ui");

        public static readonly ImageFormat R16ui =
            new ImageFormat(38, "R16ui");

        public static readonly ImageFormat R8ui =
            new ImageFormat(39, "R8ui");

        public static readonly ImageFormat R64ui =
            new ImageFormat(40, "R64ui");

        public static readonly ImageFormat R64i =
            new ImageFormat(41, "R64i");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct ImageChannelOrder : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public ImageChannelOrder(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly ImageChannelOrder R =
            new ImageChannelOrder(0, "R");

        public static readonly ImageChannelOrder A =
            new ImageChannelOrder(1, "A");

        public static readonly ImageChannelOrder RG =
            new ImageChannelOrder(2, "RG");

        public static readonly ImageChannelOrder RA =
            new ImageChannelOrder(3, "RA");

        public static readonly ImageChannelOrder RGB =
            new ImageChannelOrder(4, "RGB");

        public static readonly ImageChannelOrder RGBA =
            new ImageChannelOrder(5, "RGBA");

        public static readonly ImageChannelOrder BGRA =
            new ImageChannelOrder(6, "BGRA");

        public static readonly ImageChannelOrder ARGB =
            new ImageChannelOrder(7, "ARGB");

        public static readonly ImageChannelOrder Intensity =
            new ImageChannelOrder(8, "Intensity");

        public static readonly ImageChannelOrder Luminance =
            new ImageChannelOrder(9, "Luminance");

        public static readonly ImageChannelOrder Rx =
            new ImageChannelOrder(10, "Rx");

        public static readonly ImageChannelOrder RGx =
            new ImageChannelOrder(11, "RGx");

        public static readonly ImageChannelOrder RGBx =
            new ImageChannelOrder(12, "RGBx");

        public static readonly ImageChannelOrder Depth =
            new ImageChannelOrder(13, "Depth");

        public static readonly ImageChannelOrder DepthStencil =
            new ImageChannelOrder(14, "DepthStencil");

        public static readonly ImageChannelOrder sRGB =
            new ImageChannelOrder(15, "sRGB");

        public static readonly ImageChannelOrder sRGBx =
            new ImageChannelOrder(16, "sRGBx");

        public static readonly ImageChannelOrder sRGBA =
            new ImageChannelOrder(17, "sRGBA");

        public static readonly ImageChannelOrder sBGRA =
            new ImageChannelOrder(18, "sBGRA");

        public static readonly ImageChannelOrder ABGR =
            new ImageChannelOrder(19, "ABGR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct ImageChannelDataType : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public ImageChannelDataType(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly ImageChannelDataType SnormInt8 =
            new ImageChannelDataType(0, "SnormInt8");

        public static readonly ImageChannelDataType SnormInt16 =
            new ImageChannelDataType(1, "SnormInt16");

        public static readonly ImageChannelDataType UnormInt8 =
            new ImageChannelDataType(2, "UnormInt8");

        public static readonly ImageChannelDataType UnormInt16 =
            new ImageChannelDataType(3, "UnormInt16");

        public static readonly ImageChannelDataType UnormShort565 =
            new ImageChannelDataType(4, "UnormShort565");

        public static readonly ImageChannelDataType UnormShort555 =
            new ImageChannelDataType(5, "UnormShort555");

        public static readonly ImageChannelDataType UnormInt101010 =
            new ImageChannelDataType(6, "UnormInt101010");

        public static readonly ImageChannelDataType SignedInt8 =
            new ImageChannelDataType(7, "SignedInt8");

        public static readonly ImageChannelDataType SignedInt16 =
            new ImageChannelDataType(8, "SignedInt16");

        public static readonly ImageChannelDataType SignedInt32 =
            new ImageChannelDataType(9, "SignedInt32");

        public static readonly ImageChannelDataType UnsignedInt8 =
            new ImageChannelDataType(10, "UnsignedInt8");

        public static readonly ImageChannelDataType UnsignedInt16 =
            new ImageChannelDataType(11, "UnsignedInt16");

        public static readonly ImageChannelDataType UnsignedInt32 =
            new ImageChannelDataType(12, "UnsignedInt32");

        public static readonly ImageChannelDataType HalfFloat =
            new ImageChannelDataType(13, "HalfFloat");

        public static readonly ImageChannelDataType Float =
            new ImageChannelDataType(14, "Float");

        public static readonly ImageChannelDataType UnormInt24 =
            new ImageChannelDataType(15, "UnormInt24");

        public static readonly ImageChannelDataType UnormInt101010_2 =
            new ImageChannelDataType(16, "UnormInt101010_2");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct FPRoundingMode : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public FPRoundingMode(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly FPRoundingMode RTE =
            new FPRoundingMode(0, "RTE");

        public static readonly FPRoundingMode RTZ =
            new FPRoundingMode(1, "RTZ");

        public static readonly FPRoundingMode RTP =
            new FPRoundingMode(2, "RTP");

        public static readonly FPRoundingMode RTN =
            new FPRoundingMode(3, "RTN");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct FPDenormMode : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public FPDenormMode(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly FPDenormMode Preserve =
            new FPDenormMode(0, "Preserve");

        public static readonly FPDenormMode FlushToZero =
            new FPDenormMode(1, "FlushToZero");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct QuantizationModes : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public QuantizationModes(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly QuantizationModes TRN =
            new QuantizationModes(0, "TRN");

        public static readonly QuantizationModes TRN_ZERO =
            new QuantizationModes(1, "TRN_ZERO");

        public static readonly QuantizationModes RND =
            new QuantizationModes(2, "RND");

        public static readonly QuantizationModes RND_ZERO =
            new QuantizationModes(3, "RND_ZERO");

        public static readonly QuantizationModes RND_INF =
            new QuantizationModes(4, "RND_INF");

        public static readonly QuantizationModes RND_MIN_INF =
            new QuantizationModes(5, "RND_MIN_INF");

        public static readonly QuantizationModes RND_CONV =
            new QuantizationModes(6, "RND_CONV");

        public static readonly QuantizationModes RND_CONV_ODD =
            new QuantizationModes(7, "RND_CONV_ODD");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct FPOperationMode : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public FPOperationMode(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly FPOperationMode IEEE =
            new FPOperationMode(0, "IEEE");

        public static readonly FPOperationMode ALT =
            new FPOperationMode(1, "ALT");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct OverflowModes : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public OverflowModes(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly OverflowModes WRAP =
            new OverflowModes(0, "WRAP");

        public static readonly OverflowModes SAT =
            new OverflowModes(1, "SAT");

        public static readonly OverflowModes SAT_ZERO =
            new OverflowModes(2, "SAT_ZERO");

        public static readonly OverflowModes SAT_SYM =
            new OverflowModes(3, "SAT_SYM");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct LinkageType : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public LinkageType(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly LinkageType Export =
            new LinkageType(0, "Export");

        public static readonly LinkageType Import =
            new LinkageType(1, "Import");

        public static readonly LinkageType LinkOnceODR =
            new LinkageType(2, "LinkOnceODR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct AccessQualifier : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public AccessQualifier(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly AccessQualifier ReadOnly =
            new AccessQualifier(0, "ReadOnly");

        public static readonly AccessQualifier WriteOnly =
            new AccessQualifier(1, "WriteOnly");

        public static readonly AccessQualifier ReadWrite =
            new AccessQualifier(2, "ReadWrite");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct FunctionParameterAttribute : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public FunctionParameterAttribute(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly FunctionParameterAttribute Zext =
            new FunctionParameterAttribute(0, "Zext");

        public static readonly FunctionParameterAttribute Sext =
            new FunctionParameterAttribute(1, "Sext");

        public static readonly FunctionParameterAttribute ByVal =
            new FunctionParameterAttribute(2, "ByVal");

        public static readonly FunctionParameterAttribute Sret =
            new FunctionParameterAttribute(3, "Sret");

        public static readonly FunctionParameterAttribute NoAlias =
            new FunctionParameterAttribute(4, "NoAlias");

        public static readonly FunctionParameterAttribute NoCapture =
            new FunctionParameterAttribute(5, "NoCapture");

        public static readonly FunctionParameterAttribute NoWrite =
            new FunctionParameterAttribute(6, "NoWrite");

        public static readonly FunctionParameterAttribute NoReadWrite =
            new FunctionParameterAttribute(7, "NoReadWrite");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct Decoration : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public Decoration(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly Decoration RelaxedPrecision =
            new Decoration(0, "RelaxedPrecision");

        public static readonly Decoration SpecId =
            new Decoration(1, "SpecId");

        public static readonly Decoration Block =
            new Decoration(2, "Block");

        public static readonly Decoration BufferBlock =
            new Decoration(3, "BufferBlock");

        public static readonly Decoration RowMajor =
            new Decoration(4, "RowMajor");

        public static readonly Decoration ColMajor =
            new Decoration(5, "ColMajor");

        public static readonly Decoration ArrayStride =
            new Decoration(6, "ArrayStride");

        public static readonly Decoration MatrixStride =
            new Decoration(7, "MatrixStride");

        public static readonly Decoration GLSLShared =
            new Decoration(8, "GLSLShared");

        public static readonly Decoration GLSLPacked =
            new Decoration(9, "GLSLPacked");

        public static readonly Decoration CPacked =
            new Decoration(10, "CPacked");

        public static readonly Decoration BuiltIn =
            new Decoration(11, "BuiltIn");

        public static readonly Decoration NoPerspective =
            new Decoration(13, "NoPerspective");

        public static readonly Decoration Flat =
            new Decoration(14, "Flat");

        public static readonly Decoration Patch =
            new Decoration(15, "Patch");

        public static readonly Decoration Centroid =
            new Decoration(16, "Centroid");

        public static readonly Decoration Sample =
            new Decoration(17, "Sample");

        public static readonly Decoration Invariant =
            new Decoration(18, "Invariant");

        public static readonly Decoration Restrict =
            new Decoration(19, "Restrict");

        public static readonly Decoration Aliased =
            new Decoration(20, "Aliased");

        public static readonly Decoration Volatile =
            new Decoration(21, "Volatile");

        public static readonly Decoration Constant =
            new Decoration(22, "Constant");

        public static readonly Decoration Coherent =
            new Decoration(23, "Coherent");

        public static readonly Decoration NonWritable =
            new Decoration(24, "NonWritable");

        public static readonly Decoration NonReadable =
            new Decoration(25, "NonReadable");

        public static readonly Decoration Uniform =
            new Decoration(26, "Uniform");

        public static readonly Decoration UniformId =
            new Decoration(27, "UniformId");

        public static readonly Decoration SaturatedConversion =
            new Decoration(28, "SaturatedConversion");

        public static readonly Decoration Stream =
            new Decoration(29, "Stream");

        public static readonly Decoration Location =
            new Decoration(30, "Location");

        public static readonly Decoration Component =
            new Decoration(31, "Component");

        public static readonly Decoration Index =
            new Decoration(32, "Index");

        public static readonly Decoration Binding =
            new Decoration(33, "Binding");

        public static readonly Decoration DescriptorSet =
            new Decoration(34, "DescriptorSet");

        public static readonly Decoration Offset =
            new Decoration(35, "Offset");

        public static readonly Decoration XfbBuffer =
            new Decoration(36, "XfbBuffer");

        public static readonly Decoration XfbStride =
            new Decoration(37, "XfbStride");

        public static readonly Decoration FuncParamAttr =
            new Decoration(38, "FuncParamAttr");

        public static readonly Decoration FPRoundingMode =
            new Decoration(39, "FPRoundingMode");

        public static readonly Decoration FPFastMathMode =
            new Decoration(40, "FPFastMathMode");

        public static readonly Decoration LinkageAttributes =
            new Decoration(41, "LinkageAttributes");

        public static readonly Decoration NoContraction =
            new Decoration(42, "NoContraction");

        public static readonly Decoration InputAttachmentIndex =
            new Decoration(43, "InputAttachmentIndex");

        public static readonly Decoration Alignment =
            new Decoration(44, "Alignment");

        public static readonly Decoration MaxByteOffset =
            new Decoration(45, "MaxByteOffset");

        public static readonly Decoration AlignmentId =
            new Decoration(46, "AlignmentId");

        public static readonly Decoration MaxByteOffsetId =
            new Decoration(47, "MaxByteOffsetId");

        public static readonly Decoration NoSignedWrap =
            new Decoration(4469, "NoSignedWrap");

        public static readonly Decoration NoUnsignedWrap =
            new Decoration(4470, "NoUnsignedWrap");

        public static readonly Decoration ExplicitInterpAMD =
            new Decoration(4999, "ExplicitInterpAMD");

        public static readonly Decoration OverrideCoverageNV =
            new Decoration(5248, "OverrideCoverageNV");

        public static readonly Decoration PassthroughNV =
            new Decoration(5250, "PassthroughNV");

        public static readonly Decoration ViewportRelativeNV =
            new Decoration(5252, "ViewportRelativeNV");

        public static readonly Decoration SecondaryViewportRelativeNV =
            new Decoration(5256, "SecondaryViewportRelativeNV");

        public static readonly Decoration PerPrimitiveNV =
            new Decoration(5271, "PerPrimitiveNV");

        public static readonly Decoration PerViewNV =
            new Decoration(5272, "PerViewNV");

        public static readonly Decoration PerTaskNV =
            new Decoration(5273, "PerTaskNV");

        public static readonly Decoration PerVertexKHR =
            new Decoration(5285, "PerVertexKHR");

        public static readonly Decoration PerVertexNV =
            new Decoration(5285, "PerVertexNV");

        public static readonly Decoration NonUniform =
            new Decoration(5300, "NonUniform");

        public static readonly Decoration NonUniformEXT =
            new Decoration(5300, "NonUniformEXT");

        public static readonly Decoration RestrictPointer =
            new Decoration(5355, "RestrictPointer");

        public static readonly Decoration RestrictPointerEXT =
            new Decoration(5355, "RestrictPointerEXT");

        public static readonly Decoration AliasedPointer =
            new Decoration(5356, "AliasedPointer");

        public static readonly Decoration AliasedPointerEXT =
            new Decoration(5356, "AliasedPointerEXT");

        public static readonly Decoration BindlessSamplerNV =
            new Decoration(5398, "BindlessSamplerNV");

        public static readonly Decoration BindlessImageNV =
            new Decoration(5399, "BindlessImageNV");

        public static readonly Decoration BoundSamplerNV =
            new Decoration(5400, "BoundSamplerNV");

        public static readonly Decoration BoundImageNV =
            new Decoration(5401, "BoundImageNV");

        public static readonly Decoration SIMTCallINTEL =
            new Decoration(5599, "SIMTCallINTEL");

        public static readonly Decoration ReferencedIndirectlyINTEL =
            new Decoration(5602, "ReferencedIndirectlyINTEL");

        public static readonly Decoration ClobberINTEL =
            new Decoration(5607, "ClobberINTEL");

        public static readonly Decoration SideEffectsINTEL =
            new Decoration(5608, "SideEffectsINTEL");

        public static readonly Decoration VectorComputeVariableINTEL =
            new Decoration(5624, "VectorComputeVariableINTEL");

        public static readonly Decoration FuncParamIOKindINTEL =
            new Decoration(5625, "FuncParamIOKindINTEL");

        public static readonly Decoration VectorComputeFunctionINTEL =
            new Decoration(5626, "VectorComputeFunctionINTEL");

        public static readonly Decoration StackCallINTEL =
            new Decoration(5627, "StackCallINTEL");

        public static readonly Decoration GlobalVariableOffsetINTEL =
            new Decoration(5628, "GlobalVariableOffsetINTEL");

        public static readonly Decoration CounterBuffer =
            new Decoration(5634, "CounterBuffer");

        public static readonly Decoration HlslCounterBufferGOOGLE =
            new Decoration(5634, "HlslCounterBufferGOOGLE");

        public static readonly Decoration UserSemantic =
            new Decoration(5635, "UserSemantic");

        public static readonly Decoration HlslSemanticGOOGLE =
            new Decoration(5635, "HlslSemanticGOOGLE");

        public static readonly Decoration UserTypeGOOGLE =
            new Decoration(5636, "UserTypeGOOGLE");

        public static readonly Decoration FunctionRoundingModeINTEL =
            new Decoration(5822, "FunctionRoundingModeINTEL");

        public static readonly Decoration FunctionDenormModeINTEL =
            new Decoration(5823, "FunctionDenormModeINTEL");

        public static readonly Decoration RegisterINTEL =
            new Decoration(5825, "RegisterINTEL");

        public static readonly Decoration MemoryINTEL =
            new Decoration(5826, "MemoryINTEL");

        public static readonly Decoration NumbanksINTEL =
            new Decoration(5827, "NumbanksINTEL");

        public static readonly Decoration BankwidthINTEL =
            new Decoration(5828, "BankwidthINTEL");

        public static readonly Decoration MaxPrivateCopiesINTEL =
            new Decoration(5829, "MaxPrivateCopiesINTEL");

        public static readonly Decoration SinglepumpINTEL =
            new Decoration(5830, "SinglepumpINTEL");

        public static readonly Decoration DoublepumpINTEL =
            new Decoration(5831, "DoublepumpINTEL");

        public static readonly Decoration MaxReplicatesINTEL =
            new Decoration(5832, "MaxReplicatesINTEL");

        public static readonly Decoration SimpleDualPortINTEL =
            new Decoration(5833, "SimpleDualPortINTEL");

        public static readonly Decoration MergeINTEL =
            new Decoration(5834, "MergeINTEL");

        public static readonly Decoration BankBitsINTEL =
            new Decoration(5835, "BankBitsINTEL");

        public static readonly Decoration ForcePow2DepthINTEL =
            new Decoration(5836, "ForcePow2DepthINTEL");

        public static readonly Decoration BurstCoalesceINTEL =
            new Decoration(5899, "BurstCoalesceINTEL");

        public static readonly Decoration CacheSizeINTEL =
            new Decoration(5900, "CacheSizeINTEL");

        public static readonly Decoration DontStaticallyCoalesceINTEL =
            new Decoration(5901, "DontStaticallyCoalesceINTEL");

        public static readonly Decoration PrefetchINTEL =
            new Decoration(5902, "PrefetchINTEL");

        public static readonly Decoration StallEnableINTEL =
            new Decoration(5905, "StallEnableINTEL");

        public static readonly Decoration FuseLoopsInFunctionINTEL =
            new Decoration(5907, "FuseLoopsInFunctionINTEL");

        public static readonly Decoration AliasScopeINTEL =
            new Decoration(5914, "AliasScopeINTEL");

        public static readonly Decoration NoAliasINTEL =
            new Decoration(5915, "NoAliasINTEL");

        public static readonly Decoration BufferLocationINTEL =
            new Decoration(5921, "BufferLocationINTEL");

        public static readonly Decoration IOPipeStorageINTEL =
            new Decoration(5944, "IOPipeStorageINTEL");

        public static readonly Decoration FunctionFloatingPointModeINTEL =
            new Decoration(6080, "FunctionFloatingPointModeINTEL");

        public static readonly Decoration SingleElementVectorINTEL =
            new Decoration(6085, "SingleElementVectorINTEL");

        public static readonly Decoration VectorComputeCallableFunctionINTEL =
            new Decoration(6087, "VectorComputeCallableFunctionINTEL");

        public static readonly Decoration MediaBlockIOINTEL =
            new Decoration(6140, "MediaBlockIOINTEL");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct BuiltIn : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public BuiltIn(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly BuiltIn Position =
            new BuiltIn(0, "Position");

        public static readonly BuiltIn PointSize =
            new BuiltIn(1, "PointSize");

        public static readonly BuiltIn ClipDistance =
            new BuiltIn(3, "ClipDistance");

        public static readonly BuiltIn CullDistance =
            new BuiltIn(4, "CullDistance");

        public static readonly BuiltIn VertexId =
            new BuiltIn(5, "VertexId");

        public static readonly BuiltIn InstanceId =
            new BuiltIn(6, "InstanceId");

        public static readonly BuiltIn PrimitiveId =
            new BuiltIn(7, "PrimitiveId");

        public static readonly BuiltIn InvocationId =
            new BuiltIn(8, "InvocationId");

        public static readonly BuiltIn Layer =
            new BuiltIn(9, "Layer");

        public static readonly BuiltIn ViewportIndex =
            new BuiltIn(10, "ViewportIndex");

        public static readonly BuiltIn TessLevelOuter =
            new BuiltIn(11, "TessLevelOuter");

        public static readonly BuiltIn TessLevelInner =
            new BuiltIn(12, "TessLevelInner");

        public static readonly BuiltIn TessCoord =
            new BuiltIn(13, "TessCoord");

        public static readonly BuiltIn PatchVertices =
            new BuiltIn(14, "PatchVertices");

        public static readonly BuiltIn FragCoord =
            new BuiltIn(15, "FragCoord");

        public static readonly BuiltIn PointCoord =
            new BuiltIn(16, "PointCoord");

        public static readonly BuiltIn FrontFacing =
            new BuiltIn(17, "FrontFacing");

        public static readonly BuiltIn SampleId =
            new BuiltIn(18, "SampleId");

        public static readonly BuiltIn SamplePosition =
            new BuiltIn(19, "SamplePosition");

        public static readonly BuiltIn SampleMask =
            new BuiltIn(20, "SampleMask");

        public static readonly BuiltIn FragDepth =
            new BuiltIn(22, "FragDepth");

        public static readonly BuiltIn HelperInvocation =
            new BuiltIn(23, "HelperInvocation");

        public static readonly BuiltIn NumWorkgroups =
            new BuiltIn(24, "NumWorkgroups");

        public static readonly BuiltIn WorkgroupSize =
            new BuiltIn(25, "WorkgroupSize");

        public static readonly BuiltIn WorkgroupId =
            new BuiltIn(26, "WorkgroupId");

        public static readonly BuiltIn LocalInvocationId =
            new BuiltIn(27, "LocalInvocationId");

        public static readonly BuiltIn GlobalInvocationId =
            new BuiltIn(28, "GlobalInvocationId");

        public static readonly BuiltIn LocalInvocationIndex =
            new BuiltIn(29, "LocalInvocationIndex");

        public static readonly BuiltIn WorkDim =
            new BuiltIn(30, "WorkDim");

        public static readonly BuiltIn GlobalSize =
            new BuiltIn(31, "GlobalSize");

        public static readonly BuiltIn EnqueuedWorkgroupSize =
            new BuiltIn(32, "EnqueuedWorkgroupSize");

        public static readonly BuiltIn GlobalOffset =
            new BuiltIn(33, "GlobalOffset");

        public static readonly BuiltIn GlobalLinearId =
            new BuiltIn(34, "GlobalLinearId");

        public static readonly BuiltIn SubgroupSize =
            new BuiltIn(36, "SubgroupSize");

        public static readonly BuiltIn SubgroupMaxSize =
            new BuiltIn(37, "SubgroupMaxSize");

        public static readonly BuiltIn NumSubgroups =
            new BuiltIn(38, "NumSubgroups");

        public static readonly BuiltIn NumEnqueuedSubgroups =
            new BuiltIn(39, "NumEnqueuedSubgroups");

        public static readonly BuiltIn SubgroupId =
            new BuiltIn(40, "SubgroupId");

        public static readonly BuiltIn SubgroupLocalInvocationId =
            new BuiltIn(41, "SubgroupLocalInvocationId");

        public static readonly BuiltIn VertexIndex =
            new BuiltIn(42, "VertexIndex");

        public static readonly BuiltIn InstanceIndex =
            new BuiltIn(43, "InstanceIndex");

        public static readonly BuiltIn SubgroupEqMask =
            new BuiltIn(4416, "SubgroupEqMask");

        public static readonly BuiltIn SubgroupEqMaskKHR =
            new BuiltIn(4416, "SubgroupEqMaskKHR");

        public static readonly BuiltIn SubgroupGeMask =
            new BuiltIn(4417, "SubgroupGeMask");

        public static readonly BuiltIn SubgroupGeMaskKHR =
            new BuiltIn(4417, "SubgroupGeMaskKHR");

        public static readonly BuiltIn SubgroupGtMask =
            new BuiltIn(4418, "SubgroupGtMask");

        public static readonly BuiltIn SubgroupGtMaskKHR =
            new BuiltIn(4418, "SubgroupGtMaskKHR");

        public static readonly BuiltIn SubgroupLeMask =
            new BuiltIn(4419, "SubgroupLeMask");

        public static readonly BuiltIn SubgroupLeMaskKHR =
            new BuiltIn(4419, "SubgroupLeMaskKHR");

        public static readonly BuiltIn SubgroupLtMask =
            new BuiltIn(4420, "SubgroupLtMask");

        public static readonly BuiltIn SubgroupLtMaskKHR =
            new BuiltIn(4420, "SubgroupLtMaskKHR");

        public static readonly BuiltIn BaseVertex =
            new BuiltIn(4424, "BaseVertex");

        public static readonly BuiltIn BaseInstance =
            new BuiltIn(4425, "BaseInstance");

        public static readonly BuiltIn DrawIndex =
            new BuiltIn(4426, "DrawIndex");

        public static readonly BuiltIn PrimitiveShadingRateKHR =
            new BuiltIn(4432, "PrimitiveShadingRateKHR");

        public static readonly BuiltIn DeviceIndex =
            new BuiltIn(4438, "DeviceIndex");

        public static readonly BuiltIn ViewIndex =
            new BuiltIn(4440, "ViewIndex");

        public static readonly BuiltIn ShadingRateKHR =
            new BuiltIn(4444, "ShadingRateKHR");

        public static readonly BuiltIn BaryCoordNoPerspAMD =
            new BuiltIn(4992, "BaryCoordNoPerspAMD");

        public static readonly BuiltIn BaryCoordNoPerspCentroidAMD =
            new BuiltIn(4993, "BaryCoordNoPerspCentroidAMD");

        public static readonly BuiltIn BaryCoordNoPerspSampleAMD =
            new BuiltIn(4994, "BaryCoordNoPerspSampleAMD");

        public static readonly BuiltIn BaryCoordSmoothAMD =
            new BuiltIn(4995, "BaryCoordSmoothAMD");

        public static readonly BuiltIn BaryCoordSmoothCentroidAMD =
            new BuiltIn(4996, "BaryCoordSmoothCentroidAMD");

        public static readonly BuiltIn BaryCoordSmoothSampleAMD =
            new BuiltIn(4997, "BaryCoordSmoothSampleAMD");

        public static readonly BuiltIn BaryCoordPullModelAMD =
            new BuiltIn(4998, "BaryCoordPullModelAMD");

        public static readonly BuiltIn FragStencilRefEXT =
            new BuiltIn(5014, "FragStencilRefEXT");

        public static readonly BuiltIn ViewportMaskNV =
            new BuiltIn(5253, "ViewportMaskNV");

        public static readonly BuiltIn SecondaryPositionNV =
            new BuiltIn(5257, "SecondaryPositionNV");

        public static readonly BuiltIn SecondaryViewportMaskNV =
            new BuiltIn(5258, "SecondaryViewportMaskNV");

        public static readonly BuiltIn PositionPerViewNV =
            new BuiltIn(5261, "PositionPerViewNV");

        public static readonly BuiltIn ViewportMaskPerViewNV =
            new BuiltIn(5262, "ViewportMaskPerViewNV");

        public static readonly BuiltIn FullyCoveredEXT =
            new BuiltIn(5264, "FullyCoveredEXT");

        public static readonly BuiltIn TaskCountNV =
            new BuiltIn(5274, "TaskCountNV");

        public static readonly BuiltIn PrimitiveCountNV =
            new BuiltIn(5275, "PrimitiveCountNV");

        public static readonly BuiltIn PrimitiveIndicesNV =
            new BuiltIn(5276, "PrimitiveIndicesNV");

        public static readonly BuiltIn ClipDistancePerViewNV =
            new BuiltIn(5277, "ClipDistancePerViewNV");

        public static readonly BuiltIn CullDistancePerViewNV =
            new BuiltIn(5278, "CullDistancePerViewNV");

        public static readonly BuiltIn LayerPerViewNV =
            new BuiltIn(5279, "LayerPerViewNV");

        public static readonly BuiltIn MeshViewCountNV =
            new BuiltIn(5280, "MeshViewCountNV");

        public static readonly BuiltIn MeshViewIndicesNV =
            new BuiltIn(5281, "MeshViewIndicesNV");

        public static readonly BuiltIn BaryCoordKHR =
            new BuiltIn(5286, "BaryCoordKHR");

        public static readonly BuiltIn BaryCoordNV =
            new BuiltIn(5286, "BaryCoordNV");

        public static readonly BuiltIn BaryCoordNoPerspKHR =
            new BuiltIn(5287, "BaryCoordNoPerspKHR");

        public static readonly BuiltIn BaryCoordNoPerspNV =
            new BuiltIn(5287, "BaryCoordNoPerspNV");

        public static readonly BuiltIn FragSizeEXT =
            new BuiltIn(5292, "FragSizeEXT");

        public static readonly BuiltIn FragmentSizeNV =
            new BuiltIn(5292, "FragmentSizeNV");

        public static readonly BuiltIn FragInvocationCountEXT =
            new BuiltIn(5293, "FragInvocationCountEXT");

        public static readonly BuiltIn InvocationsPerPixelNV =
            new BuiltIn(5293, "InvocationsPerPixelNV");

        public static readonly BuiltIn LaunchIdNV =
            new BuiltIn(5319, "LaunchIdNV");

        public static readonly BuiltIn LaunchIdKHR =
            new BuiltIn(5319, "LaunchIdKHR");

        public static readonly BuiltIn LaunchSizeNV =
            new BuiltIn(5320, "LaunchSizeNV");

        public static readonly BuiltIn LaunchSizeKHR =
            new BuiltIn(5320, "LaunchSizeKHR");

        public static readonly BuiltIn WorldRayOriginNV =
            new BuiltIn(5321, "WorldRayOriginNV");

        public static readonly BuiltIn WorldRayOriginKHR =
            new BuiltIn(5321, "WorldRayOriginKHR");

        public static readonly BuiltIn WorldRayDirectionNV =
            new BuiltIn(5322, "WorldRayDirectionNV");

        public static readonly BuiltIn WorldRayDirectionKHR =
            new BuiltIn(5322, "WorldRayDirectionKHR");

        public static readonly BuiltIn ObjectRayOriginNV =
            new BuiltIn(5323, "ObjectRayOriginNV");

        public static readonly BuiltIn ObjectRayOriginKHR =
            new BuiltIn(5323, "ObjectRayOriginKHR");

        public static readonly BuiltIn ObjectRayDirectionNV =
            new BuiltIn(5324, "ObjectRayDirectionNV");

        public static readonly BuiltIn ObjectRayDirectionKHR =
            new BuiltIn(5324, "ObjectRayDirectionKHR");

        public static readonly BuiltIn RayTminNV =
            new BuiltIn(5325, "RayTminNV");

        public static readonly BuiltIn RayTminKHR =
            new BuiltIn(5325, "RayTminKHR");

        public static readonly BuiltIn RayTmaxNV =
            new BuiltIn(5326, "RayTmaxNV");

        public static readonly BuiltIn RayTmaxKHR =
            new BuiltIn(5326, "RayTmaxKHR");

        public static readonly BuiltIn InstanceCustomIndexNV =
            new BuiltIn(5327, "InstanceCustomIndexNV");

        public static readonly BuiltIn InstanceCustomIndexKHR =
            new BuiltIn(5327, "InstanceCustomIndexKHR");

        public static readonly BuiltIn ObjectToWorldNV =
            new BuiltIn(5330, "ObjectToWorldNV");

        public static readonly BuiltIn ObjectToWorldKHR =
            new BuiltIn(5330, "ObjectToWorldKHR");

        public static readonly BuiltIn WorldToObjectNV =
            new BuiltIn(5331, "WorldToObjectNV");

        public static readonly BuiltIn WorldToObjectKHR =
            new BuiltIn(5331, "WorldToObjectKHR");

        public static readonly BuiltIn HitTNV =
            new BuiltIn(5332, "HitTNV");

        public static readonly BuiltIn HitKindNV =
            new BuiltIn(5333, "HitKindNV");

        public static readonly BuiltIn HitKindKHR =
            new BuiltIn(5333, "HitKindKHR");

        public static readonly BuiltIn CurrentRayTimeNV =
            new BuiltIn(5334, "CurrentRayTimeNV");

        public static readonly BuiltIn IncomingRayFlagsNV =
            new BuiltIn(5351, "IncomingRayFlagsNV");

        public static readonly BuiltIn IncomingRayFlagsKHR =
            new BuiltIn(5351, "IncomingRayFlagsKHR");

        public static readonly BuiltIn RayGeometryIndexKHR =
            new BuiltIn(5352, "RayGeometryIndexKHR");

        public static readonly BuiltIn WarpsPerSMNV =
            new BuiltIn(5374, "WarpsPerSMNV");

        public static readonly BuiltIn SMCountNV =
            new BuiltIn(5375, "SMCountNV");

        public static readonly BuiltIn WarpIDNV =
            new BuiltIn(5376, "WarpIDNV");

        public static readonly BuiltIn SMIDNV =
            new BuiltIn(5377, "SMIDNV");

        public static readonly BuiltIn CullMaskKHR =
            new BuiltIn(6021, "CullMaskKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct Scope : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public Scope(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly Scope CrossDevice =
            new Scope(0, "CrossDevice");

        public static readonly Scope Device =
            new Scope(1, "Device");

        public static readonly Scope Workgroup =
            new Scope(2, "Workgroup");

        public static readonly Scope Subgroup =
            new Scope(3, "Subgroup");

        public static readonly Scope Invocation =
            new Scope(4, "Invocation");

        public static readonly Scope QueueFamily =
            new Scope(5, "QueueFamily");

        public static readonly Scope QueueFamilyKHR =
            new Scope(5, "QueueFamilyKHR");

        public static readonly Scope ShaderCallKHR =
            new Scope(6, "ShaderCallKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct GroupOperation : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public GroupOperation(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly GroupOperation Reduce =
            new GroupOperation(0, "Reduce");

        public static readonly GroupOperation InclusiveScan =
            new GroupOperation(1, "InclusiveScan");

        public static readonly GroupOperation ExclusiveScan =
            new GroupOperation(2, "ExclusiveScan");

        public static readonly GroupOperation ClusteredReduce =
            new GroupOperation(3, "ClusteredReduce");

        public static readonly GroupOperation PartitionedReduceNV =
            new GroupOperation(6, "PartitionedReduceNV");

        public static readonly GroupOperation PartitionedInclusiveScanNV =
            new GroupOperation(7, "PartitionedInclusiveScanNV");

        public static readonly GroupOperation PartitionedExclusiveScanNV =
            new GroupOperation(8, "PartitionedExclusiveScanNV");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct KernelEnqueueFlags : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public KernelEnqueueFlags(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly KernelEnqueueFlags NoWait =
            new KernelEnqueueFlags(0, "NoWait");

        public static readonly KernelEnqueueFlags WaitKernel =
            new KernelEnqueueFlags(1, "WaitKernel");

        public static readonly KernelEnqueueFlags WaitWorkGroup =
            new KernelEnqueueFlags(2, "WaitWorkGroup");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct Capability : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public Capability(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly Capability Matrix =
            new Capability(0, "Matrix");

        public static readonly Capability Shader =
            new Capability(1, "Shader");

        public static readonly Capability Geometry =
            new Capability(2, "Geometry");

        public static readonly Capability Tessellation =
            new Capability(3, "Tessellation");

        public static readonly Capability Addresses =
            new Capability(4, "Addresses");

        public static readonly Capability Linkage =
            new Capability(5, "Linkage");

        public static readonly Capability Kernel =
            new Capability(6, "Kernel");

        public static readonly Capability Vector16 =
            new Capability(7, "Vector16");

        public static readonly Capability Float16Buffer =
            new Capability(8, "Float16Buffer");

        public static readonly Capability Float16 =
            new Capability(9, "Float16");

        public static readonly Capability Float64 =
            new Capability(10, "Float64");

        public static readonly Capability Int64 =
            new Capability(11, "Int64");

        public static readonly Capability Int64Atomics =
            new Capability(12, "Int64Atomics");

        public static readonly Capability ImageBasic =
            new Capability(13, "ImageBasic");

        public static readonly Capability ImageReadWrite =
            new Capability(14, "ImageReadWrite");

        public static readonly Capability ImageMipmap =
            new Capability(15, "ImageMipmap");

        public static readonly Capability Pipes =
            new Capability(17, "Pipes");

        public static readonly Capability Groups =
            new Capability(18, "Groups");

        public static readonly Capability DeviceEnqueue =
            new Capability(19, "DeviceEnqueue");

        public static readonly Capability LiteralSampler =
            new Capability(20, "LiteralSampler");

        public static readonly Capability AtomicStorage =
            new Capability(21, "AtomicStorage");

        public static readonly Capability Int16 =
            new Capability(22, "Int16");

        public static readonly Capability TessellationPointSize =
            new Capability(23, "TessellationPointSize");

        public static readonly Capability GeometryPointSize =
            new Capability(24, "GeometryPointSize");

        public static readonly Capability ImageGatherExtended =
            new Capability(25, "ImageGatherExtended");

        public static readonly Capability StorageImageMultisample =
            new Capability(27, "StorageImageMultisample");

        public static readonly Capability UniformBufferArrayDynamicIndexing =
            new Capability(28, "UniformBufferArrayDynamicIndexing");

        public static readonly Capability SampledImageArrayDynamicIndexing =
            new Capability(29, "SampledImageArrayDynamicIndexing");

        public static readonly Capability StorageBufferArrayDynamicIndexing =
            new Capability(30, "StorageBufferArrayDynamicIndexing");

        public static readonly Capability StorageImageArrayDynamicIndexing =
            new Capability(31, "StorageImageArrayDynamicIndexing");

        public static readonly Capability ClipDistance =
            new Capability(32, "ClipDistance");

        public static readonly Capability CullDistance =
            new Capability(33, "CullDistance");

        public static readonly Capability ImageCubeArray =
            new Capability(34, "ImageCubeArray");

        public static readonly Capability SampleRateShading =
            new Capability(35, "SampleRateShading");

        public static readonly Capability ImageRect =
            new Capability(36, "ImageRect");

        public static readonly Capability SampledRect =
            new Capability(37, "SampledRect");

        public static readonly Capability GenericPointer =
            new Capability(38, "GenericPointer");

        public static readonly Capability Int8 =
            new Capability(39, "Int8");

        public static readonly Capability InputAttachment =
            new Capability(40, "InputAttachment");

        public static readonly Capability SparseResidency =
            new Capability(41, "SparseResidency");

        public static readonly Capability MinLod =
            new Capability(42, "MinLod");

        public static readonly Capability Sampled1D =
            new Capability(43, "Sampled1D");

        public static readonly Capability Image1D =
            new Capability(44, "Image1D");

        public static readonly Capability SampledCubeArray =
            new Capability(45, "SampledCubeArray");

        public static readonly Capability SampledBuffer =
            new Capability(46, "SampledBuffer");

        public static readonly Capability ImageBuffer =
            new Capability(47, "ImageBuffer");

        public static readonly Capability ImageMSArray =
            new Capability(48, "ImageMSArray");

        public static readonly Capability StorageImageExtendedFormats =
            new Capability(49, "StorageImageExtendedFormats");

        public static readonly Capability ImageQuery =
            new Capability(50, "ImageQuery");

        public static readonly Capability DerivativeControl =
            new Capability(51, "DerivativeControl");

        public static readonly Capability InterpolationFunction =
            new Capability(52, "InterpolationFunction");

        public static readonly Capability TransformFeedback =
            new Capability(53, "TransformFeedback");

        public static readonly Capability GeometryStreams =
            new Capability(54, "GeometryStreams");

        public static readonly Capability StorageImageReadWithoutFormat =
            new Capability(55, "StorageImageReadWithoutFormat");

        public static readonly Capability StorageImageWriteWithoutFormat =
            new Capability(56, "StorageImageWriteWithoutFormat");

        public static readonly Capability MultiViewport =
            new Capability(57, "MultiViewport");

        public static readonly Capability SubgroupDispatch =
            new Capability(58, "SubgroupDispatch");

        public static readonly Capability NamedBarrier =
            new Capability(59, "NamedBarrier");

        public static readonly Capability PipeStorage =
            new Capability(60, "PipeStorage");

        public static readonly Capability GroupNonUniform =
            new Capability(61, "GroupNonUniform");

        public static readonly Capability GroupNonUniformVote =
            new Capability(62, "GroupNonUniformVote");

        public static readonly Capability GroupNonUniformArithmetic =
            new Capability(63, "GroupNonUniformArithmetic");

        public static readonly Capability GroupNonUniformBallot =
            new Capability(64, "GroupNonUniformBallot");

        public static readonly Capability GroupNonUniformShuffle =
            new Capability(65, "GroupNonUniformShuffle");

        public static readonly Capability GroupNonUniformShuffleRelative =
            new Capability(66, "GroupNonUniformShuffleRelative");

        public static readonly Capability GroupNonUniformClustered =
            new Capability(67, "GroupNonUniformClustered");

        public static readonly Capability GroupNonUniformQuad =
            new Capability(68, "GroupNonUniformQuad");

        public static readonly Capability ShaderLayer =
            new Capability(69, "ShaderLayer");

        public static readonly Capability ShaderViewportIndex =
            new Capability(70, "ShaderViewportIndex");

        public static readonly Capability UniformDecoration =
            new Capability(71, "UniformDecoration");

        public static readonly Capability FragmentShadingRateKHR =
            new Capability(4422, "FragmentShadingRateKHR");

        public static readonly Capability SubgroupBallotKHR =
            new Capability(4423, "SubgroupBallotKHR");

        public static readonly Capability DrawParameters =
            new Capability(4427, "DrawParameters");

        public static readonly Capability WorkgroupMemoryExplicitLayoutKHR =
            new Capability(4428, "WorkgroupMemoryExplicitLayoutKHR");

        public static readonly Capability WorkgroupMemoryExplicitLayout8BitAccessKHR =
            new Capability(4429, "WorkgroupMemoryExplicitLayout8BitAccessKHR");

        public static readonly Capability WorkgroupMemoryExplicitLayout16BitAccessKHR =
            new Capability(4430, "WorkgroupMemoryExplicitLayout16BitAccessKHR");

        public static readonly Capability SubgroupVoteKHR =
            new Capability(4431, "SubgroupVoteKHR");

        public static readonly Capability StorageBuffer16BitAccess =
            new Capability(4433, "StorageBuffer16BitAccess");

        public static readonly Capability StorageUniformBufferBlock16 =
            new Capability(4433, "StorageUniformBufferBlock16");

        public static readonly Capability UniformAndStorageBuffer16BitAccess =
            new Capability(4434, "UniformAndStorageBuffer16BitAccess");

        public static readonly Capability StorageUniform16 =
            new Capability(4434, "StorageUniform16");

        public static readonly Capability StoragePushConstant16 =
            new Capability(4435, "StoragePushConstant16");

        public static readonly Capability StorageInputOutput16 =
            new Capability(4436, "StorageInputOutput16");

        public static readonly Capability DeviceGroup =
            new Capability(4437, "DeviceGroup");

        public static readonly Capability MultiView =
            new Capability(4439, "MultiView");

        public static readonly Capability VariablePointersStorageBuffer =
            new Capability(4441, "VariablePointersStorageBuffer");

        public static readonly Capability VariablePointers =
            new Capability(4442, "VariablePointers");

        public static readonly Capability AtomicStorageOps =
            new Capability(4445, "AtomicStorageOps");

        public static readonly Capability SampleMaskPostDepthCoverage =
            new Capability(4447, "SampleMaskPostDepthCoverage");

        public static readonly Capability StorageBuffer8BitAccess =
            new Capability(4448, "StorageBuffer8BitAccess");

        public static readonly Capability UniformAndStorageBuffer8BitAccess =
            new Capability(4449, "UniformAndStorageBuffer8BitAccess");

        public static readonly Capability StoragePushConstant8 =
            new Capability(4450, "StoragePushConstant8");

        public static readonly Capability DenormPreserve =
            new Capability(4464, "DenormPreserve");

        public static readonly Capability DenormFlushToZero =
            new Capability(4465, "DenormFlushToZero");

        public static readonly Capability SignedZeroInfNanPreserve =
            new Capability(4466, "SignedZeroInfNanPreserve");

        public static readonly Capability RoundingModeRTE =
            new Capability(4467, "RoundingModeRTE");

        public static readonly Capability RoundingModeRTZ =
            new Capability(4468, "RoundingModeRTZ");

        public static readonly Capability RayQueryProvisionalKHR =
            new Capability(4471, "RayQueryProvisionalKHR");

        public static readonly Capability RayQueryKHR =
            new Capability(4472, "RayQueryKHR");

        public static readonly Capability RayTraversalPrimitiveCullingKHR =
            new Capability(4478, "RayTraversalPrimitiveCullingKHR");

        public static readonly Capability RayTracingKHR =
            new Capability(4479, "RayTracingKHR");

        public static readonly Capability Float16ImageAMD =
            new Capability(5008, "Float16ImageAMD");

        public static readonly Capability ImageGatherBiasLodAMD =
            new Capability(5009, "ImageGatherBiasLodAMD");

        public static readonly Capability FragmentMaskAMD =
            new Capability(5010, "FragmentMaskAMD");

        public static readonly Capability StencilExportEXT =
            new Capability(5013, "StencilExportEXT");

        public static readonly Capability ImageReadWriteLodAMD =
            new Capability(5015, "ImageReadWriteLodAMD");

        public static readonly Capability Int64ImageEXT =
            new Capability(5016, "Int64ImageEXT");

        public static readonly Capability ShaderClockKHR =
            new Capability(5055, "ShaderClockKHR");

        public static readonly Capability SampleMaskOverrideCoverageNV =
            new Capability(5249, "SampleMaskOverrideCoverageNV");

        public static readonly Capability GeometryShaderPassthroughNV =
            new Capability(5251, "GeometryShaderPassthroughNV");

        public static readonly Capability ShaderViewportIndexLayerEXT =
            new Capability(5254, "ShaderViewportIndexLayerEXT");

        public static readonly Capability ShaderViewportIndexLayerNV =
            new Capability(5254, "ShaderViewportIndexLayerNV");

        public static readonly Capability ShaderViewportMaskNV =
            new Capability(5255, "ShaderViewportMaskNV");

        public static readonly Capability ShaderStereoViewNV =
            new Capability(5259, "ShaderStereoViewNV");

        public static readonly Capability PerViewAttributesNV =
            new Capability(5260, "PerViewAttributesNV");

        public static readonly Capability FragmentFullyCoveredEXT =
            new Capability(5265, "FragmentFullyCoveredEXT");

        public static readonly Capability MeshShadingNV =
            new Capability(5266, "MeshShadingNV");

        public static readonly Capability ImageFootprintNV =
            new Capability(5282, "ImageFootprintNV");

        public static readonly Capability FragmentBarycentricKHR =
            new Capability(5284, "FragmentBarycentricKHR");

        public static readonly Capability FragmentBarycentricNV =
            new Capability(5284, "FragmentBarycentricNV");

        public static readonly Capability ComputeDerivativeGroupQuadsNV =
            new Capability(5288, "ComputeDerivativeGroupQuadsNV");

        public static readonly Capability FragmentDensityEXT =
            new Capability(5291, "FragmentDensityEXT");

        public static readonly Capability ShadingRateNV =
            new Capability(5291, "ShadingRateNV");

        public static readonly Capability GroupNonUniformPartitionedNV =
            new Capability(5297, "GroupNonUniformPartitionedNV");

        public static readonly Capability ShaderNonUniform =
            new Capability(5301, "ShaderNonUniform");

        public static readonly Capability ShaderNonUniformEXT =
            new Capability(5301, "ShaderNonUniformEXT");

        public static readonly Capability RuntimeDescriptorArray =
            new Capability(5302, "RuntimeDescriptorArray");

        public static readonly Capability RuntimeDescriptorArrayEXT =
            new Capability(5302, "RuntimeDescriptorArrayEXT");

        public static readonly Capability InputAttachmentArrayDynamicIndexing =
            new Capability(5303, "InputAttachmentArrayDynamicIndexing");

        public static readonly Capability InputAttachmentArrayDynamicIndexingEXT =
            new Capability(5303, "InputAttachmentArrayDynamicIndexingEXT");

        public static readonly Capability UniformTexelBufferArrayDynamicIndexing =
            new Capability(5304, "UniformTexelBufferArrayDynamicIndexing");

        public static readonly Capability UniformTexelBufferArrayDynamicIndexingEXT =
            new Capability(5304, "UniformTexelBufferArrayDynamicIndexingEXT");

        public static readonly Capability StorageTexelBufferArrayDynamicIndexing =
            new Capability(5305, "StorageTexelBufferArrayDynamicIndexing");

        public static readonly Capability StorageTexelBufferArrayDynamicIndexingEXT =
            new Capability(5305, "StorageTexelBufferArrayDynamicIndexingEXT");

        public static readonly Capability UniformBufferArrayNonUniformIndexing =
            new Capability(5306, "UniformBufferArrayNonUniformIndexing");

        public static readonly Capability UniformBufferArrayNonUniformIndexingEXT =
            new Capability(5306, "UniformBufferArrayNonUniformIndexingEXT");

        public static readonly Capability SampledImageArrayNonUniformIndexing =
            new Capability(5307, "SampledImageArrayNonUniformIndexing");

        public static readonly Capability SampledImageArrayNonUniformIndexingEXT =
            new Capability(5307, "SampledImageArrayNonUniformIndexingEXT");

        public static readonly Capability StorageBufferArrayNonUniformIndexing =
            new Capability(5308, "StorageBufferArrayNonUniformIndexing");

        public static readonly Capability StorageBufferArrayNonUniformIndexingEXT =
            new Capability(5308, "StorageBufferArrayNonUniformIndexingEXT");

        public static readonly Capability StorageImageArrayNonUniformIndexing =
            new Capability(5309, "StorageImageArrayNonUniformIndexing");

        public static readonly Capability StorageImageArrayNonUniformIndexingEXT =
            new Capability(5309, "StorageImageArrayNonUniformIndexingEXT");

        public static readonly Capability InputAttachmentArrayNonUniformIndexing =
            new Capability(5310, "InputAttachmentArrayNonUniformIndexing");

        public static readonly Capability InputAttachmentArrayNonUniformIndexingEXT =
            new Capability(5310, "InputAttachmentArrayNonUniformIndexingEXT");

        public static readonly Capability UniformTexelBufferArrayNonUniformIndexing =
            new Capability(5311, "UniformTexelBufferArrayNonUniformIndexing");

        public static readonly Capability UniformTexelBufferArrayNonUniformIndexingEXT =
            new Capability(5311, "UniformTexelBufferArrayNonUniformIndexingEXT");

        public static readonly Capability StorageTexelBufferArrayNonUniformIndexing =
            new Capability(5312, "StorageTexelBufferArrayNonUniformIndexing");

        public static readonly Capability StorageTexelBufferArrayNonUniformIndexingEXT =
            new Capability(5312, "StorageTexelBufferArrayNonUniformIndexingEXT");

        public static readonly Capability RayTracingNV =
            new Capability(5340, "RayTracingNV");

        public static readonly Capability RayTracingMotionBlurNV =
            new Capability(5341, "RayTracingMotionBlurNV");

        public static readonly Capability VulkanMemoryModel =
            new Capability(5345, "VulkanMemoryModel");

        public static readonly Capability VulkanMemoryModelKHR =
            new Capability(5345, "VulkanMemoryModelKHR");

        public static readonly Capability VulkanMemoryModelDeviceScope =
            new Capability(5346, "VulkanMemoryModelDeviceScope");

        public static readonly Capability VulkanMemoryModelDeviceScopeKHR =
            new Capability(5346, "VulkanMemoryModelDeviceScopeKHR");

        public static readonly Capability PhysicalStorageBufferAddresses =
            new Capability(5347, "PhysicalStorageBufferAddresses");

        public static readonly Capability PhysicalStorageBufferAddressesEXT =
            new Capability(5347, "PhysicalStorageBufferAddressesEXT");

        public static readonly Capability ComputeDerivativeGroupLinearNV =
            new Capability(5350, "ComputeDerivativeGroupLinearNV");

        public static readonly Capability RayTracingProvisionalKHR =
            new Capability(5353, "RayTracingProvisionalKHR");

        public static readonly Capability CooperativeMatrixNV =
            new Capability(5357, "CooperativeMatrixNV");

        public static readonly Capability FragmentShaderSampleInterlockEXT =
            new Capability(5363, "FragmentShaderSampleInterlockEXT");

        public static readonly Capability FragmentShaderShadingRateInterlockEXT =
            new Capability(5372, "FragmentShaderShadingRateInterlockEXT");

        public static readonly Capability ShaderSMBuiltinsNV =
            new Capability(5373, "ShaderSMBuiltinsNV");

        public static readonly Capability FragmentShaderPixelInterlockEXT =
            new Capability(5378, "FragmentShaderPixelInterlockEXT");

        public static readonly Capability DemoteToHelperInvocation =
            new Capability(5379, "DemoteToHelperInvocation");

        public static readonly Capability DemoteToHelperInvocationEXT =
            new Capability(5379, "DemoteToHelperInvocationEXT");

        public static readonly Capability BindlessTextureNV =
            new Capability(5390, "BindlessTextureNV");

        public static readonly Capability SubgroupShuffleINTEL =
            new Capability(5568, "SubgroupShuffleINTEL");

        public static readonly Capability SubgroupBufferBlockIOINTEL =
            new Capability(5569, "SubgroupBufferBlockIOINTEL");

        public static readonly Capability SubgroupImageBlockIOINTEL =
            new Capability(5570, "SubgroupImageBlockIOINTEL");

        public static readonly Capability SubgroupImageMediaBlockIOINTEL =
            new Capability(5579, "SubgroupImageMediaBlockIOINTEL");

        public static readonly Capability RoundToInfinityINTEL =
            new Capability(5582, "RoundToInfinityINTEL");

        public static readonly Capability FloatingPointModeINTEL =
            new Capability(5583, "FloatingPointModeINTEL");

        public static readonly Capability IntegerFunctions2INTEL =
            new Capability(5584, "IntegerFunctions2INTEL");

        public static readonly Capability FunctionPointersINTEL =
            new Capability(5603, "FunctionPointersINTEL");

        public static readonly Capability IndirectReferencesINTEL =
            new Capability(5604, "IndirectReferencesINTEL");

        public static readonly Capability AsmINTEL =
            new Capability(5606, "AsmINTEL");

        public static readonly Capability AtomicFloat32MinMaxEXT =
            new Capability(5612, "AtomicFloat32MinMaxEXT");

        public static readonly Capability AtomicFloat64MinMaxEXT =
            new Capability(5613, "AtomicFloat64MinMaxEXT");

        public static readonly Capability AtomicFloat16MinMaxEXT =
            new Capability(5616, "AtomicFloat16MinMaxEXT");

        public static readonly Capability VectorComputeINTEL =
            new Capability(5617, "VectorComputeINTEL");

        public static readonly Capability VectorAnyINTEL =
            new Capability(5619, "VectorAnyINTEL");

        public static readonly Capability ExpectAssumeKHR =
            new Capability(5629, "ExpectAssumeKHR");

        public static readonly Capability SubgroupAvcMotionEstimationINTEL =
            new Capability(5696, "SubgroupAvcMotionEstimationINTEL");

        public static readonly Capability SubgroupAvcMotionEstimationIntraINTEL =
            new Capability(5697, "SubgroupAvcMotionEstimationIntraINTEL");

        public static readonly Capability SubgroupAvcMotionEstimationChromaINTEL =
            new Capability(5698, "SubgroupAvcMotionEstimationChromaINTEL");

        public static readonly Capability VariableLengthArrayINTEL =
            new Capability(5817, "VariableLengthArrayINTEL");

        public static readonly Capability FunctionFloatControlINTEL =
            new Capability(5821, "FunctionFloatControlINTEL");

        public static readonly Capability FPGAMemoryAttributesINTEL =
            new Capability(5824, "FPGAMemoryAttributesINTEL");

        public static readonly Capability FPFastMathModeINTEL =
            new Capability(5837, "FPFastMathModeINTEL");

        public static readonly Capability ArbitraryPrecisionIntegersINTEL =
            new Capability(5844, "ArbitraryPrecisionIntegersINTEL");

        public static readonly Capability ArbitraryPrecisionFloatingPointINTEL =
            new Capability(5845, "ArbitraryPrecisionFloatingPointINTEL");

        public static readonly Capability UnstructuredLoopControlsINTEL =
            new Capability(5886, "UnstructuredLoopControlsINTEL");

        public static readonly Capability FPGALoopControlsINTEL =
            new Capability(5888, "FPGALoopControlsINTEL");

        public static readonly Capability KernelAttributesINTEL =
            new Capability(5892, "KernelAttributesINTEL");

        public static readonly Capability FPGAKernelAttributesINTEL =
            new Capability(5897, "FPGAKernelAttributesINTEL");

        public static readonly Capability FPGAMemoryAccessesINTEL =
            new Capability(5898, "FPGAMemoryAccessesINTEL");

        public static readonly Capability FPGAClusterAttributesINTEL =
            new Capability(5904, "FPGAClusterAttributesINTEL");

        public static readonly Capability LoopFuseINTEL =
            new Capability(5906, "LoopFuseINTEL");

        public static readonly Capability MemoryAccessAliasingINTEL =
            new Capability(5910, "MemoryAccessAliasingINTEL");

        public static readonly Capability FPGABufferLocationINTEL =
            new Capability(5920, "FPGABufferLocationINTEL");

        public static readonly Capability ArbitraryPrecisionFixedPointINTEL =
            new Capability(5922, "ArbitraryPrecisionFixedPointINTEL");

        public static readonly Capability USMStorageClassesINTEL =
            new Capability(5935, "USMStorageClassesINTEL");

        public static readonly Capability IOPipesINTEL =
            new Capability(5943, "IOPipesINTEL");

        public static readonly Capability BlockingPipesINTEL =
            new Capability(5945, "BlockingPipesINTEL");

        public static readonly Capability FPGARegINTEL =
            new Capability(5948, "FPGARegINTEL");

        public static readonly Capability DotProductInputAll =
            new Capability(6016, "DotProductInputAll");

        public static readonly Capability DotProductInputAllKHR =
            new Capability(6016, "DotProductInputAllKHR");

        public static readonly Capability DotProductInput4x8Bit =
            new Capability(6017, "DotProductInput4x8Bit");

        public static readonly Capability DotProductInput4x8BitKHR =
            new Capability(6017, "DotProductInput4x8BitKHR");

        public static readonly Capability DotProductInput4x8BitPacked =
            new Capability(6018, "DotProductInput4x8BitPacked");

        public static readonly Capability DotProductInput4x8BitPackedKHR =
            new Capability(6018, "DotProductInput4x8BitPackedKHR");

        public static readonly Capability DotProduct =
            new Capability(6019, "DotProduct");

        public static readonly Capability DotProductKHR =
            new Capability(6019, "DotProductKHR");

        public static readonly Capability RayCullMaskKHR =
            new Capability(6020, "RayCullMaskKHR");

        public static readonly Capability BitInstructions =
            new Capability(6025, "BitInstructions");

        public static readonly Capability GroupNonUniformRotateKHR =
            new Capability(6026, "GroupNonUniformRotateKHR");

        public static readonly Capability AtomicFloat32AddEXT =
            new Capability(6033, "AtomicFloat32AddEXT");

        public static readonly Capability AtomicFloat64AddEXT =
            new Capability(6034, "AtomicFloat64AddEXT");

        public static readonly Capability LongConstantCompositeINTEL =
            new Capability(6089, "LongConstantCompositeINTEL");

        public static readonly Capability OptNoneINTEL =
            new Capability(6094, "OptNoneINTEL");

        public static readonly Capability AtomicFloat16AddEXT =
            new Capability(6095, "AtomicFloat16AddEXT");

        public static readonly Capability DebugInfoModuleINTEL =
            new Capability(6114, "DebugInfoModuleINTEL");

        public static readonly Capability SplitBarrierINTEL =
            new Capability(6141, "SplitBarrierINTEL");

        public static readonly Capability GroupUniformArithmeticKHR =
            new Capability(6400, "GroupUniformArithmeticKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct RayQueryIntersection : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public RayQueryIntersection(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly RayQueryIntersection RayQueryCandidateIntersectionKHR =
            new RayQueryIntersection(0, "RayQueryCandidateIntersectionKHR");

        public static readonly RayQueryIntersection RayQueryCommittedIntersectionKHR =
            new RayQueryIntersection(1, "RayQueryCommittedIntersectionKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct RayQueryCommittedIntersectionType : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public RayQueryCommittedIntersectionType(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly RayQueryCommittedIntersectionType RayQueryCommittedIntersectionNoneKHR =
            new RayQueryCommittedIntersectionType(0, "RayQueryCommittedIntersectionNoneKHR");

        public static readonly RayQueryCommittedIntersectionType RayQueryCommittedIntersectionTriangleKHR =
            new RayQueryCommittedIntersectionType(1, "RayQueryCommittedIntersectionTriangleKHR");

        public static readonly RayQueryCommittedIntersectionType RayQueryCommittedIntersectionGeneratedKHR =
            new RayQueryCommittedIntersectionType(2, "RayQueryCommittedIntersectionGeneratedKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct RayQueryCandidateIntersectionType : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public RayQueryCandidateIntersectionType(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly RayQueryCandidateIntersectionType RayQueryCandidateIntersectionTriangleKHR =
            new RayQueryCandidateIntersectionType(0, "RayQueryCandidateIntersectionTriangleKHR");

        public static readonly RayQueryCandidateIntersectionType RayQueryCandidateIntersectionAABBKHR =
            new RayQueryCandidateIntersectionType(1, "RayQueryCandidateIntersectionAABBKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct PackedVectorFormat : ISPIRVType
    {
        private SPIRVWord _value;
        private string _repr;

        public PackedVectorFormat(SPIRVWord word, string name)
        {
            _value = word;
            _repr = name;
        }

        public static readonly PackedVectorFormat PackedVectorFormat4x8Bit =
            new PackedVectorFormat(0, "PackedVectorFormat4x8Bit");

        public static readonly PackedVectorFormat PackedVectorFormat4x8BitKHR =
            new PackedVectorFormat(0, "PackedVectorFormat4x8BitKHR");

        public SPIRVWord[] ToWords() =>
            new SPIRVWord[] { SPIRVWord.FromBytes(BitConverter.GetBytes(_value.Data)) };

        public string ToRepr() => _repr;
    }

    internal struct IdResultType : ISPIRVType
    {
        private SPIRVWord _value;

        public IdResultType(SPIRVWord word)
        {
            _value = word;
        }

        public SPIRVWord[] ToWords() => new SPIRVWord[] { _value };

        public string ToRepr() => "%" + _value;

    }
    internal struct IdResult : ISPIRVType
    {
        private SPIRVWord _value;

        public IdResult(SPIRVWord word)
        {
            _value = word;
        }

        public SPIRVWord[] ToWords() => new SPIRVWord[] { _value };

        public string ToRepr() => "%" + _value;

    }
    internal struct IdMemorySemantics : ISPIRVType
    {
        private SPIRVWord _value;

        public IdMemorySemantics(SPIRVWord word)
        {
            _value = word;
        }

        public SPIRVWord[] ToWords() => new SPIRVWord[] { _value };

        public string ToRepr() => "%" + _value;

    }
    internal struct IdScope : ISPIRVType
    {
        private SPIRVWord _value;

        public IdScope(SPIRVWord word)
        {
            _value = word;
        }

        public SPIRVWord[] ToWords() => new SPIRVWord[] { _value };

        public string ToRepr() => "%" + _value;

    }
    internal struct IdRef : ISPIRVType
    {
        private SPIRVWord _value;

        public IdRef(SPIRVWord word)
        {
            _value = word;
        }

        public SPIRVWord[] ToWords() => new SPIRVWord[] { _value };

        public string ToRepr() => "%" + _value;

    }
    internal struct PairLiteralIntegerIdRef : ISPIRVType
    {
        public LiteralInteger base0;
        public IdRef base1;

        public SPIRVWord[] ToWords()
        {
            List<SPIRVWord> words = new List<SPIRVWord>();
            words.AddRange(base0.ToWords());
            words.AddRange(base1.ToWords());
            return words.ToArray();
        }

        public string ToRepr()
        {
            string _repr = "{ ";
            _repr += $"base0 = {base0.ToRepr()} ";
            _repr += $"base1 = {base1.ToRepr()} ";
            _repr += "}";
            return _repr;
        }
    }

    internal struct PairIdRefLiteralInteger : ISPIRVType
    {
        public IdRef base0;
        public LiteralInteger base1;

        public SPIRVWord[] ToWords()
        {
            List<SPIRVWord> words = new List<SPIRVWord>();
            words.AddRange(base0.ToWords());
            words.AddRange(base1.ToWords());
            return words.ToArray();
        }

        public string ToRepr()
        {
            string _repr = "{ ";
            _repr += $"base0 = {base0.ToRepr()} ";
            _repr += $"base1 = {base1.ToRepr()} ";
            _repr += "}";
            return _repr;
        }
    }

    internal struct PairIdRefIdRef : ISPIRVType
    {
        public IdRef base0;
        public IdRef base1;

        public SPIRVWord[] ToWords()
        {
            List<SPIRVWord> words = new List<SPIRVWord>();
            words.AddRange(base0.ToWords());
            words.AddRange(base1.ToWords());
            return words.ToArray();
        }

        public string ToRepr()
        {
            string _repr = "{ ";
            _repr += $"base0 = {base0.ToRepr()} ";
            _repr += $"base1 = {base1.ToRepr()} ";
            _repr += "}";
            return _repr;
        }
    }

}

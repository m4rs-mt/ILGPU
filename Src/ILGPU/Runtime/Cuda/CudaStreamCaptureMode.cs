// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaStreamCaptureMode.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Controls how a stream-capture interacts with other potentially unsafe driver
    /// operations issued (from any thread) while a capture is in progress. Mirrors the
    /// CUDA driver enumeration <c>CUstreamCaptureMode</c>.
    /// </summary>
    public enum CudaStreamCaptureMode
    {
        /// <summary>
        /// Capture is global: potentially unsafe driver calls from any thread are
        /// prohibited for the duration of the capture (the strictest, default mode).
        /// </summary>
        Global = 0,

        /// <summary>
        /// Capture is local to the calling thread: only that thread is restricted.
        /// </summary>
        ThreadLocal = 1,

        /// <summary>
        /// Capture is relaxed: potentially unsafe driver calls are permitted.
        /// </summary>
        Relaxed = 2,
    }
}

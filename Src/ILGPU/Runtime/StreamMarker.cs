// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: StreamMarker.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents a marker used in a stream.
    /// </summary>
    public abstract class StreamMarker : AcceleratorObject
    {
        /// <summary>
        /// Constructs a stream marker.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        protected StreamMarker(Accelerator accelerator)
            : base(accelerator)
        { }

        /// <summary>
        /// Captures the contents of the stream.
        /// </summary>
        public abstract void Record(AcceleratorStream stream);

        /// <summary>
        /// Waits for the stream marker to complete.
        /// </summary>
        public abstract void Synchronize();
    }
}

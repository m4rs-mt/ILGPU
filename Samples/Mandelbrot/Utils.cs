// -----------------------------------------------------------------------------
//                                ILGPU Samples
//                 Copyright (c) 2017-2019 ILGPU Samples Project
//                                www.ilgpu.net
//
// File: Utils.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace Mandelbrot
{
    class Utils
    {
        /// <summary>
        /// Internal stop watch object.
        /// </summary>
        private static Stopwatch _stopWatch;

        /// <summary>
        /// Create a stop watch and start it.
        /// </summary>
        public static void InitWatch()
        {
            _stopWatch = new Stopwatch();
            _stopWatch.Start();
        }

        /// <summary>
        /// Stop the stop watch and print the elapsed time. 
        /// The optional input parameter is printed in front of the time.
        /// </summary>
        /// <param name="str"></param>
        public static void PrintElapsedTime(string str = "Execution Time")
        {
            _stopWatch.Stop();
            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = _stopWatch.Elapsed;

            // Format and display the TimeSpan value.
            string elapsedTime =
                $"{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds:000}   -   {ts.Ticks} Ticks";
            Console.WriteLine(str + " " + elapsedTime);
        }
    }
}

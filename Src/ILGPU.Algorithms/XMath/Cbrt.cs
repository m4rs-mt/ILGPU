// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2021 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: Cbrt.cs
//
// This file was made by Marcel Pawelczyk, to be used freely without restriction by
// the ILGPU project. 
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

namespace ILGPU.Algorithms
{

    partial class XMath
    {
        /// <summary>
        /// Computes 3rd root of n to within double precision.
        /// </summary>
        /// <param name="n">This value's cube root will be calculated.</param>
        /// <param name="checksafety">Enables safety checks.
        /// <para>Default : Enabled / true</para>
        /// <para>This includes checking n for negative and 0 values, Infinities and NaN.</para>
        /// <para>Set to false at own risk for minor speed improvement to disable safety checks.</para>
        /// </param>
        /// <returns>Cbrt(n).</returns>
        public static unsafe double Cbrt(float n, bool checksafety = true)
        {
            // Perform "Safety Check" for; -n, 0, +Inf, -Inf, NaN
            if (checksafety)
            {
                if (n < 0) { return -Cbrt(-n); }
                if (n == 0 || double.IsNaN(n) || double.IsInfinity(n)) { return n; }
            }

            // Initial approximation
            // Convert the binary representation of float into a positive int
            // Isolate & Convert mantissa into actual power it represents
            // Perform cube root on 2^P, to appoximate x using power law
            double x = 1 << (int)((((*(uint*)&n) >> 23) - 127) * 0.33333333333333333333f);

            // Perform check if x^3 matches n
            double xcubed = x * x * x;
            if (xcubed == n) { return x; }

            // Perform 3 itterations of Halley algorithm for double accuracy
            double xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            return x;
        }

        /// <summary>
        /// Computes 3rd root of n to within double precision.
        /// </summary>
        /// <param name="n">This value's cube root will be calculated.</param>
        /// <param name="checksafety">Enables safety checks.
        /// <para>Default : Enabled / true</para>
        /// <para>This includes checking n for negative and 0 values, Infinities and NaN.</para>
        /// <para>Set to false at own risk for minor speed improvement to disable safety checks.</para>
        /// </param>
        /// <returns>Cbrt(n).</returns>
        public static unsafe double Cbrt(double n, bool checksafety = true)
        {
            // Perform "Safety Check" for; -n, 0, +Inf, -Inf, NaN
            if (checksafety)
            {
                if (n < 0) { return -Cbrt(-n); }
                if (n == 0 || double.IsNaN(n) || double.IsInfinity(n)) { return n; }
            }

            // Initial approximation
            // Convert the binary representation of float into a positive int
            // Isolate & Convert mantissa into actual power it represents
            // Perform cube root on 2^P, to appoximate x using power law
            double x = 1 << (int)((((*(uint*)&n) >> 53) - 1023) * 0.33333333333333333333f) + 0b1;

            // Perform check if x^3 matches n
            double xcubed = x * x * x;
            if (xcubed == n) { return x; }

            // Perform 4 itterations of Halley algorithm for double accuracy
            double xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            return x;
        }

        /// <summary>
        /// Computes 3rd root of n to within single precision.
        /// </summary>
        /// <param name="n">This value's cube root will be calculated.</param>
        /// <param name="checksafety">Enables safety checks.
        /// <para>Default : Enabled / true</para>
        /// <para>This includes checking n for negative and 0 values, Infinities and NaN.</para>
        /// <para>Set to false at own risk for minor speed improvement to disable safety checks.</para>
        /// </param>
        /// <returns>Cbrt(n).</returns>
        public unsafe static float CbrtFast(float n, bool checksafety = true)
        {
            if (checksafety)
            {
                if (n < 0) { return -CbrtFast(-n); }
                if (n == 0 || double.IsNaN(n) || double.IsInfinity(n)) { return n; }
            }

            // Initial approximation
            // Convert the binary representation of float into a positive int
            // Isolate & Convert mantissa into actual power it represents
            // Perform cube root on 2^P, to appoximate x using power law
            float x = 1 << (int)((((*(uint*)&n) >> 23) - 127) * 0.33333333333333333333f) + 0b1;


            // Perform 3 itterations of Halley algorithm for float accuracy
            float xcubed = x * x * x;
            if (xcubed == n) { return x; }

            xcubed = x * x * x;
            float xcubedPlusN = xcubed + n;

            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));
            xcubed = x * x * x;

            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));
            xcubed = x * x * x;

            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            return x;
        }

        /// <summary>
        /// Computes 3rd root of n to within single precision.
        /// </summary>
        /// <param name="N">This value's cube root will be calculated.</param>
        /// <param name="checksafety">Enables safety checks.
        /// <para>Default : Enabled / true</para>
        /// <para>This includes checking n for negative and 0 values, Infinities and NaN.</para>
        /// <para>Set to false at own risk for minor speed improvement to disable safety checks.</para>
        /// </param>
        /// <returns>Cbrt(n).</returns>
        public unsafe static float CbrtFast(double N, bool checksafety = true)
        {

            float n = (float)N;

            // Perform "Safety Check" for; -n, 0, +Inf, -Inf, NaN
            if (checksafety)
            {
                if (n < 0) { return -CbrtFast(-n); }
                if (n == 0 || double.IsNaN(n) || double.IsInfinity(n)) { return n; }
            }

            // Initial approximation
            // Convert the binary representation of float into a positive int
            // Isolate & Convert mantissa into actual power it represents
            // Perform cube root on 2^P, to appoximate x using power law
            float x = 1 << (int)((((*(uint*)&N) >> 53) - 1023) * 0.33333333333333333333f) + 0b1;

            // Perform check if x^3 matches n
            float xcubed = x * x * x;
            if (xcubed == n) { return x; }

            // Perform 4 itterations of Halley algorithm for double accuracy
            float xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            xcubed = x * x * x;
            xcubedPlusN = xcubed + n;
            x = x * ((xcubedPlusN + n) / (xcubed + xcubedPlusN));

            return x;
        }




    }

}

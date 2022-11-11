// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Reflection;

namespace ILGPU.Util
{
    /// <summary>
    /// Extensions for methods.
    /// </summary>
    static class MethodExtensions
    {
        /// <summary>
        /// Returns a parameter offset of 1 for instance methods and 0 for static
        /// methods.
        /// </summary>
        /// <param name="method">The method to compute the parameter offset for.</param>
        /// <returns>
        /// A parameter offset of 1 for instance methods and 0 for static methods.
        /// </returns>
        public static int GetParameterOffset(this MethodBase method) =>
            method.IsStatic || method.IsNotCapturingLambda() ? 0 : 1;

        /// <summary>
        /// Returns true if the method can be considered a non-capturing lambda.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True, if the method is a non-capturing lambda.</returns>
        public static bool IsNotCapturingLambda(this MethodBase method)
        {
            if (method.IsStatic)
                return false;

            // C# and F# both create a helper class to represent a lambda - the fields of
            // the class are used to capture the variables. To detect a non-capturing
            // lambda, we look for a class that has no instance fields or properties (so
            // that it cannot have local state).
            //
            // IMPORTANT: We currently do not check for the existance of the
            // CompilerGenerated attribute because F# uses a different attribute
            // i.e. [CompilationMapping(SourceConstructFlags.Closure)], which only exists
            // in F#. As a side-effect, the detection therefore also allows instance
            // methods on any class without instance fields or properties to be
            // considered as a non-capturing lambda.
            //
            // IMPORTANT: It is possible for the lambda to capture static fields and
            // properties, and still pass this detection because we do not inspect the
            // IL instructions here. We are relying on the rest of the compilation to
            // detect invalid cases.
            //
            // NB: In future, this will only apply to C#, as F# has been updated to create
            // a static function for non-capturing lambdas:
            // https://github.com/dotnet/fsharp/tree/596f3d7
            //
            var declaringType = method.DeclaringType;
            return declaringType.IsClass
                && declaringType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public).Length == 0
                && declaringType.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public).Length == 0;
        }
    }
}

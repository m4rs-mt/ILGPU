using System.Runtime.CompilerServices;

namespace ILGPU;


/// <summary>
/// Extension class for Mini52Float8
/// </summary>
public static partial class Mini52Float8Extensions
{
    /// <summary>
    /// The bit mask of the sign bit.
    /// </summary>
    internal const ushort SignBitMask = 0x8000;

    /// <summary>
    /// The bit mask of the exponent and the mantissa.
    /// </summary>
    internal const ushort ExponentMantissaMask = 0x7FFF;
    // 0111 1111 1111 1111 (ignores the sign bit)
    internal const ushort ExponentMask = 0x7F80;
    // 0111 1111 1000 0000 (covers only the exponent)
    internal const ushort MantissaMask = 0x007F;

    /// <summary>
    /// Negate value
    /// </summary>
    /// <param name="Mini52Float8Value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 Neg(Mini52Float8 Mini52Float8Value) =>
        new Mini52Float8((ushort)(Mini52Float8Value.RawValue ^ SignBitMask));



    /// <summary>
    /// Absolute value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 Abs(Mini52Float8 value) =>
        new Mini52Float8((ushort)(value.RawValue & ExponentMantissaMask));

    /// <summary>
    /// Implements a FP16 addition using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 AddFP32(Mini52Float8 first, Mini52Float8 second) =>
        (Mini52Float8)((float)first + (float)second);

    /// <summary>
    /// Implements a FP16 subtraction using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 SubFP32(Mini52Float8 first, Mini52Float8 second) =>
        (Mini52Float8)((float)first - (float)second);

    /// <summary>
    /// Implements a FP16 multiplication using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 MulFP32(Mini52Float8 first, Mini52Float8 second) =>
        (Mini52Float8)((float)first * (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 DivFP32(Mini52Float8 first, Mini52Float8 second) =>
        (Mini52Float8)((float)first / (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first Mini52Float8.</param>
    /// <param name="second">The second Mini52Float8.</param>
    /// <param name="third">The third Mini52Float8.</param>
    /// <returns>The resulting Mini52Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini52Float8 FmaFP32(Mini52Float8 first, Mini52Float8 second, Mini52Float8 third) =>
        (Mini52Float8)((float)first * (float)second + (float)third);


}

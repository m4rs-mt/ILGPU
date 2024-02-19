using System.Runtime.CompilerServices;

namespace ILGPU;


/// <summary>
/// Extension class for Mini43Float8
/// </summary>
public static partial class Mini43Float8Extensions
{
    /// <summary>
    /// The bit mask of the sign bit.
    /// </summary>
    internal const byte SignBitMask = 0x80;

    /// <summary>
    /// The bit mask of the exponent and the mantissa.
    /// </summary>
    internal const byte ExponentMantissaMask = 0x7F;
    // 0111 1111 (ignores the sign bit)

    internal const byte ExponentMask = 0x78;
    // 0111 1000 (covers only the exponent)

    internal const byte MantissaMask = 0x07;
    // 0000 0111 (covers only the mantissa)

    /// <summary>
    /// Negate value
    /// </summary>
    /// <param name="Mini43Float8Value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43Float8 Neg(Mini43Float8 Mini43Float8Value) =>
        new Mini43Float8((byte)(Mini43Float8Value.RawValue ^ SignBitMask));



    /// <summary>
    /// Absolute value
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43Float8 Abs(Mini43Float8 value) =>
        new Mini43Float8((byte)(value.RawValue & ExponentMantissaMask));

    /// <summary>
    /// Implements a FP16 addition using FP32.
    /// </summary>
    /// <param name="first">The first Mini43Float8.</param>
    /// <param name="second">The second Mini43Float8.</param>
    /// <returns>The resulting Mini43Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43Float8 AddFP32(Mini43Float8 first, Mini43Float8 second) =>
        (Mini43Float8)((float)first + (float)second);

    /// <summary>
    /// Implements a FP16 subtraction using FP32.
    /// </summary>
    /// <param name="first">The first Mini43Float8.</param>
    /// <param name="second">The second Mini43Float8.</param>
    /// <returns>The resulting Mini43Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43Float8 SubFP32(Mini43Float8 first, Mini43Float8 second) =>
        (Mini43Float8)((float)first - (float)second);

    /// <summary>
    /// Implements a FP16 multiplication using FP32.
    /// </summary>
    /// <param name="first">The first Mini43Float8.</param>
    /// <param name="second">The second Mini43Float8.</param>
    /// <returns>The resulting Mini43Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43Float8 MulFP32(Mini43Float8 first, Mini43Float8 second) =>
        (Mini43Float8)((float)first * (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first Mini43Float8.</param>
    /// <param name="second">The second Mini43Float8.</param>
    /// <returns>The resulting Mini43Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43Float8 DivFP32(Mini43Float8 first, Mini43Float8 second) =>
        (Mini43Float8)((float)first / (float)second);

    /// <summary>
    /// Implements a FP16 division using FP32.
    /// </summary>
    /// <param name="first">The first Mini43Float8.</param>
    /// <param name="second">The second Mini43Float8.</param>
    /// <param name="third">The third Mini43Float8.</param>
    /// <returns>The resulting Mini43Float8 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mini43Float8 FmaFP32(Mini43Float8 first, Mini43Float8 second, Mini43Float8 third) =>
        (Mini43Float8)((float)first * (float)second + (float)third);


}

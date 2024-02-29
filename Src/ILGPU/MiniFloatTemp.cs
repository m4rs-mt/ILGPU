using System.Runtime.CompilerServices;

namespace ILGPU;

public class MiniFloatTemp
{

    private static readonly byte[] exponentToMiniLookupTable;

    private static Mini43Float8 SingleToMini43Float8(float value)
    {
        // Extracting the binary representation of the float value
        uint floatBits = Unsafe.As<float, uint>(ref value);

        byte exponentIndex = (byte)((floatBits >> 23) & 0xFF); // Extract 8-bit exponent

        if (exponentIndex == 0xFF && (floatBits & 0x007FFFFF) != 0)
        {
            return Mini43Float8.NaN;
            // Assuming Mini43Float8.NaN is a predefined value for NaN
        }

        // Extracting sign (1 bit)
        byte sign = (byte)((floatBits >> 24) & 0x80); // Extract sign bit

        // Extract mantissa bits for rounding
        uint mantissaBits = (floatBits & 0x007FFFFF);


        // Using the lookup table to convert the exponent
        byte exponent = exponentToMiniLookupTable[exponentIndex];
        // Convert using the lookup table

        byte mantissa = (byte)((mantissaBits >> 20) & 0x7); // Direct extraction
        byte roundBit = (byte)((mantissaBits >> 19) & 0x1);
        byte stickyBit = (byte)((mantissaBits & 0x0007FFFF) > 0 ? 1 : 0);

        // Rounding
        if (roundBit == 1 && (stickyBit == 1 || (mantissa & 0x1) == 1)) {
            mantissa++;
            if (mantissa == 0x8) {
                mantissa = 0;
                if (++exponent == 0xF) { // Simplified handling for overflow
                    exponent = 0xF; // Max value for 4-bit exponent
                }
            }
        }

        // Combining into Mini43Float8 format
        // (1 bit sign, 5 bits exponent, 2 bits mantissa)
        byte mini43Float8 = (byte)(sign | (exponent << 3) | mantissa);

        return new Mini43Float8(mini43Float8);
    }


    private static Mini52Float8 SingleToMini52Float8(float value)
    {
        // Extracting the binary representation of the float value
        uint floatBits = Unsafe.As<float, uint>(ref value);

        byte exponentIndex = (byte)((floatBits >> 23) & 0xFF); // Extract 8-bit exponent

        // Extracting sign (1 bit)
        byte sign = (byte)((floatBits >> 24) & 0x80); // Extract sign bit

        // Extract mantissa bits for rounding
        uint mantissaBits = (floatBits & 0x007FFFFF);

        if (exponentIndex == 0xFF)
        {
            if (mantissaBits == 0) // Infinity check
            {
                if (sign != 0) // Positive Infinity
                    return Mini52Float8.NegativeInfinity;
                else // Negative Infinity
                    return Mini52Float8.PositiveInfinity;
            }
            else // NaN check
            {
                return Mini52Float8.NaN;
            }
        }

        // Using the lookup table to convert the exponent
        byte exponent = exponentToMiniLookupTable[exponentIndex];
        // Convert using the lookup table

        byte mantissa = (byte)((mantissaBits >> 21) & 0x3); // Direct extraction
        byte roundBit = (byte)((mantissaBits >> 20) & 0x1);
        byte stickyBit = (byte)((mantissaBits & 0x000FFFFF) > 0 ? 1 : 0);

        // Rounding
        if (roundBit == 1 && (stickyBit == 1 || (mantissa & 0x1) == 1)) {
            mantissa++;
            if (mantissa == 0x4) {
                mantissa = 0;
                if (++exponent == 0x20) { // Simplified handling for overflow
                    exponent = 0x1F; // Max value for 5-bit exponent
                }
            }
        }

        // Combining into Mini52Float8 format
        // (1 bit sign, 5 bits exponent, 2 bits mantissa)
        byte mini52Float8 = (byte)(sign | (exponent << 2) | mantissa);

        return new Mini52Float8(mini52Float8);
    }

}

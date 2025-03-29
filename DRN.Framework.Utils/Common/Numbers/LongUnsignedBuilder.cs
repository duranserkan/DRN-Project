namespace DRN.Framework.Utils.Common.Numbers;

public class LongUnsignedBuilder(NumberBuildDirection direction)
{
    private ulong _value;
    private byte _nibbleCount;
    
    /// <summary>
    /// Adds a 4-bit nibble to the long, starting from the most significant bits.
    /// </summary>
    /// <param name="nibble">
    /// A value between 0x0 (0) and 0xF (15) (4 bits). Values surpassing 15 are truncated to 15.
    /// </param>
    /// <returns>true if added. ulong has 16 available nibble slots. It can't be added more than that. Call reset before creating new ulong number.</returns>
    public bool TryAddNibble(byte nibble)
    {
        if (_nibbleCount >= 16)
            return false;

        if (nibble > 15)
            nibble = 15;

        // Mask to ensure only 4 bits are used
        var maskedNibble = (byte)(nibble & 0x0F);

        // Calculate the shift position for the next nibble
        var shift = direction == NumberBuildDirection.MostSignificantFirst
            ? 60 - 4 * _nibbleCount
            : 4 * _nibbleCount;
        
        _value |= (ulong)maskedNibble << shift;

        _nibbleCount++;
        
        return true;
    }
    
    public ulong GetValue() => _value;
    public byte GetNibbleCount() => _nibbleCount;

    /// <summary>
    /// Resets the builder to start constructing a new value. Call this method to set Value and NibbleCount to 0.
    /// </summary>
    public void Reset()
    {
        _value = 0;
        _nibbleCount = 0;
    }
}
namespace DRN.Framework.Utils.Common.Numbers;

//todo add longbuilder xml summaries
public class LongBuilder
{
    public static LongBuilder Default => new(NumberBuildDirection.MostSignificantFirst, ResidueType.UInt);
    
    private long _residue;
    private bool _signBit = true;
    private long _value = long.MinValue;

    private int _currentBitOffset;
    private readonly int _availableBits; //offset from most significant bit, also mean total available bits
    private readonly int _residueBitLength;
    private readonly NumberBuildDirection _direction;

    public LongBuilder(NumberBuildDirection direction, ResidueType residueType)
    {
        _direction = direction;
        _residueBitLength = residueType.GetResidueBitLength();
        _availableBits = 64 - _residueBitLength - 1; //1 bit spared for sign
        _currentBitOffset = 0;
    }

    public bool TryAddNibble(byte nibble)
    {
        const int bitSize = 4;
        return TryAdd(nibble, bitSize, 0x0F);
    }

    public bool TryAddByte(byte byt)
    {
        const int bitSize = 8;
        return TryAdd(byt, bitSize, byte.MaxValue);
    }

    public bool TryAddUnsignedShort(ushort unsignedShort)
    {
        const int bitSize = 16;
        return TryAdd(unsignedShort, bitSize, ushort.MaxValue);
    }

    public bool TryAddUnsignedInt(uint unsignedInt)
    {
        const int bitSize = 32;
        return TryAdd(unsignedInt, bitSize, uint.MaxValue);
    }

    private bool TryAdd(uint nibble, int bitSize, uint mask)
    {
        if (!ValidateWriteOperation(bitSize))
            return false;

        var shift = CalculateShift(bitSize);
        long maskedNibble = nibble & mask;
        _value |= maskedNibble << shift;
        _currentBitOffset += bitSize;

        return true;
    }

    private bool ValidateWriteOperation(int bitSize)
    {
        var currentAvailableBits = _availableBits - _currentBitOffset;

        return bitSize <= currentAvailableBits;
    }

    private int CalculateShift(int bitSize) =>
        _direction == NumberBuildDirection.MostSignificantFirst
            ? _availableBits - _currentBitOffset - bitSize
            : _currentBitOffset;

    public void SetResidueValue(uint value)
    {
        var mask = (1L << _residueBitLength) - 1; //1L << 4 => ...10000 --- (1L << 4 -1) => 01111
        _residue = value & mask;
        _value |= _residue << _availableBits;
    }

    public void MakeNegative() => UpdateSignBit(true);

    public void MakePositive() => UpdateSignBit(false);

    private void UpdateSignBit(bool signBit)
    {
        _value = signBit ? _value | (1L << 63) : _value & ~(1L << 63);
        _signBit = signBit;
    }

    public long GetValue() => _value;
    public int GetResidue() => (int)_residue;
    public bool IsPositive() => !_signBit;
    public bool IsNegative() => _signBit;

    public void Reset()
    {
        _residue = 0;
        _signBit = true;
        _value = long.MinValue;
        _currentBitOffset = 0;
    }
}
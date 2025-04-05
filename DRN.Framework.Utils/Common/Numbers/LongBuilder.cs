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
    private readonly byte _residueBitLength;
    private readonly NumberBuildDirection _direction;

    public LongBuilder(NumberBuildDirection direction, ResidueType residueType)
    {
        _direction = direction;
        _residueBitLength = residueType.GetResidueBitLength();
        _availableBits = 64 - _residueBitLength - 1; //1 bit spared for sign
        _currentBitOffset = 0;
    }

    public bool TryAddBit(byte bit)
    {
        const byte bitLength = 1;
        return TryAdd(bit, bitLength);
    }

    public bool TryAddCrumb(byte crumb)
    {
        const byte bitLength = 2;
        return TryAdd(crumb, bitLength);
    }

    public bool TryAddNibble(byte nibble)
    {
        const byte bitLength = 4;
        return TryAdd(nibble, bitLength);
    }

    public bool TryAddByte(byte byt)
    {
        const byte bitLength = 8;
        return TryAdd(byt, bitLength);
    }

    public bool TryAddUnsignedShort(ushort unsignedShort)
    {
        const byte bitLength = 16;
        return TryAdd(unsignedShort, bitLength);
    }

    public bool TryAddUnsignedInt(uint unsignedInt)
    {
        const byte bitLength = 32;
        return TryAdd(unsignedInt, bitLength);
    }

    public bool TryAdd(uint value, byte bitLength)
    {
        if (!ValidateWriteOperation(bitLength))
            return false;

        var maskedValue = value & bitLength.GetBitMask();
        _value |= maskedValue << CalculateShift(bitLength);
        _currentBitOffset += bitLength;

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
        _residue = value & _residueBitLength.GetBitMask();
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
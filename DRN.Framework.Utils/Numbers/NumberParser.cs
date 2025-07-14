namespace DRN.Framework.Utils.Numbers;

public class IntParser(int value, NumberBuildDirection direction, byte residueBitLength)
    : NumberParser(direction, 32, residueBitLength, true, value);

public class IntUnsignedParser(uint value, NumberBuildDirection direction)
    : NumberParser(direction, 32, 0, false, unsignedValue: value);

public class LongParser(long value, NumberBuildDirection direction, byte residueBitLength)
    : NumberParser(direction, 64, residueBitLength, true, value);

public class LongUnsignedParser(ulong value, NumberBuildDirection direction)
    : NumberParser(direction, 64, 0, false, unsignedValue: value);

public abstract class NumberParser
{
    public static IntParser Default(int value) => new(value, NumberBuildDirection.MostSignificantFirst, 15);
    public static IntUnsignedParser Default(uint value) => new(value, NumberBuildDirection.MostSignificantFirst);
    public static LongUnsignedParser Default(ulong value) => new(value, NumberBuildDirection.MostSignificantFirst);
    public static LongParser Default(long value) => new(value, NumberBuildDirection.MostSignificantFirst, 31);

    private readonly NumberBuildDirection _direction;
    private readonly bool _signed;
    private readonly byte _residueBitLength;
    private readonly byte _availableBitLength;

    private long _signedValue;
    private ulong _unsignedValue;
    private int _currentBitOffset;

    protected NumberParser(NumberBuildDirection direction, byte bitLength, byte residueBitLength, bool signed, long signedValue = 0, ulong unsignedValue = 0)
    {
        _direction = direction;
        _residueBitLength = residueBitLength;
        _signed = signed;
        _signedValue = signedValue;
        _unsignedValue = unsignedValue;
        _availableBitLength = signed
            ? (byte)(bitLength - residueBitLength - 1) //1 bit spared for sign
            : bitLength;
    }

    //offset from most significant bit, also mean total available bits

    private void ValidateReadOperation(int bitSize)
    {
        var currentAvailableBits = _availableBitLength - _currentBitOffset;

        if (bitSize > currentAvailableBits)
            throw new InvalidOperationException($"Attempt to read {bitSize} bits exceeds available {currentAvailableBits} bits.");
    }

    private int CalculateShift(int bitSize) => _direction == NumberBuildDirection.MostSignificantFirst
        ? _availableBitLength - _currentBitOffset - bitSize
        : _currentBitOffset;

    public uint ReadResidueValue() => (uint)((_signedValue >> _availableBitLength) & _residueBitLength.GetBitMaskSigned());
    public byte ReadBit() => (byte)Read(1);
    public byte ReadCrumb() => (byte)Read(2);
    public byte ReadNibble() => (byte)Read(4);
    public byte ReadByte() => (byte)Read(8);
    public ushort ReadUShort() => (ushort)Read(16);
    public uint ReadUInt() => Read(32);

    public uint Read(byte bitSize)
    {
        ValidateReadOperation(bitSize);

        var shift = CalculateShift(bitSize);
        var value = _signed
            ? (uint)((_signedValue >> shift) & bitSize.GetBitMaskSigned())
            : (uint)((_unsignedValue >> shift) & bitSize.GetBitMaskUnsigned());

        _currentBitOffset += bitSize;

        return value;
    }

    public void ResetToParse(long signedValue)
    {
        _signedValue = signedValue;
        _currentBitOffset = 0;
    }

    public void ResetToParse(ulong unsignedValue)
    {
        _unsignedValue = unsignedValue;
        _currentBitOffset = 0;
    }
}
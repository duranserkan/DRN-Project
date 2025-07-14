namespace DRN.Framework.Utils.Numbers;

public struct NumberParser
{
    public static NumberParser Get(int value, NumberBuildDirection direction = NumberBuildDirection.MostSignificantFirst, byte residueBitLength = 15)
        => new(direction, 32, residueBitLength, true, value);

    public static NumberParser Get(long value, NumberBuildDirection direction = NumberBuildDirection.MostSignificantFirst, byte residueBitLength = 31)
        => new(direction, 64, residueBitLength, true, value);

    public static NumberParser Get(uint value, NumberBuildDirection direction = NumberBuildDirection.MostSignificantFirst)
        => new(direction, 32, 0, false, unsignedValue: value);

    public static NumberParser Get(ulong value, NumberBuildDirection direction = NumberBuildDirection.MostSignificantFirst)
        => new(direction, 64, 0, false, unsignedValue: value);


    private readonly NumberBuildDirection _direction;
    private readonly bool _signed;
    private readonly byte _residueBitLength;
    private readonly byte _availableBitLength;

    private long _signedValue;
    private ulong _unsignedValue;
    private int _currentBitOffset;

    private NumberParser(NumberBuildDirection direction, byte bitLength, byte residueBitLength, bool signed, long signedValue = 0, ulong unsignedValue = 0)
    {
        _direction = direction;
        _residueBitLength = residueBitLength;
        _signed = signed;
        _signedValue = signedValue;
        _unsignedValue = unsignedValue;

        //offset from most significant bit, also mean total available bits
        _availableBitLength = signed
            ? (byte)(bitLength - residueBitLength - 1) //1 bit spared for sign
            : bitLength;
    }

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
        
        var value = _signed
            ? (uint)((_signedValue >> CalculateShift(bitSize)) & bitSize.GetBitMaskSigned())
            : (uint)((_unsignedValue >> CalculateShift(bitSize)) & bitSize.GetBitMaskUnsigned());

        _currentBitOffset += bitSize;

        return value;
    }

    //todo add tests
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
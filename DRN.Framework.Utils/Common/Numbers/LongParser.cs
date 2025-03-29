namespace DRN.Framework.Utils.Common.Numbers;

public class LongParser
{
    public static LongParser Default(long value) => new(value, NumberBuildDirection.MostSignificantFirst, ResidueType.UInt);

    private int _currentBitOffset;
    private readonly int _availableBits; //offset from most significant bit, also mean total available bits
    private readonly int _residueBitLength;
    private readonly long _value;
    private readonly NumberBuildDirection _direction;

    public LongParser(long value, NumberBuildDirection direction, ResidueType residueType)
    {
        _value = value;
        _direction = direction;
        _residueBitLength = residueType.GetResidueBitLength();
        _availableBits = 64 - _residueBitLength - 1; //1 bit spared for sign
        _currentBitOffset = 0;
    }

    private void ValidateReadOperation(int bitSize)
    {
        var currentAvailableBits = _availableBits - _currentBitOffset;

        if (bitSize > currentAvailableBits)
            throw new InvalidOperationException($"Attempt to read {bitSize} bits exceeds available {currentAvailableBits} bits.");
    }

    private int CalculateShift(int bitSize)
    {
        return _direction == NumberBuildDirection.MostSignificantFirst
            ? _availableBits - _currentBitOffset - bitSize
            : _currentBitOffset;
    }

    public byte ReadNibble()
    {
        const int bitSize = 4;
        ValidateReadOperation(bitSize);

        var shift = CalculateShift(bitSize);
        var value = (byte)((_value >> shift) & 0x0F);
        _currentBitOffset += bitSize;

        return value;
    }

    public byte ReadByte()
    {
        const int bitSize = 8;
        ValidateReadOperation(bitSize);

        var shift = CalculateShift(bitSize);
        var value = (byte)((_value >> shift) & 0xFF);
        _currentBitOffset += bitSize;

        return value;
    }

    public ushort ReadUShort()
    {
        const int bitSize = 16;
        ValidateReadOperation(bitSize);

        var shift = CalculateShift(bitSize);
        var value = (ushort)((_value >> shift) & 0xFFFF);
        _currentBitOffset += bitSize;

        return value;
    }

    public uint ReadUInt()
    {
        const int bitSize = 32;
        ValidateReadOperation(bitSize);

        var shift = CalculateShift(bitSize);
        var value = (uint)((_value >> shift) & 0xFFFFFFFF);
        _currentBitOffset += bitSize;

        return value;
    }

    public uint ReadResidueValue()
    {
        var mask = (1L << _residueBitLength) - 1; //1L << 4 => ...10000 --- (1L << 4 -1) => 01111
        return (uint)((_value >> _availableBits) & mask);
    }

    public void Reset() => _currentBitOffset = 0;
}
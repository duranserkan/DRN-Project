namespace DRN.Framework.Utils.Numbers;

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

    private int CalculateShift(int bitSize) => _direction == NumberBuildDirection.MostSignificantFirst
        ? _availableBits - _currentBitOffset - bitSize
        : _currentBitOffset;

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
        var value = (uint)((_value >> shift) & bitSize.GetBitMask());

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
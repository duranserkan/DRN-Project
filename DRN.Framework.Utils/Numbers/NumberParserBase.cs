namespace DRN.Framework.Utils.Numbers;

public abstract class NumberParserSignedBase(NumberBuildDirection direction, byte bitLength, byte residueBitLength, long value)
    : NumberParserBase(direction, bitLength, true, signedValue: value)
{
    protected override byte AvailableBitLength { get; } = (byte)(bitLength - residueBitLength - 1); //1 bit spared for sign

    public uint ReadResidueValue() => (uint)((SignedValue >> AvailableBitLength) & residueBitLength.GetBitMaskSigned());
}

public abstract class NumberParserUnsignedBase(NumberBuildDirection direction, byte bitLength, ulong value)
    : NumberParserBase(direction, bitLength, false, unsignedValue: value)
{
    protected override byte AvailableBitLength { get; } = bitLength;
}

public abstract class NumberParserBase(NumberBuildDirection direction, byte bitLength, bool signed, long signedValue = 0, ulong unsignedValue = 0)
{
    private int _currentBitOffset;

    protected readonly ulong UnsignedValue = unsignedValue;
    protected readonly long SignedValue = signedValue;

    protected readonly byte BitLength = bitLength;
    protected abstract byte AvailableBitLength { get; } //offset from most significant bit, also mean total available bits

    private void ValidateReadOperation(int bitSize)
    {
        var currentAvailableBits = AvailableBitLength - _currentBitOffset;

        if (bitSize > currentAvailableBits)
            throw new InvalidOperationException($"Attempt to read {bitSize} bits exceeds available {currentAvailableBits} bits.");
    }

    private int CalculateShift(int bitSize) => direction == NumberBuildDirection.MostSignificantFirst
        ? AvailableBitLength - _currentBitOffset - bitSize
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
        var value = signed
            ? (uint)((SignedValue >> shift) & bitSize.GetBitMaskSigned())
            : (uint)((UnsignedValue >> shift) & bitSize.GetBitMaskUnsigned());

        _currentBitOffset += bitSize;

        return value;
    }

    public void Reset() => _currentBitOffset = 0;
}
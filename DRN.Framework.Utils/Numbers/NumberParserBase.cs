namespace DRN.Framework.Utils.Numbers;

//todo evaluate struct implementation/object pooling for high performance scenarios
public abstract class NumberParserBase(NumberBuildDirection direction, byte bitLength, byte residueBitLength, bool signed, long signedValue = 0, ulong unsignedValue = 0)
{
    private int _currentBitOffset;

    //offset from most significant bit, also mean total available bits
    private byte AvailableBitLength { get; } = signed
        ? (byte)(bitLength - residueBitLength - 1) //1 bit spared for sign
        : bitLength;

    private void ValidateReadOperation(int bitSize)
    {
        var currentAvailableBits = AvailableBitLength - _currentBitOffset;

        if (bitSize > currentAvailableBits)
            throw new InvalidOperationException($"Attempt to read {bitSize} bits exceeds available {currentAvailableBits} bits.");
    }

    private int CalculateShift(int bitSize) => direction == NumberBuildDirection.MostSignificantFirst
        ? AvailableBitLength - _currentBitOffset - bitSize
        : _currentBitOffset;

    public uint ReadResidueValue() => (uint)((signedValue >> AvailableBitLength) & residueBitLength.GetBitMaskSigned());
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
            ? (uint)((signedValue >> shift) & bitSize.GetBitMaskSigned())
            : (uint)((unsignedValue >> shift) & bitSize.GetBitMaskUnsigned());

        _currentBitOffset += bitSize;

        return value;
    }

    public void Reset() => _currentBitOffset = 0;
}
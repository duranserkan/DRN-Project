namespace DRN.Framework.Utils.Numbers;

//todo evaluate struct implementation/object pooling for high performance scenarios
public abstract class NumberBuilderUnsignedBase(NumberBuildDirection direction, byte bitLength) : NumberBuilderBase(direction, bitLength, false)
{
    protected override byte AvailableBitLength { get; } = bitLength;
}

public abstract class NumberBuilderSignedBase : NumberBuilderBase
{
    private long _residue;
    private bool _signBit = true;

    private readonly byte _residueBitLength;
    protected override byte AvailableBitLength { get; }

    protected NumberBuilderSignedBase(NumberBuildDirection direction, byte residueBitLength, byte bitLength) : base(direction, bitLength, true)
    {
        _residueBitLength = residueBitLength;
        AvailableBitLength = (byte)(bitLength - _residueBitLength - 1); //1 bit spared for sign
    }

    public void SetResidueValue(uint value)
    {
        _residue = value & _residueBitLength.GetBitMaskSigned();
        SignedValue |= _residue << AvailableBitLength;
    }

    public void MakeNegative() => UpdateSignBit(true);

    public void MakePositive() => UpdateSignBit(false);

    private void UpdateSignBit(bool signBit)
    {
        SignedValue = signBit ? SignedValue | (1L << (BitLength - 1)) : SignedValue & ~(1L << (BitLength - 1));
        _signBit = signBit;
    }

    public int GetResidue() => (int)_residue;
    public bool IsPositive() => !_signBit;
    public bool IsNegative() => _signBit;

    /// <summary>
    /// Resets the builder to start constructing a new value.
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        _residue = 0;
        _signBit = true;
    }
}

public abstract class NumberBuilderBase(NumberBuildDirection direction, byte bitLength, bool signed)
{
    private int _currentBitOffset;

    protected readonly byte BitLength = bitLength;
    protected abstract byte AvailableBitLength { get; } //offset from most significant bit, also mean total available bits

    protected ulong UnsignedValue = ulong.MinValue;
    protected long SignedValue = long.MinValue;

    public bool TryAddBit(byte bit) => TryAdd(bit, 1);
    public bool TryAddCrumb(byte crumb) => TryAdd(crumb, 2);
    public bool TryAddNibble(byte nibble) => TryAdd(nibble, 4);
    public bool TryAddByte(byte byt) => TryAdd(byt, 8);
    public bool TryAddUShort(ushort unsignedShort) => TryAdd(unsignedShort, 16);
    public bool TryAddUInt(uint unsignedInt) => TryAdd(unsignedInt, 32);

    public bool TryAdd(uint value, byte bitLength)
    {
        if (!ValidateWriteOperation(bitLength))
            return false;

        if (signed)
            SignedValue |= (value & bitLength.GetBitMaskSigned()) << CalculateShift(bitLength);
        else
            UnsignedValue |= (value & bitLength.GetBitMaskUnsigned()) << CalculateShift(bitLength);

        _currentBitOffset += bitLength;

        return true;
    }

    private bool ValidateWriteOperation(int bitSize) => bitSize <= (AvailableBitLength - _currentBitOffset);

    private int CalculateShift(int bitSize) => direction == NumberBuildDirection.MostSignificantFirst
        ? AvailableBitLength - _currentBitOffset - bitSize
        : _currentBitOffset;

    /// <summary>
    /// Resets the builder to start constructing a new value.
    /// </summary>
    public virtual void Reset()
    {
        UnsignedValue = ulong.MinValue;
        SignedValue = long.MinValue;
        _currentBitOffset = 0;
    }
}
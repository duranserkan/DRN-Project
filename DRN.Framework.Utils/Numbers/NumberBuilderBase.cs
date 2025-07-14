namespace DRN.Framework.Utils.Numbers;

public abstract class NumberBuilderBase
{
    private int _currentBitOffset;

    private long _residue;
    private bool _signBit = true;
    private readonly bool _signed;

    private readonly byte _residueBitLength;
    private readonly byte _bitLength;
    private byte AvailableBitLength { get; } //offset from most significant bit, also mean total available bits

    protected ulong UnsignedValue = ulong.MinValue;
    protected long SignedValue = long.MinValue;
    private readonly NumberBuildDirection _direction;

    protected NumberBuilderBase(NumberBuildDirection direction, byte bitLength, byte residueBitLength, bool signed)
    {
        _direction = direction;
        _signed = signed;
        _residueBitLength = residueBitLength;
        _bitLength = bitLength;

        if (_signed)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(residueBitLength);
            _residueBitLength = residueBitLength;
            AvailableBitLength = (byte)(bitLength - _residueBitLength - 1); //1 bit spared for sign
        }
        else
            AvailableBitLength = bitLength;
    }

    
    public int GetResidue() => (int)_residue;

    public void SetResidueValue(uint value)
    {
        if (!_signed) return;

        _residue = value & _residueBitLength.GetBitMaskSigned();
        SignedValue |= _residue << AvailableBitLength;
    }

    public bool IsPositive() => !_signBit;
    public bool IsNegative() => _signBit;
    public void MakeNegative() => UpdateSignBit(true);
    public void MakePositive() => UpdateSignBit(false);

    private void UpdateSignBit(bool signBit)
    {
        if (!_signed) return;

        SignedValue = signBit ? SignedValue | (1L << (_bitLength - 1)) : SignedValue & ~(1L << (_bitLength - 1));
        _signBit = signBit;
    }


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

        if (_signed)
            SignedValue |= (value & bitLength.GetBitMaskSigned()) << CalculateShift(bitLength);
        else
            UnsignedValue |= (value & bitLength.GetBitMaskUnsigned()) << CalculateShift(bitLength);

        _currentBitOffset += bitLength;

        return true;
    }

    private bool ValidateWriteOperation(int bitSize) => bitSize <= (AvailableBitLength - _currentBitOffset);

    private int CalculateShift(int bitSize) => _direction == NumberBuildDirection.MostSignificantFirst
        ? AvailableBitLength - _currentBitOffset - bitSize
        : _currentBitOffset;
    
    /// <summary>
    /// Resets the builder to start constructing a new value.
    /// </summary>
    public void Reset()
    {
        UnsignedValue = ulong.MinValue;
        SignedValue = long.MinValue;
        _currentBitOffset = 0;
        _residue = 0;
        _signBit = true;
    }
}
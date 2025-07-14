using System.Numerics;

namespace DRN.Framework.Utils.Numbers;

public static class NumberBuilder
{
    public static NumberBuilder<int> GetInt(NumberBuildDirection direction = NumberBuildDirection.MostSignificantFirst, byte residueBitLength = 15) =>
        new(direction, 32, residueBitLength, true);

    public static NumberBuilder<long> GetLong(NumberBuildDirection direction = NumberBuildDirection.MostSignificantFirst, byte residueBitLength = 31) =>
        new(direction, 64, residueBitLength, true);


    public static NumberBuilder<ulong> GetLongUnsigned(NumberBuildDirection direction = NumberBuildDirection.MostSignificantFirst) =>
        new(direction, 64, 0, false);

    public static NumberBuilder<uint> GetIntUnsigned(NumberBuildDirection direction = NumberBuildDirection.MostSignificantFirst) =>
        new(direction, 32, 0, false);
}

public struct NumberBuilder<TNumber> where TNumber : struct, IBinaryInteger<TNumber>
{
    private long _residue;
    private bool _signBit = true;
    private readonly bool _signed;

    private readonly byte _bitLength;
    private readonly byte _residueBitLength;
    private readonly byte _availableBitLength; //offset from most significant bit, also mean total available bits
    private readonly NumberBuildDirection _direction;

    private int _currentBitOffset;
    private ulong _unsignedValue = ulong.MinValue;
    private long _signedValue = long.MinValue;

    internal NumberBuilder(NumberBuildDirection direction, byte bitLength, byte residueBitLength, bool signed)
    {
        _direction = direction;
        _signed = signed;
        _residueBitLength = residueBitLength;
        _bitLength = bitLength;

        if (_signed)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(residueBitLength);
            _residueBitLength = residueBitLength;
            _availableBitLength = (byte)(bitLength - _residueBitLength - 1); //1 bit spared for sign
        }
        else
            _availableBitLength = bitLength;
    }

    public TNumber GetValue() => _signed
        ? TNumber.CreateTruncating(_signedValue)
        : TNumber.CreateTruncating(_unsignedValue);

    public int GetResidue() => (int)_residue;

    public void SetResidueValue(uint value)
    {
        if (!_signed) return;

        _residue = value & _residueBitLength.GetBitMaskSigned();
        _signedValue |= _residue << _availableBitLength;
    }

    public bool IsPositive() => !_signBit;
    public bool IsNegative() => _signBit;
    public void MakeNegative() => UpdateSignBit(true);
    public void MakePositive() => UpdateSignBit(false);

    private void UpdateSignBit(bool signBit)
    {
        if (!_signed) return;

        _signedValue = signBit ? _signedValue | (1L << (_bitLength - 1)) : _signedValue & ~(1L << (_bitLength - 1));
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
            _signedValue |= (value & bitLength.GetBitMaskSigned()) << CalculateShift(bitLength);
        else
            _unsignedValue |= (value & bitLength.GetBitMaskUnsigned()) << CalculateShift(bitLength);

        _currentBitOffset += bitLength;

        return true;
    }

    private bool ValidateWriteOperation(int bitSize) => bitSize <= (_availableBitLength - _currentBitOffset);

    private int CalculateShift(int bitSize) => _direction == NumberBuildDirection.MostSignificantFirst
        ? _availableBitLength - _currentBitOffset - bitSize
        : _currentBitOffset;

    /// <summary>
    /// Resets the builder to start constructing a new value.
    /// </summary>
    public void Reset()
    {
        _unsignedValue = ulong.MinValue;
        _signedValue = long.MinValue;
        _currentBitOffset = 0;
        _residue = 0;
        _signBit = true;
    }
}
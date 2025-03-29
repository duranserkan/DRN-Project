namespace DRN.Framework.Utils.Extensions;

public static class LongUtils
{
    public static BitPositions GetBitPositions(this long value)
    {
        var ones = new List<byte>(64);
        var zeros = new List<byte>(64);
        for (byte i = 0; i < 64; i++)
        {
            var mask = 1L << i;
            if ((value & mask) == 0)
                zeros.Add(i);
            else
                ones.Add(i);
        }

        return new BitPositions(ones, zeros);
    }
}

public record BitPositions(IReadOnlyList<byte> Ones, IReadOnlyList<byte> Zeros);
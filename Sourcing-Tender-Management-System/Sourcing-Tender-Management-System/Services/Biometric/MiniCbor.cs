using System.Text;

namespace Sourcing_Tender_Management_System.Services.Biometric;

/// <summary>
/// A small, strict CBOR reader: just what WebAuthn needs (attestation objects and COSE keys).
/// Integers become long, byte strings byte[], text strings string, arrays List, maps Dictionary
/// (keys are long or string). Indefinite lengths and floating point values are rejected.
/// </summary>
public static class MiniCbor
{
    private const int MaxDepth = 8;
    private const ulong MaxItems = 64;

    public static object? Read(ReadOnlySpan<byte> data, out int consumed)
    {
        var position = 0;
        var value = ReadItem(data, ref position, 0);
        consumed = position;
        return value;
    }

    private static object? ReadItem(ReadOnlySpan<byte> data, ref int position, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new FormatException("CBOR is nested too deeply.");
        }

        if (position >= data.Length)
        {
            throw new FormatException("CBOR is truncated.");
        }

        var initial = data[position++];
        var major = initial >> 5;
        var info = initial & 0x1F;

        if (major == 7)
        {
            return info switch
            {
                20 => false,
                21 => true,
                22 or 23 => null,
                _ => throw new FormatException("Unsupported CBOR simple value.")
            };
        }

        var argument = ReadArgument(data, ref position, info);

        switch (major)
        {
            case 0:
                return argument <= long.MaxValue ? (long)argument : throw new FormatException("Integer too large.");
            case 1:
                return argument <= long.MaxValue ? -1L - (long)argument : throw new FormatException("Integer too large.");
            case 2:
                return ReadBytes(data, ref position, argument).ToArray();
            case 3:
                return Encoding.UTF8.GetString(ReadBytes(data, ref position, argument));
            case 4:
            {
                if (argument > MaxItems)
                {
                    throw new FormatException("CBOR array too long.");
                }

                var list = new List<object?>();
                for (ulong i = 0; i < argument; i++)
                {
                    list.Add(ReadItem(data, ref position, depth + 1));
                }

                return list;
            }
            case 5:
            {
                if (argument > MaxItems)
                {
                    throw new FormatException("CBOR map too large.");
                }

                var map = new Dictionary<object, object?>();
                for (ulong i = 0; i < argument; i++)
                {
                    var key = ReadItem(data, ref position, depth + 1);
                    if (key is not (long or string))
                    {
                        throw new FormatException("Unsupported CBOR map key.");
                    }

                    map[key] = ReadItem(data, ref position, depth + 1);
                }

                return map;
            }
            case 6:
                // A tag only labels the item that follows it.
                return ReadItem(data, ref position, depth + 1);
            default:
                throw new FormatException("Unsupported CBOR item.");
        }
    }

    private static ulong ReadArgument(ReadOnlySpan<byte> data, ref int position, int info)
    {
        if (info < 24)
        {
            return (ulong)info;
        }

        var size = info switch
        {
            24 => 1,
            25 => 2,
            26 => 4,
            27 => 8,
            _ => throw new FormatException("Indefinite CBOR lengths are not supported.")
        };

        if (position + size > data.Length)
        {
            throw new FormatException("CBOR is truncated.");
        }

        ulong value = 0;
        for (var i = 0; i < size; i++)
        {
            value = (value << 8) | data[position++];
        }

        return value;
    }

    private static ReadOnlySpan<byte> ReadBytes(ReadOnlySpan<byte> data, ref int position, ulong length)
    {
        if (length > (ulong)(data.Length - position))
        {
            throw new FormatException("CBOR is truncated.");
        }

        var slice = data.Slice(position, (int)length);
        position += (int)length;
        return slice;
    }
}

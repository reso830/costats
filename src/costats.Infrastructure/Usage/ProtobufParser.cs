namespace costats.Infrastructure.Usage;

public static class ProtobufParser
{
    public static List<(int FieldNumber, object Value)> Parse(byte[] data)
    {
        var fields = new List<(int, object)>();
        var position = 0;

        while (position < data.Length)
        {
            if (!TryReadVarint(data, ref position, out var key))
            {
                break;
            }

            var wireType = (int)(key & 0x7);
            var fieldNumber = (int)(key >> 3);

            switch (wireType)
            {
                case 0:
                    if (!TryReadVarint(data, ref position, out var varint))
                    {
                        return fields;
                    }
                    fields.Add((fieldNumber, varint));
                    break;
                case 1:
                    if (position + 8 > data.Length)
                    {
                        return fields;
                    }
                    fields.Add((fieldNumber, BitConverter.ToInt64(data, position)));
                    position += 8;
                    break;
                case 2:
                    if (!TryReadVarint(data, ref position, out var length) ||
                        length < 0 ||
                        length > int.MaxValue ||
                        position + (int)length > data.Length)
                    {
                        return fields;
                    }

                    var bytes = new byte[(int)length];
                    Array.Copy(data, position, bytes, 0, bytes.Length);
                    position += bytes.Length;
                    fields.Add((fieldNumber, bytes));
                    break;
                case 5:
                    if (position + 4 > data.Length)
                    {
                        return fields;
                    }
                    fields.Add((fieldNumber, BitConverter.ToInt32(data, position)));
                    position += 4;
                    break;
                default:
                    return fields;
            }
        }

        return fields;
    }

    private static bool TryReadVarint(byte[] data, ref int position, out long value)
    {
        value = 0;
        var shift = 0;

        while (position < data.Length)
        {
            var current = data[position++];
            value |= (long)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return true;
            }

            shift += 7;
            if (shift >= 64)
            {
                break;
            }
        }

        return false;
    }
}

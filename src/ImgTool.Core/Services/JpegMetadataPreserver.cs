using System.Buffers.Binary;

namespace ImgTool.Core.Services;

/// <summary>
/// Переносит EXIF/XMP/IPTC/ICC-сегменты из исходного JPEG в результат.
/// Ориентация в EXIF сбрасывается в TopLeft, так как пиксели уже повёрнуты.
/// </summary>
public static class JpegMetadataPreserver
{
    private static readonly byte[] ExifHeader = "Exif\0\0"u8.ToArray();
    private static readonly byte[] XmpHeader = "http://ns.adobe.com/xap/1.0/\0"u8.ToArray();
    private static readonly byte[] IccHeader = "ICC_PROFILE\0"u8.ToArray();
    private static readonly byte[] PhotoshopHeader = "Photoshop 3.0\0"u8.ToArray();

    private readonly record struct Segment(byte Marker, byte[] Payload);

    public static void Copy(string sourcePath, string outputPath)
    {
        if (!File.Exists(sourcePath) || !File.Exists(outputPath))
        {
            return;
        }

        if (!Path.GetExtension(sourcePath).Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetExtension(sourcePath).Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        byte[] source = File.ReadAllBytes(sourcePath);
        if (source.Length < 4 || source[0] != 0xFF || source[1] != 0xD8)
        {
            return;
        }

        List<Segment> segments = ReadMetadataSegments(source);
        if (segments.Count == 0)
        {
            return;
        }

        byte[] output = File.ReadAllBytes(outputPath);
        if (output.Length < 2 || output[0] != 0xFF || output[1] != 0xD8)
        {
            return;
        }

        string tempPath = outputPath + ".imgtool-meta";
        try
        {
            using (FileStream stream = File.Create(tempPath))
            {
                stream.WriteByte(0xFF);
                stream.WriteByte(0xD8);

                Span<byte> lengthBytes = stackalloc byte[2];
                foreach (Segment segment in segments)
                {
                    int length = segment.Payload.Length + 2;
                    if (length > ushort.MaxValue)
                    {
                        continue;
                    }

                    stream.WriteByte(0xFF);
                    stream.WriteByte(segment.Marker);
                    BinaryPrimitives.WriteUInt16BigEndian(lengthBytes, (ushort)length);
                    stream.Write(lengthBytes);
                    stream.Write(segment.Payload);
                }

                stream.Write(output, 2, output.Length - 2);
            }

            File.Move(tempPath, outputPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static List<Segment> ReadMetadataSegments(byte[] jpeg)
    {
        var segments = new List<Segment>();
        int position = 2;
        bool exifAdded = false;

        while (position + 4 <= jpeg.Length)
        {
            if (jpeg[position] != 0xFF)
            {
                break;
            }

            byte marker = jpeg[position + 1];
            if (marker == 0xD9 || marker == 0xDA)
            {
                break;
            }

            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7) || marker == 0xFF)
            {
                position += marker == 0xFF ? 1 : 2;
                continue;
            }

            int length = (jpeg[position + 2] << 8) | jpeg[position + 3];
            if (length < 2 || position + 2 + length > jpeg.Length)
            {
                break;
            }

            int payloadOffset = position + 4;
            int payloadLength = length - 2;
            ReadOnlySpan<byte> payload = jpeg.AsSpan(payloadOffset, payloadLength);

            if (marker == 0xE1)
            {
                if (payload.StartsWith(ExifHeader))
                {
                    if (!exifAdded)
                    {
                        byte[] copy = payload.ToArray();
                        ExifOrientation.SetTopLeft(copy);
                        segments.Add(new Segment(0xE1, copy));
                        exifAdded = true;
                    }
                }
                else if (payload.StartsWith(XmpHeader))
                {
                    segments.Add(new Segment(0xE1, payload.ToArray()));
                }
            }
            else if (marker == 0xE2 && payload.StartsWith(IccHeader))
            {
                segments.Add(new Segment(0xE2, payload.ToArray()));
            }
            else if (marker == 0xED && payload.StartsWith(PhotoshopHeader))
            {
                segments.Add(new Segment(0xED, payload.ToArray()));
            }

            position += 2 + length;
        }

        return segments;
    }

    /// <summary>Патчит тег Orientation (0x0112) в EXIF-сегменте на значение 1.</summary>
    public static class ExifOrientation
    {
        private const int TiffHeaderOffset = 6;
        private const ushort OrientationTag = 0x0112;

        public static void SetTopLeft(byte[] exifPayload)
        {
            if (exifPayload.Length < TiffHeaderOffset + 8)
            {
                return;
            }

            int tiff = TiffHeaderOffset;
            bool littleEndian;
            if (exifPayload[tiff] == 'I' && exifPayload[tiff + 1] == 'I')
            {
                littleEndian = true;
            }
            else if (exifPayload[tiff] == 'M' && exifPayload[tiff + 1] == 'M')
            {
                littleEndian = false;
            }
            else
            {
                return;
            }

            uint ifdOffset = ReadUInt32(exifPayload, tiff + 4, littleEndian);
            long ifdPosition = tiff + ifdOffset;
            if (ifdPosition < 0 || ifdPosition + 2 > exifPayload.Length)
            {
                return;
            }

            int ifd = (int)ifdPosition;
            int entryCount = ReadUInt16(exifPayload, ifd, littleEndian);
            for (int i = 0; i < entryCount; i++)
            {
                int entry = ifd + 2 + (i * 12);
                if (entry + 12 > exifPayload.Length)
                {
                    return;
                }

                ushort tag = ReadUInt16(exifPayload, entry, littleEndian);
                if (tag != OrientationTag)
                {
                    continue;
                }

                ushort type = ReadUInt16(exifPayload, entry + 2, littleEndian);
                uint count = ReadUInt32(exifPayload, entry + 4, littleEndian);
                if (type != 3 || count == 0)
                {
                    return;
                }

                int valuePosition;
                if (count == 1)
                {
                    valuePosition = entry + 8;
                }
                else
                {
                    uint valueOffset = ReadUInt32(exifPayload, entry + 8, littleEndian);
                    long absolute = tiff + valueOffset;
                    if (absolute < 0 || absolute + 2 > exifPayload.Length)
                    {
                        return;
                    }

                    valuePosition = (int)absolute;
                }

                WriteUInt16(exifPayload, valuePosition, 1, littleEndian);
                return;
            }
        }

        private static ushort ReadUInt16(byte[] buffer, int offset, bool littleEndian) =>
            littleEndian
                ? BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset, 2))
                : BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(offset, 2));

        private static uint ReadUInt32(byte[] buffer, int offset, bool littleEndian) =>
            littleEndian
                ? BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, 4))
                : BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, 4));

        private static void WriteUInt16(byte[] buffer, int offset, ushort value, bool littleEndian)
        {
            if (littleEndian)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset, 2), value);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), value);
            }
        }
    }
}

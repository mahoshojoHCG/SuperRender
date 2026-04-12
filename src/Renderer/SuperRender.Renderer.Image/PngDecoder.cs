using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SuperRender.Renderer.Image;

/// <summary>
/// Pure C# PNG image decoder. Supports color types 0 (grayscale), 2 (RGB), 3 (indexed),
/// 4 (grayscale+alpha), and 6 (RGBA) at bit depths 1/2/4/8/16. Non-interlaced only.
/// </summary>
public static class PngDecoder
{
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    public static bool IsPng(ReadOnlySpan<byte> data)
        => data.Length >= 8 && data[..8].SequenceEqual(PngSignature);

    public static ImageData? Decode(ReadOnlySpan<byte> data)
    {
        if (!IsPng(data))
            return null;

        // Parse chunks
        int offset = 8;
        int width = 0, height = 0;
        byte bitDepth = 0, colorType = 0, interlace = 0;
        bool hasIhdr = false;
        byte[]? palette = null;
        byte[]? transparency = null;
        using var idatStream = new MemoryStream();
        bool hasIend = false;

        while (offset + 8 <= data.Length)
        {
            if (offset + 4 > data.Length) return null;
            uint chunkLength = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            offset += 4;

            if (offset + 4 > data.Length) return null;
            string chunkType = Encoding.ASCII.GetString(data.Slice(offset, 4));
            offset += 4;

            if (chunkLength > int.MaxValue) return null;
            int len = (int)chunkLength;

            if (offset + len + 4 > data.Length) return null; // +4 for CRC
            ReadOnlySpan<byte> chunkData = data.Slice(offset, len);

            switch (chunkType)
            {
                case "IHDR":
                    if (len < 13) return null;
                    width = (int)BinaryPrimitives.ReadUInt32BigEndian(chunkData);
                    height = (int)BinaryPrimitives.ReadUInt32BigEndian(chunkData[4..]);
                    bitDepth = chunkData[8];
                    colorType = chunkData[9];
                    // chunkData[10] = compression method (must be 0)
                    // chunkData[11] = filter method (must be 0)
                    interlace = chunkData[12];
                    hasIhdr = true;
                    break;

                case "PLTE":
                    palette = chunkData.ToArray();
                    break;

                case "tRNS":
                    transparency = chunkData.ToArray();
                    break;

                case "IDAT":
                    idatStream.Write(chunkData);
                    break;

                case "IEND":
                    hasIend = true;
                    break;
            }

            offset += len + 4; // skip data + CRC

            if (hasIend) break;
        }

        if (!hasIhdr || !hasIend) return null;
        if (width <= 0 || height <= 0) return null;
        if (interlace != 0) return null; // Adam7 not supported

        // Validate color type / bit depth combinations
        if (!IsValidColorTypeBitDepth(colorType, bitDepth)) return null;

        // Color type 3 requires a palette
        if (colorType == 3 && palette == null) return null;

        // Calculate bytes per pixel and scanline stride (needed for expected decompressed size)
        int channels = GetChannelCount(colorType);
        int bitsPerPixel = channels * bitDepth;
        int bytesPerPixel = Math.Max(1, bitsPerPixel / 8);

        // For sub-byte bit depths, calculate scanline width in bytes
        int scanlineBits = width * bitsPerPixel;
        int scanlineBytes = (scanlineBits + 7) / 8;

        // Expected decompressed size: height * (1 filter byte + scanlineBytes)
        int expectedSize = height * (1 + scanlineBytes);

        // Decompress IDAT data (skip 2-byte zlib header)
        byte[] compressedData = idatStream.ToArray();
        if (compressedData.Length < 2) return null;

        byte[] decompressedBuf = ArrayPool<byte>.Shared.Rent(expectedSize);
        try
        {
            int decompressedLen;
            try
            {
                decompressedLen = DecompressZlib(compressedData, decompressedBuf);
            }
            catch
            {
                return null;
            }

            if (decompressedLen < expectedSize) return null;

            // Reconstruct filtered scanlines
            byte[] reconstructed = ArrayPool<byte>.Shared.Rent(height * scanlineBytes);
            try
            {
                reconstructed.AsSpan(0, height * scanlineBytes).Clear();
                if (!ReconstructFilters(decompressedBuf.AsSpan(), reconstructed.AsSpan(0, height * scanlineBytes), height, scanlineBytes, bytesPerPixel))
                    return null;

                // Convert to RGBA
                byte[] pixels = ConvertToRgba(reconstructed, width, height, colorType, bitDepth,
                    scanlineBytes, palette, transparency);

                return new ImageData { Width = width, Height = height, Pixels = pixels };
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(reconstructed);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(decompressedBuf);
        }
    }

    private static bool IsValidColorTypeBitDepth(byte colorType, byte bitDepth) => colorType switch
    {
        0 => bitDepth is 1 or 2 or 4 or 8 or 16,           // Grayscale
        2 => bitDepth is 8 or 16,                            // RGB
        3 => bitDepth is 1 or 2 or 4 or 8,                  // Indexed
        4 => bitDepth is 8 or 16,                            // Grayscale + Alpha
        6 => bitDepth is 8 or 16,                            // RGBA
        _ => false
    };

    private static int GetChannelCount(byte colorType) => colorType switch
    {
        0 => 1,  // Grayscale
        2 => 3,  // RGB
        3 => 1,  // Indexed (1 index byte per pixel)
        4 => 2,  // Grayscale + Alpha
        6 => 4,  // RGBA
        _ => 0
    };

    private static int DecompressZlib(byte[] zlibData, byte[] output)
    {
        // Skip 2-byte zlib header (CMF + FLG)
        using var compressed = new MemoryStream(zlibData, 2, zlibData.Length - 2);
        using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
        int totalRead = 0;
        int read;
        while (totalRead < output.Length &&
               (read = deflate.Read(output, totalRead, output.Length - totalRead)) > 0)
        {
            totalRead += read;
        }
        return totalRead;
    }

    private static bool ReconstructFilters(ReadOnlySpan<byte> decompressed, Span<byte> reconstructed,
        int height, int scanlineBytes, int bytesPerPixel)
    {
        for (int y = 0; y < height; y++)
        {
            int srcOffset = y * (1 + scanlineBytes);
            byte filterType = decompressed[srcOffset];
            int srcRow = srcOffset + 1;
            int dstRow = y * scanlineBytes;
            int prevRow = (y - 1) * scanlineBytes;

            if (filterType > 4) return false;

            for (int x = 0; x < scanlineBytes; x++)
            {
                byte raw = decompressed[srcRow + x];
                byte a = (x >= bytesPerPixel) ? reconstructed[dstRow + x - bytesPerPixel] : (byte)0;
                byte b = (y > 0) ? reconstructed[prevRow + x] : (byte)0;
                byte c = (x >= bytesPerPixel && y > 0) ? reconstructed[prevRow + x - bytesPerPixel] : (byte)0;

                byte result = filterType switch
                {
                    0 => raw,
                    1 => (byte)(raw + a),
                    2 => (byte)(raw + b),
                    3 => (byte)(raw + (byte)((a + b) / 2)),
                    4 => (byte)(raw + PaethPredictor(a, b, c)),
                    _ => raw // unreachable due to guard above
                };

                reconstructed[dstRow + x] = result;
            }
        }

        return true;
    }

    private static byte PaethPredictor(byte a, byte b, byte c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static byte[] ConvertToRgba(ReadOnlySpan<byte> reconstructed, int width, int height,
        byte colorType, byte bitDepth, int scanlineBytes, byte[]? palette, byte[]? transparency)
    {
        var pixels = new byte[width * height * 4];
        Span<byte> dest = pixels;

        for (int y = 0; y < height; y++)
        {
            int srcRow = y * scanlineBytes;
            int dstRow = y * width * 4;

            for (int x = 0; x < width; x++)
            {
                int dstIdx = dstRow + x * 4;

                switch (colorType)
                {
                    case 0: // Grayscale
                        {
                            byte gray = ReadSample(reconstructed, srcRow, x, bitDepth);
                            dest[dstIdx] = gray;
                            dest[dstIdx + 1] = gray;
                            dest[dstIdx + 2] = gray;
                            dest[dstIdx + 3] = 255;

                            // tRNS for grayscale: 2 bytes (16-bit gray value to match)
                            if (transparency != null && transparency.Length >= 2)
                            {
                                int trnGray = bitDepth == 16
                                    ? transparency[0]
                                    : BinaryPrimitives.ReadUInt16BigEndian(transparency) >> (16 - bitDepth) & ((1 << bitDepth) - 1);
                                // For simplicity, compare the final 8-bit value
                                int trnGray8 = bitDepth == 16 ? transparency[0] : ScaleTo8Bit(trnGray, bitDepth);
                                if (gray == trnGray8) dest[dstIdx + 3] = 0;
                            }
                        }
                        break;

                    case 2: // RGB
                        if (bitDepth == 16)
                        {
                            int si = srcRow + x * 6;
                            dest[dstIdx] = reconstructed[si];         // R high byte
                            dest[dstIdx + 1] = reconstructed[si + 2]; // G high byte
                            dest[dstIdx + 2] = reconstructed[si + 4]; // B high byte
                        }
                        else
                        {
                            int si = srcRow + x * 3;
                            dest[dstIdx] = reconstructed[si];
                            dest[dstIdx + 1] = reconstructed[si + 1];
                            dest[dstIdx + 2] = reconstructed[si + 2];
                        }
                        dest[dstIdx + 3] = 255;

                        // tRNS for RGB: 6 bytes (R, G, B each as 16-bit)
                        if (transparency != null && transparency.Length >= 6)
                        {
                            byte tr = (byte)(BinaryPrimitives.ReadUInt16BigEndian(transparency) >> 8);
                            byte tg = (byte)(BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(2)) >> 8);
                            byte tb = (byte)(BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(4)) >> 8);
                            if (bitDepth == 8)
                            {
                                tr = (byte)BinaryPrimitives.ReadUInt16BigEndian(transparency);
                                tg = (byte)BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(2));
                                tb = (byte)BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(4));
                            }
                            if (dest[dstIdx] == tr && dest[dstIdx + 1] == tg && dest[dstIdx + 2] == tb)
                                dest[dstIdx + 3] = 0;
                        }
                        break;

                    case 3: // Indexed
                        {
                            int index = ReadSample(reconstructed, srcRow, x, bitDepth);
                            if (palette != null && index * 3 + 2 < palette.Length)
                            {
                                dest[dstIdx] = palette[index * 3];
                                dest[dstIdx + 1] = palette[index * 3 + 1];
                                dest[dstIdx + 2] = palette[index * 3 + 2];
                            }
                            dest[dstIdx + 3] = (transparency != null && index < transparency.Length)
                                ? transparency[index]
                                : (byte)255;
                        }
                        break;

                    case 4: // Grayscale + Alpha
                        if (bitDepth == 16)
                        {
                            int si = srcRow + x * 4;
                            byte gray = reconstructed[si]; // high byte
                            byte alpha = reconstructed[si + 2]; // high byte
                            dest[dstIdx] = gray;
                            dest[dstIdx + 1] = gray;
                            dest[dstIdx + 2] = gray;
                            dest[dstIdx + 3] = alpha;
                        }
                        else
                        {
                            int si = srcRow + x * 2;
                            byte gray = reconstructed[si];
                            byte alpha = reconstructed[si + 1];
                            dest[dstIdx] = gray;
                            dest[dstIdx + 1] = gray;
                            dest[dstIdx + 2] = gray;
                            dest[dstIdx + 3] = alpha;
                        }
                        break;

                    case 6: // RGBA
                        if (bitDepth == 16)
                        {
                            int si = srcRow + x * 8;
                            dest[dstIdx] = reconstructed[si];         // R high byte
                            dest[dstIdx + 1] = reconstructed[si + 2]; // G high byte
                            dest[dstIdx + 2] = reconstructed[si + 4]; // B high byte
                            dest[dstIdx + 3] = reconstructed[si + 6]; // A high byte
                        }
                        else
                        {
                            int si = srcRow + x * 4;
                            dest[dstIdx] = reconstructed[si];
                            dest[dstIdx + 1] = reconstructed[si + 1];
                            dest[dstIdx + 2] = reconstructed[si + 2];
                            dest[dstIdx + 3] = reconstructed[si + 3];
                        }
                        break;
                }
            }
        }

        return pixels;
    }

    /// <summary>
    /// Reads a single sample value from packed scanline data at the given pixel index,
    /// handling sub-byte bit depths (1, 2, 4) and scaling to 8-bit.
    /// </summary>
    private static byte ReadSample(ReadOnlySpan<byte> data, int rowOffset, int pixelIndex, byte bitDepth)
    {
        if (bitDepth >= 8)
        {
            int bytesPerSample = bitDepth / 8;
            return data[rowOffset + pixelIndex * bytesPerSample]; // high byte for 16-bit
        }

        // Sub-byte bit depths: pack multiple samples per byte, MSB first
        int samplesPerByte = 8 / bitDepth;
        int byteIndex = rowOffset + pixelIndex / samplesPerByte;
        int bitOffset = (samplesPerByte - 1 - (pixelIndex % samplesPerByte)) * bitDepth;
        int mask = (1 << bitDepth) - 1;
        int raw = (data[byteIndex] >> bitOffset) & mask;

        return ScaleTo8Bit(raw, bitDepth);
    }

    /// <summary>
    /// Scales a sample from the given bit depth to 8-bit by replicating bits.
    /// </summary>
    private static byte ScaleTo8Bit(int value, byte bitDepth) => bitDepth switch
    {
        1 => (byte)(value * 255),
        2 => (byte)(value * 85),     // 0→0, 1→85, 2→170, 3→255
        4 => (byte)(value * 17),     // 0→0, 1→17, ..., 15→255
        8 => (byte)value,
        16 => (byte)value,           // Already took high byte
        _ => (byte)value
    };
}

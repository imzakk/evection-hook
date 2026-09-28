namespace Evection.Detector;

/// <summary>Minimal PE header reader — just enough to learn a Windows executable's CPU architecture.</summary>
internal static class PeReader
{
    public static CpuArchitecture ReadArchitecture(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D) // "MZ"
                return CpuArchitecture.Unknown;

            stream.Position = 0x3C;
            var peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset + 6 > stream.Length)
                return CpuArchitecture.Unknown;

            stream.Position = peOffset;
            if (reader.ReadUInt32() != 0x00004550) // "PE\0\0"
                return CpuArchitecture.Unknown;

            return reader.ReadUInt16() switch
            {
                0x014C => CpuArchitecture.X86,
                0x8664 => CpuArchitecture.X64,
                0xAA64 => CpuArchitecture.Arm64,
                _ => CpuArchitecture.Unknown,
            };
        }
        catch (IOException)
        {
            return CpuArchitecture.Unknown;
        }
    }
}

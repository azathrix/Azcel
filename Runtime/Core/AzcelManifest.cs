using System;
using System.Collections.Generic;
using System.IO;

namespace Azcel
{
    public enum AzcelManifestEntryType : byte
    {
        Table = 0,
        Global = 1
    }

    public sealed class AzcelManifestEntry
    {
        public AzcelManifestEntryType EntryType;
        public string ConfigName;
        public string TypeName;
    }

    public sealed class AzcelManifest
    {
        public const string ManifestName = "AzcelManifest";
        public const int CurrentVersion = 1;

        public string FormatId;
        public List<AzcelManifestEntry> Entries { get; } = new();

        public static byte[] Serialize(AzcelManifest manifest)
        {
            if (manifest == null)
                return Array.Empty<byte>();

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write('A');
            writer.Write('Z');
            writer.Write('C');
            writer.Write('L');
            writer.Write(CurrentVersion);
            writer.Write(manifest.FormatId ?? string.Empty);
            writer.Write(manifest.Entries.Count);

            foreach (var entry in manifest.Entries)
            {
                writer.Write((byte)entry.EntryType);
                writer.Write(entry.ConfigName ?? string.Empty);
                writer.Write(entry.TypeName ?? string.Empty);
            }

            return stream.ToArray();
        }

        public static AzcelManifest Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            var header = new[] { reader.ReadChar(), reader.ReadChar(), reader.ReadChar(), reader.ReadChar() };
            if (header[0] != 'A' || header[1] != 'Z' || header[2] != 'C' || header[3] != 'L')
                return null;

            var version = reader.ReadInt32();
            if (version != CurrentVersion)
                return null;

            var manifest = new AzcelManifest
            {
                FormatId = reader.ReadString()
            };

            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var entry = new AzcelManifestEntry
                {
                    EntryType = (AzcelManifestEntryType)reader.ReadByte(),
                    ConfigName = reader.ReadString(),
                    TypeName = reader.ReadString()
                };
                manifest.Entries.Add(entry);
            }

            return manifest;
        }
    }
}

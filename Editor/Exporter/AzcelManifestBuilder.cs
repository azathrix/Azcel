using System;

namespace Azcel.Editor
{
    public static class AzcelManifestBuilder
    {
        public static AzcelManifest Build(ConvertContext context, string codeNamespace, string formatId)
        {
            var manifest = new AzcelManifest
            {
                FormatId = formatId
            };

            var ns = string.IsNullOrEmpty(codeNamespace) ? "Game.Tables" : codeNamespace.Trim();

            foreach (var table in context.Tables)
            {
                manifest.Entries.Add(new AzcelManifestEntry
                {
                    EntryType = AzcelManifestEntryType.Table,
                    ConfigName = table.Name,
                    TypeName = $"{ns}.{table.Name}Table"
                });
            }

            foreach (var global in context.Globals)
            {
                var configName = $"GlobalConfig{global.Name}";
                manifest.Entries.Add(new AzcelManifestEntry
                {
                    EntryType = AzcelManifestEntryType.Global,
                    ConfigName = configName,
                    TypeName = $"{ns}.{configName}"
                });
            }

            return manifest;
        }
    }
}

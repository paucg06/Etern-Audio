using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
#if !NETCOREAPP && !NET8_0_OR_GREATER
using System.Reflection;

[assembly: AssemblyTitle("Etern Audio")]
[assembly: AssemblyDescription("Etern Audio - Gestor y Organizador Inteligente de Efectos de Sonido")]
[assembly: AssemblyCompany("Etern Studio")]
[assembly: AssemblyProduct("Etern Audio")]
[assembly: AssemblyCopyright("Copyright © Etern Studio 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
#endif

namespace EternAudio
{
    [DataContract]
    public class SfxFile
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string FilePath { get; set; }
        [DataMember] public string FileName { get; set; }
        [DataMember] public string DisplayName { get; set; }
        [DataMember] public string OriginalRawName { get; set; }
        [DataMember] public string[] Tags { get; set; }
        [DataMember] public string Category { get; set; }
        [DataMember] public string SubCategory { get; set; }
        [DataMember] public string SuggestedFolder { get; set; }
        [DataMember] public double ConfidenceScore { get; set; } // 0.0 to 1.0 (e.g. 0.85 = 85%)
        [DataMember] public long FileSizeBytes { get; set; }
        [DataMember] public double DurationSeconds { get; set; }
        [DataMember] public bool IsShortSfx { get; set; } // true if < 30s, false if >= 30s
        [DataMember] public bool NeedsReview { get; set; } // true if organization was uncertain (< 80%)
        [DataMember] public double MatchScore { get; set; } // 1.0 to 10.0 search relevance score
        [DataMember] public string LibraryId { get; set; }
        [DataMember] public bool IsFavorite { get; set; }
        [DataMember] public int PlayCount { get; set; }
        [DataMember] public long DateAddedTicks { get; set; }

        public SfxFile()
        {
            Id = Guid.NewGuid().ToString();
            Tags = new string[0];
            Category = "General";
            SubCategory = "General";
            SuggestedFolder = "";
            ConfidenceScore = 1.0;
            NeedsReview = false;
            MatchScore = 10.0;
            DateAddedTicks = DateTime.Now.Ticks;
        }
    }

    [DataContract]
    public class SfxLibrary
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string RootPath { get; set; }
        [DataMember] public long LastScannedTicks { get; set; }
        [DataMember] public int FileCount { get; set; }

        public SfxLibrary()
        {
            Id = Guid.NewGuid().ToString();
        }
    }

    [DataContract]
    public class SfxDatabase
    {
        [DataMember] public List<SfxLibrary> Libraries { get; set; }
        [DataMember] public List<SfxFile> Files { get; set; }
        [DataMember] public string PreferredLanguage { get; set; } // "es" or "en"

        public SfxDatabase()
        {
            Libraries = new List<SfxLibrary>();
            Files = new List<SfxFile>();
            PreferredLanguage = "es";
        }
    }

    public class FolderNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public int FileCount { get; set; }
        public List<FolderNode> Children { get; set; }

        public FolderNode()
        {
            Children = new List<FolderNode>();
        }
    }

    public static class Storage
    {
        private static readonly string DbDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EternAudio");

        private static readonly string DbPath = Path.Combine(DbDir, "eternaudio_db.json");

        public static SfxDatabase Load()
        {
            try
            {
                if (!Directory.Exists(DbDir)) Directory.CreateDirectory(DbDir);
                if (!File.Exists(DbPath)) return new SfxDatabase();

                using (var fs = new FileStream(DbPath, FileMode.Open, FileAccess.Read))
                {
                    var s = new DataContractJsonSerializer(typeof(SfxDatabase));
                    return (SfxDatabase)s.ReadObject(fs) ?? new SfxDatabase();
                }
            }
            catch { return new SfxDatabase(); }
        }

        public static void Save(SfxDatabase db)
        {
            try
            {
                if (!Directory.Exists(DbDir)) Directory.CreateDirectory(DbDir);
                using (var fs = new FileStream(DbPath, FileMode.Create, FileAccess.Write))
                {
                    var s = new DataContractJsonSerializer(typeof(SfxDatabase));
                    s.WriteObject(fs, db);
                }
            }
            catch { }
        }
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace EternAudio
{
    public static class FileOrganizer
    {
        // ─── Direct Spanish Dictionary Mappings for Common File Names ─────────────────
        private static readonly Dictionary<string, string> KnownNameTranslations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "aaah", "Grito_De_Ahhh" },
            { "ayuda_2", "Grito_De_Ayuda" },
            { "Boton Respuesta Incorrecta", "Boton_Respuesta_Incorrecta" },
            { "breaking_bone_hueso", "Romperse_El_Hueso" },
            { "cartoon-falling", "Caida_Dibujos_Animados" },
            { "chainsaw-noise-cutting-wood-lumberjack-285941", "Motosierra_Cortando_Madera" },
            { "concha-de-tu-madre-kirbi", "Insulto_Meme_Kirby" },
            { "Doraemon", "Cancion_Doraemon" },
            { "Drama Goku Meme", "Drama_Meme_Goku" },
            { "earthquake-end", "Fin_Del_Terremoto" },
            { "Esnifar_Sniff", "Esnifar_Nariz" },
            { "falling-body", "Caida_De_Cuerpo" },
            { "Frase Estaba Paralizado Con Mucho Miedo", "Frase_Miedo_Paralizado" },
            { "Golpe Yunke", "Golpe_Yunque_Metal" },
            { "ground-impact-352053", "Impacto_Contra_El_Suelo" },
            { "Hit", "Golpe_Seco" },
            { "James Bond", "Tema_James_Bond" },
            { "kirby-falling", "Caida_Meme_Kirby" },
            { "Meme Japones ChingChengHanji", "Meme_Japones_Ching_Cheng" },
            { "monkey sound", "Sonido_De_Mono" },
            { "hora_hora_hora", "Grito_Anime_Hora_Hora" },
            { "parpadear", "Efecto_Parpadeo_Ojos" },
            { "Notification", "Notificacion_Simple" },
            { "WhatsApp notification", "Notificacion_Whatsapp" },
            { "IPHONE TIPEAR EFECTO DE SONIDO _ TECLADO _ PERSONA ESCRIBIENDO IPHONE SOUND EFFECT - SIN COPYRIGHT", "Teclado_Iphone_Escribiendo" }
        };

        // Word translations (EN -> ES)
        private static readonly Dictionary<string, string> WordTranslations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "breaking", "Ruptura" }, { "bone", "Hueso" }, { "falling", "Caida" }, { "fall", "Caida" },
            { "chainsaw", "Motosierra" }, { "wood", "Madera" }, { "cutting", "Cortando" }, { "impact", "Impacto" },
            { "ground", "Suelo" }, { "earthquake", "Terremoto" }, { "end", "Fin" }, { "hit", "Golpe" },
            { "body", "Cuerpo" }, { "sniff", "Esnifar" }, { "sound", "Sonido" }, { "effect", "Efecto" },
            { "noise", "Ruido" }, { "button", "Boton" }, { "wrong", "Incorrecto" }, { "correct", "Correcto" },
            { "game", "Juego" }, { "fail", "Fallo" }, { "success", "Exito" }, { "bell", "Campana" },
            { "door", "Puerta" }, { "glass", "Vidrio" }, { "fire", "Fuego" }, { "water", "Agua" },
            { "wind", "Viento" }, { "thunder", "Trueno" }, { "scream", "Grito" }, { "voice", "Voz" },
            { "laugh", "Risa" }, { "magic", "Magia" }, { "sword", "Espada" }, { "gun", "Disparo" },
            { "car", "Coche" }, { "train", "Tren" }, { "explosion", "Explosion" }, { "boom", "Bum" }
        };

        /// <summary>
        /// Cleans a filename and formats it cleanly to Title_Case_With_Underscores in Spanish.
        /// </summary>
        public static string FormatCleanSpanishFileName(string rawFileName)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(rawFileName);

            // Check known exact mapping
            foreach (var kvp in KnownNameTranslations)
            {
                if (nameWithoutExt.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kvp.Value;
            }

            // Clean YouTube hashes [WXOXRR4vmwo], filler words
            string cleaned = Regex.Replace(nameWithoutExt, @"\[[A-Za-z0-9_-]{8,}\]", "");
            cleaned = Regex.Replace(cleaned, @"(?i)(EFECTO DE SONIDO|SOUND EFFECT|SIN COPYRIGHT|NO COPYRIGHT|MP3|WAV)", "");
            cleaned = Regex.Replace(cleaned, @"^\d+[\s\-_]*", ""); // remove leading track numbers
            cleaned = Regex.Replace(cleaned, @"[\-_]+", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            if (string.IsNullOrEmpty(cleaned))
                cleaned = nameWithoutExt;

            // Tokenize and translate words to Spanish
            var words = cleaned.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanWords = new List<string>();

            foreach (var w in words)
            {
                if (Regex.IsMatch(w, @"^\d+$")) continue; // skip raw digits
                string normalized = TagEngine.NormalizeText(w);

                if (WordTranslations.ContainsKey(normalized))
                {
                    cleanWords.Add(WordTranslations[normalized]);
                }
                else
                {
                    // Capitalize word
                    string cap = char.ToUpper(w[0]) + (w.Length > 1 ? w.Substring(1).ToLower() : "");
                    cleanWords.Add(cap);
                }
            }

            if (cleanWords.Count == 0)
                cleanWords.Add("Efecto_Sonido");

            return string.Join("_", cleanWords);
        }

        /// <summary>
        /// Estimates or reads audio file duration in seconds.
        /// </summary>
        public static double GetAudioDurationSeconds(string filePath)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".wav")
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        fs.Seek(24, SeekOrigin.Begin);
                        int sampleRate = br.ReadInt32();
                        fs.Seek(34, SeekOrigin.Begin);
                        short bitsPerSample = br.ReadInt16();
                        fs.Seek(40, SeekOrigin.Begin);
                        int dataSize = br.ReadInt32();
                        fs.Seek(22, SeekOrigin.Begin);
                        short channels = br.ReadInt16();
                        if (sampleRate > 0 && bitsPerSample > 0 && channels > 0)
                            return (double)dataSize / (sampleRate * channels * (bitsPerSample / 8.0));
                    }
                }
            }
            catch { }

            // Estimate from file size (average ~128kbps = 16000 bytes/sec for mp3)
            try
            {
                long size = new FileInfo(filePath).Length;
                return Math.Max(1.0, size / 16000.0);
            }
            catch
            {
                return 5.0;
            }
        }

        /// <summary>
        /// Physical auto-organizer: scans a root library directory, renames unorganized files cleanly,
        /// and moves loose files into proper Fbx or Musica subfolders on disk!
        /// </summary>
        public static int PerformAutoOrganization(string rootDirectoryPath)
        {
            if (!Directory.Exists(rootDirectoryPath)) return 0;
            int organizedCount = 0;

            string fbxDir = Path.Combine(rootDirectoryPath, "Fbx");
            string musicDir = Path.Combine(rootDirectoryPath, "Musica");

            if (!Directory.Exists(fbxDir)) Directory.CreateDirectory(fbxDir);
            if (!Directory.Exists(musicDir)) Directory.CreateDirectory(musicDir);

            // Default subcategories
            string memesDir = Path.Combine(fbxDir, "Cartoon-Animados");
            string generalSfxDir = Path.Combine(fbxDir, "Efectos Frecuentes");
            string musicGeneralDir = Path.Combine(musicDir, "Productividad");

            if (!Directory.Exists(memesDir)) Directory.CreateDirectory(memesDir);
            if (!Directory.Exists(generalSfxDir)) Directory.CreateDirectory(generalSfxDir);
            if (!Directory.Exists(musicGeneralDir)) Directory.CreateDirectory(musicGeneralDir);

            // Find all loose files in the root of the library
            foreach (var file in Directory.GetFiles(rootDirectoryPath))
            {
                if (!TagEngine.IsAudioFile(file)) continue;

                try
                {
                    double duration = GetAudioDurationSeconds(file);
                    string ext = Path.GetExtension(file);
                    string cleanName = FormatCleanSpanishFileName(file) + ext;

                    string targetFolder;
                    if (duration >= 30.0)
                    {
                        targetFolder = musicGeneralDir;
                    }
                    else
                    {
                        var autoTagged = TagEngine.AutoTag(file);
                        if (autoTagged.Category == "Comedia" || autoTagged.Category == "Voz")
                            targetFolder = memesDir;
                        else
                            targetFolder = generalSfxDir;
                    }

                    string targetFilePath = Path.Combine(targetFolder, cleanName);

                    // Ensure target filename is unique
                    int counter = 1;
                    string nameNoExt = Path.GetFileNameWithoutExtension(cleanName);
                    while (File.Exists(targetFilePath))
                    {
                        targetFilePath = Path.Combine(targetFolder, nameNoExt + "_" + counter.ToString() + ext);
                        counter++;
                    }

                    File.Move(file, targetFilePath);
                    organizedCount++;
                }
                catch { }
            }

            return organizedCount;
        }

        /// <summary>
        /// Builds a directory tree structure for the sidebar tree view.
        /// </summary>
        public static FolderNode BuildDirectoryTree(string rootPath)
        {
            if (!Directory.Exists(rootPath)) return null;

            var rootNode = new FolderNode
            {
                Name = Path.GetFileName(rootPath),
                FullPath = rootPath,
                IsDirectory = true
            };

            PopulateNode(rootNode);
            return rootNode;
        }

        private static void PopulateNode(FolderNode parentNode)
        {
            try
            {
                int fileCountInDir = 0;
                foreach (var f in Directory.GetFiles(parentNode.FullPath))
                    if (TagEngine.IsAudioFile(f)) fileCountInDir++;

                parentNode.FileCount = fileCountInDir;

                foreach (var dir in Directory.GetDirectories(parentNode.FullPath))
                {
                    var childNode = new FolderNode
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        IsDirectory = true
                    };
                    PopulateNode(childNode);
                    parentNode.Children.Add(childNode);
                    parentNode.FileCount += childNode.FileCount;
                }
            }
            catch { }
        }
    }
}

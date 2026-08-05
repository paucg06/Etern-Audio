using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace EternAudio
{
    public static class FileOrganizer
    {
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
            { "IPHONE TIPEAR EFECTO DE SONIDO _ TECLADO _ PERSONA ESCRIBIENDO IPHONE SOUND EFFECT - SIN COPYRIGHT", "Teclado_Iphone_Escribiendo" },
            { "mala noticias mi gente", "Mala_Noticia_Mi_Gente" },
            { "malanoticias migente", "Mala_Noticia_Mi_Gente" },
            { "malanoticiasmigente", "Mala_Noticia_Mi_Gente" },
            { "Puñetazo Dani", "Puñetazo_Dani" },
            { "Pollo de goma largo", "Pollo_De_Goma_Largo" },
            { "Agua boil", "Sonido_Agua_Hirviendo" }
        };

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

        public static string FormatCleanSpanishFileName(string rawFileName)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(rawFileName);

            foreach (var kvp in KnownNameTranslations)
            {
                if (nameWithoutExt.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kvp.Value;
            }

            string cleaned = nameWithoutExt;
            cleaned = Regex.Replace(cleaned, @"(?i)malanoticias", "Mala Noticias ");
            cleaned = Regex.Replace(cleaned, @"(?i)migente", " Mi Gente");
            cleaned = Regex.Replace(cleaned, @"(?i)efectodesonido", " Efecto Sonido");

            cleaned = Regex.Replace(cleaned, @"\[[A-Za-z0-9_-]{8,}\]", "");
            cleaned = Regex.Replace(cleaned, @"(?i)(EFECTO DE SONIDO|SOUND EFFECT|SIN COPYRIGHT|NO COPYRIGHT|MP3|WAV)", "");
            cleaned = Regex.Replace(cleaned, @"^\d+[\s\-_]*", "");
            cleaned = Regex.Replace(cleaned, @"[\-_]+", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            if (string.IsNullOrEmpty(cleaned))
                cleaned = nameWithoutExt;

            var words = cleaned.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanWords = new List<string>();

            foreach (var w in words)
            {
                if (Regex.IsMatch(w, @"^\d+$")) continue;
                string normalized = TagEngine.NormalizeText(w);

                if (WordTranslations.ContainsKey(normalized))
                {
                    cleanWords.Add(WordTranslations[normalized]);
                }
                else
                {
                    string cap = char.ToUpper(w[0]) + (w.Length > 1 ? w.Substring(1).ToLower() : "");
                    cleanWords.Add(cap);
                }
            }

            if (cleanWords.Count == 0)
                cleanWords.Add("Efecto_Sonido");

            return string.Join("_", cleanWords);
        }

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

        public static int PerformAutoOrganization(string rootDirectoryPath, Action<int, int, string> onProgress = null)
        {
            if (!Directory.Exists(rootDirectoryPath)) return 0;
            int organizedCount = 0;

            string fbxDir = Path.Combine(rootDirectoryPath, "Fbx");
            string musicDir = Path.Combine(rootDirectoryPath, "Musica");

            if (!Directory.Exists(fbxDir)) Directory.CreateDirectory(fbxDir);
            if (!Directory.Exists(musicDir)) Directory.CreateDirectory(musicDir);

            // Subdirectories
            string memesDir = Path.Combine(fbxDir, "Cartoon-Animados");
            string animeDir = Path.Combine(fbxDir, "Anime-Manga");
            string techDir = Path.Combine(fbxDir, "Internet-Ordenadores");
            string animalDir = Path.Combine(fbxDir, "Animales");
            string natureDir = Path.Combine(fbxDir, "Naturaleza-Liquidos");
            string hitsDir = Path.Combine(fbxDir, "Golpes-Puñetazos");
            string reviewDir = Path.Combine(fbxDir, "Por_Clasificar");
            string musicGeneralDir = Path.Combine(musicDir, "Productividad");

            if (!Directory.Exists(memesDir)) Directory.CreateDirectory(memesDir);
            if (!Directory.Exists(animeDir)) Directory.CreateDirectory(animeDir);
            if (!Directory.Exists(techDir)) Directory.CreateDirectory(techDir);
            if (!Directory.Exists(animalDir)) Directory.CreateDirectory(animalDir);
            if (!Directory.Exists(natureDir)) Directory.CreateDirectory(natureDir);
            if (!Directory.Exists(hitsDir)) Directory.CreateDirectory(hitsDir);
            if (!Directory.Exists(reviewDir)) Directory.CreateDirectory(reviewDir);
            if (!Directory.Exists(musicGeneralDir)) Directory.CreateDirectory(musicGeneralDir);

            var allFiles = new List<string>();
            CollectAudioFilesRecursive(rootDirectoryPath, allFiles);

            int total = allFiles.Count;

            for (int i = 0; i < total; i++)
            {
                string file = allFiles[i];
                if (onProgress != null)
                    onProgress(i + 1, total, Path.GetFileName(file));

                try
                {
                    string ext = Path.GetExtension(file);
                    string cleanFileNameNoExt = FormatCleanSpanishFileName(file);
                    string targetFileName = cleanFileNameNoExt + ext;
                    string currentDir = Path.GetDirectoryName(file);
                    string currentDirName = Path.GetFileName(currentDir);
                    string currentFileName = Path.GetFileName(file);

                    bool isLooseFile = (currentDir.Equals(rootDirectoryPath, StringComparison.OrdinalIgnoreCase));
                    bool inGenericFolder = (currentDirName.Equals("SinOrdenar", StringComparison.OrdinalIgnoreCase) ||
                                            currentDirName.Equals("PorClasificar", StringComparison.OrdinalIgnoreCase) ||
                                            currentDirName.Equals("Por_Clasificar", StringComparison.OrdinalIgnoreCase) ||
                                            currentDirName.Equals("Efectos Frecuentes", StringComparison.OrdinalIgnoreCase));
                    bool needsRename = !currentFileName.Equals(targetFileName, StringComparison.OrdinalIgnoreCase);

                    string targetFolder = currentDir;
                    if (isLooseFile || inGenericFolder)
                    {
                        double duration = GetAudioDurationSeconds(file);
                        if (duration >= 30.0)
                        {
                            targetFolder = musicGeneralDir;
                        }
                        else
                        {
                            var autoTagged = TagEngine.AutoTag(file);
                            if (autoTagged.SuggestedFolder == "Internet-Ordenadores") targetFolder = techDir;
                            else if (autoTagged.SuggestedFolder == "Animales") targetFolder = animalDir;
                            else if (autoTagged.SuggestedFolder == "Anime-Manga") targetFolder = animeDir;
                            else if (autoTagged.SuggestedFolder == "Naturaleza-Liquidos") targetFolder = natureDir;
                            else if (autoTagged.SuggestedFolder == "Golpes-Puñetazos") targetFolder = hitsDir;
                            else if (autoTagged.NeedsReview) targetFolder = reviewDir;
                            else targetFolder = memesDir;
                        }
                    }

                    string targetFilePath = Path.Combine(targetFolder, targetFileName);

                    if (needsRename || !targetFolder.Equals(currentDir, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(targetFilePath) && !targetFilePath.Equals(file, StringComparison.OrdinalIgnoreCase))
                        {
                            int counter = 1;
                            while (File.Exists(targetFilePath))
                            {
                                targetFilePath = Path.Combine(targetFolder, cleanFileNameNoExt + "_" + counter.ToString() + ext);
                                counter++;
                            }
                        }

                        if (!targetFilePath.Equals(file, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Move(file, targetFilePath);
                            organizedCount++;
                        }
                    }
                }
                catch { }
            }

            return organizedCount;
        }

        private static void CollectAudioFilesRecursive(string path, List<string> list)
        {
            try
            {
                foreach (var f in Directory.GetFiles(path))
                    if (TagEngine.IsAudioFile(f)) list.Add(f);

                foreach (var d in Directory.GetDirectories(path))
                    CollectAudioFilesRecursive(d, list);
            }
            catch { }
        }

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

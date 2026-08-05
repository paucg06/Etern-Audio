using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace EternAudio
{
    public static class TagEngine
    {
        public static readonly string[] AudioExtensions = { ".wav", ".mp3", ".aac", ".ogg", ".flac", ".m4a", ".wma", ".opus" };

        public static bool IsAudioFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            foreach (var ae in AudioExtensions)
                if (ae == ext) return true;
            return false;
        }

        // ─── Extensive Open Bilingual (ES + EN) Concept Thesaurus ─────────────
        private static readonly Dictionary<string, string[]> ConceptGraph =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Vulgarities, Slang & Bodily Sounds (caca, mierda, shit, poop, fart, pedo)
            { "caca",        new[] {"caca","mierda","shit","poop","crap","turd","dung","feces","sucio","dirty","pedo","fart","comedia","meme","perro","dog"} },
            { "mierda",      new[] {"mierda","caca","shit","poop","crap","turd","dung","feces","sucio","dirty","pedo","fart","comedia","meme"} },
            { "shit",        new[] {"shit","caca","mierda","poop","crap","turd","dung","feces","dirty","fart","pedo","meme","comedy"} },
            { "poop",        new[] {"poop","shit","caca","mierda","crap","turd","dung","feces","dirty","fart","pedo","meme"} },
            { "crap",        new[] {"crap","shit","poop","caca","mierda","turd","dirty","meme"} },
            { "pedo",        new[] {"pedo","fart","toot","gases","gas","caca","mierda","shit","poop","comedia","funny"} },
            { "fart",        new[] {"fart","pedo","toot","gases","gas","caca","mierda","shit","poop","comedy","funny"} },

            // Poultry & Birds (pollo, gallo, chicken, rooster, nugget, kikiriki)
            { "gallo",        new[] {"gallo","pollo","rooster","chicken","kikiriki","kikirikigallo","nugget","granja","ave","pajaro","farm","cock","bird","corral"} },
            { "pollo",        new[] {"pollo","gallo","chicken","rooster","kikiriki","kikirikigallo","nugget","granja","ave","pajaro","farm","bird","fried_chicken"} },
            { "chicken",      new[] {"chicken","pollo","gallo","rooster","kikiriki","nugget","farm","granja","bird","ave"} },
            { "rooster",      new[] {"rooster","gallo","pollo","chicken","kikiriki","farm","granja","bird","ave"} },
            { "kikiriki",     new[] {"kikiriki","kikirikigallo","gallo","pollo","rooster","chicken","granja","ave"} },
            { "nugget",       new[] {"nugget","chicken","pollo","gallo","rooster","comedia","meme"} },

            // Domestic Animals & Wildlife
            { "perro",        new[] {"perro","dog","bark","ladridos","ladrido","puppy","hound","canino","mascota","can","caca","poop"} },
            { "dog",          new[] {"dog","perro","bark","ladrido","puppy","hound","canino","mascota"} },
            { "gato",         new[] {"gato","cat","meow","maullido","miau","kitten","felino","purr","ronroneo"} },
            { "cat",          new[] {"cat","gato","meow","maullido","miau","kitten","felino","purr"} },
            { "mono",         new[] {"mono","monkey","ape","chimpance","gorila","selva","jungle","primate"} },
            { "monkey",       new[] {"monkey","mono","ape","chimpance","gorila","selva","jungle"} },

            // Computing, Mouse, Systems & Tech
            { "raton",        new[] {"raton","mouse","clic","click","boton","button","teclado","keyboard","ui","interfaz","pc","ordenador","roedor","queso","select"} },
            { "mouse",        new[] {"mouse","raton","clic","click","boton","button","teclado","keyboard","ui","interfaz","pc","ordenador","roedor","queso","select"} },
            { "click",        new[] {"click","clic","mouse","raton","boton","button","select","press","ui","interfaz","teclado","keyboard","pulsar"} },
            { "clic",         new[] {"clic","click","mouse","raton","boton","button","select","press","ui","interfaz","teclado","keyboard","pulsar"} },
            { "windows",      new[] {"windows","windows_xp","sistema","ordenador","pc","error","apagandose","error_windows","software"} },
            { "teclado",      new[] {"teclado","keyboard","typing","tipear","click","clic","boton","iphone","pc","escritura"} },

            // House, Architecture & Domestic
            { "casa",         new[] {"casa","house","home","hogar","puerta","door","ventana","window","cocina","habitacion","pasos","madera","llave","lock","reloj","ambiente","edificio","domestico"} },
            { "house",        new[] {"house","casa","home","hogar","puerta","door","ventana","window","room","key","lock","domestic"} },
            { "home",         new[] {"home","house","casa","hogar","puerta","door","room","domestic"} },
            { "puerta",       new[] {"puerta","door","gate","pestillo","lock","llave","porton","madera","cerrar","abrir","golpe_puerta","casa","house"} },
            { "door",         new[] {"door","puerta","gate","lock","key","wood","close","open","house","home"} },
            { "ventana",      new[] {"ventana","window","cristal","glass","abrir","casa","house"} },

            // Water, Liquids & Nature
            { "agua",         new[] {"agua","water","boil","hervir","liquido","lluvia","rain","rio","river","mar","sea","splash","fluido"} },
            { "water",        new[] {"water","agua","boil","rain","lluvia","sea","river","splash","liquid"} },
            { "boil",         new[] {"boil","hervir","agua","water","burbujas","liquido"} },
            { "lluvia",       new[] {"lluvia","rain","drizzle","shower","agua","chubasco","tormenta","water"} },
            { "viento",       new[] {"viento","wind","breeze","gust","brisa","rafaga","tormenta","aire"} },

            // Vehicles & Transport
            { "coche",        new[] {"coche","auto","car","vehiculo","motor","engine","rueda","wheel","freno","brake","claxon","horn","trafico","traffic"} },
            { "car",          new[] {"car","coche","auto","vehicle","engine","motor","drive","wheel","brake","horn"} },

            // Anime, Japan & Cartoons
            { "japon",        new[] {"japon","japan","anime","manga","otaku","tokyo","ninja","samurai","japones","japanese","goku","doraemon","kirby","nintendo","ching_cheng"} },
            { "japan",        new[] {"japan","japon","anime","manga","otaku","tokyo","ninja","samurai","japones","japanese","goku","doraemon","kirby","nintendo"} },
            { "anime",        new[] {"anime","manga","japon","japones","japanese","cartoon","dibujos","animados","otaku","goku","doraemon","kirby","naruto","sensei","hora_hora"} },
            { "cartoon",      new[] {"cartoon","anime","dibujos","animados","comedia","funny","comedy","silly","humor","meme","kirby","doraemon","animacion"} },
            { "sensei",       new[] {"sensei","anime","japones","frase","hora_hora","manga","otaku"} },
            { "goku",         new[] {"goku","dragonball","anime","drama","meme","comedy","saiyan","kamehameha"} },
            { "kirby",        new[] {"kirby","cartoon","funny","game","nintendo","comedia","meme","dibujos","nintendo_switch"} },

            // Hits, Punches & Combat
            { "golpe",        new[] {"golpe","hit","punch","impact","impacto","crash","smash","puñetazo","slap","puño","puñetazo_dani"} },
            { "puñetazo",     new[] {"puñetazo","punch","hit","golpe","impacto","boxeo","fight","pelea","puño","puñetazo_dani"} },
            { "hit",          new[] {"hit","golpe","punch","impact","impacto","smash","slap","puñetazo"} },
            { "arma",         new[] {"arma","gun","weapon","shoot","fire","pistola","fusil","disparo","bala","tiro","espada"} },
            { "espada",       new[] {"espada","sword","blade","slash","slice","clang","hoja","tajo","corte","metal"} },

            // Voice & Screams
            { "grito",        new[] {"grito","scream","yell","shout","cry","gritar","chillar","terror","horror","miedo","ayuda","ahhh"} },
            { "voz",          new[] {"voz","voice","human","speak","talk","humano","hablar","habla","grito","frase","persona"} }
        };

        private static readonly Dictionary<string, string> CategoryMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"explosion","Explosión"},{"bomba","Explosión"},{"boom","Explosión"},
            {"impact","Golpes-Puñetazos"},{"impacto","Golpes-Puñetazos"},{"hit","Golpes-Puñetazos"},{"golpe","Golpes-Puñetazos"},{"puñetazo","Golpes-Puñetazos"},
            {"gallo","Animales"},{"pollo","Animales"},{"chicken","Animales"},{"rooster","Animales"},{"perro","Animales"},{"dog","Animales"},{"gato","Animales"},{"cat","Animales"},{"mono","Animales"},{"caca","Animales"},{"mierda","Animales"},{"shit","Animales"},{"poop","Animales"},{"pedo","Animales"},{"fart","Animales"},
            {"agua","Naturaleza-Liquidos"},{"water","Naturaleza-Liquidos"},{"boil","Naturaleza-Liquidos"},{"lluvia","Naturaleza-Liquidos"},{"viento","Naturaleza-Liquidos"},
            {"windows","Internet-Ordenadores"},{"teclado","Internet-Ordenadores"},{"mouse","Internet-Ordenadores"},{"raton","Internet-Ordenadores"},
            {"anime","Anime-Manga"},{"japon","Anime-Manga"},{"japan","Anime-Manga"},{"sensei","Anime-Manga"},{"goku","Anime-Manga"},{"doraemon","Anime-Manga"},{"kirby","Anime-Manga"},
            {"puerta","Objetos-Herramientas"},{"ventana","Objetos-Herramientas"},{"casa","Objetos-Herramientas"}
        };

        public static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder();
            foreach (char c in text.ToLowerInvariant())
            {
                switch (c)
                {
                    case 'á': case 'à': case 'ä': sb.Append('a'); break;
                    case 'é': case 'è': case 'ë': sb.Append('e'); break;
                    case 'í': case 'ì': case 'ï': sb.Append('i'); break;
                    case 'ó': case 'ò': case 'ö': sb.Append('o'); break;
                    case 'ú': case 'ù': case 'ü': sb.Append('u'); break;
                    case 'ñ': sb.Append('n'); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        public static string[] TokenizeFilename(string filename)
        {
            string clean = FileOrganizer.FormatCleanSpanishFileName(filename);
            var tokens = Regex.Split(clean, @"[\s\-_\.]+");
            var result = new List<string>();
            foreach (var t in tokens)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                if (Regex.IsMatch(t, @"^\d+$")) continue;
                if (t.Length < 2) continue;
                result.Add(NormalizeText(t));
            }
            return result.ToArray();
        }

        public static SfxFile AutoTag(string filePath)
        {
            string rawFilename = Path.GetFileName(filePath);
            string cleanFileName = FileOrganizer.FormatCleanSpanishFileName(rawFilename) + Path.GetExtension(rawFilename);
            string[] tokens = TokenizeFilename(rawFilename);

            var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var token in tokens)
                tagSet.Add(token);

            foreach (var token in tokens)
            {
                if (ConceptGraph.ContainsKey(token))
                    foreach (var syn in ConceptGraph[token])
                        tagSet.Add(syn);

                foreach (var kvp in ConceptGraph)
                {
                    if (kvp.Key.Length >= 3 && token.Length >= 3 &&
                        (kvp.Key.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         token.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        foreach (var syn in kvp.Value)
                            tagSet.Add(syn);
                    }
                }

                if (CategoryMap.ContainsKey(token))
                {
                    string cat = CategoryMap[token];
                    if (!categoryCounts.ContainsKey(cat)) categoryCounts[cat] = 0;
                    categoryCounts[cat]++;
                }
            }

            string dirName = Path.GetFileName(Path.GetDirectoryName(filePath));
            if (!string.IsNullOrEmpty(dirName))
            {
                var dirToks = Regex.Split(dirName, @"[\s\-_\.]+");
                foreach (var dt in dirToks)
                    if (dt.Length >= 2) tagSet.Add(NormalizeText(dt));
            }

            double duration = FileOrganizer.GetAudioDurationSeconds(filePath);
            bool isShort = duration < 30.0;

            string suggestedSubfolder = "Efectos Frecuentes";
            double confidence = 0.50;

            if (categoryCounts.Count > 0)
            {
                int maxCount = 0;
                foreach (var kvp in categoryCounts)
                {
                    if (kvp.Value > maxCount)
                    {
                        maxCount = kvp.Value;
                        suggestedSubfolder = kvp.Key;
                    }
                }
                confidence = Math.Min(0.95, 0.70 + (maxCount * 0.15));
            }
            else if (!string.IsNullOrEmpty(dirName) && dirName != "Efectos Sonido" && dirName != "Fbx" && dirName != "Musica" && dirName != "PorClasificar" && dirName != "SinOrdenar")
            {
                suggestedSubfolder = dirName;
                confidence = 0.90;
            }

            bool needsReview = confidence < 0.80;
            string category = !string.IsNullOrEmpty(dirName) && dirName != "Fbx" && dirName != "Musica" ? dirName : suggestedSubfolder;
            string displayName = Path.GetFileNameWithoutExtension(cleanFileName).Replace("_", " ").Trim();
            var tagList = new List<string>(tagSet);

            long fileSize = 0;
            try { fileSize = new FileInfo(filePath).Length; } catch { }

            return new SfxFile
            {
                FilePath = filePath,
                FileName = cleanFileName,
                DisplayName = displayName,
                OriginalRawName = rawFilename,
                Tags = tagList.ToArray(),
                Category = category,
                SubCategory = suggestedSubfolder,
                SuggestedFolder = suggestedSubfolder,
                ConfidenceScore = confidence,
                FileSizeBytes = fileSize,
                DurationSeconds = duration,
                IsShortSfx = isShort,
                NeedsReview = needsReview
            };
        }

        public static string[] ExpandQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new string[0];
            string normalized = NormalizeText(query);
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            expanded.Add(normalized);

            var words = normalized.Split(new char[] { ' ', ',', ';', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                expanded.Add(word);
                if (ConceptGraph.ContainsKey(word))
                {
                    foreach (var syn in ConceptGraph[word])
                        expanded.Add(NormalizeText(syn));
                }

                foreach (var kvp in ConceptGraph)
                {
                    if (kvp.Key.Length >= 3 && word.Length >= 3 &&
                        (kvp.Key.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         word.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        foreach (var syn in kvp.Value)
                            expanded.Add(NormalizeText(syn));
                    }
                }
            }

            return new List<string>(expanded).ToArray();
        }

        public static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1048576) return (bytes / 1024.0).ToString("F1") + " KB";
            return (bytes / 1048576.0).ToString("F1") + " MB";
        }

        public static string GetCategoryColor(string category)
        {
            if (category.IndexOf("Música", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Musica", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("8Bit", StringComparison.OrdinalIgnoreCase) >= 0) return "#bc8cff";
            if (category.IndexOf("Animal", StringComparison.OrdinalIgnoreCase) >= 0) return "#a3855d";
            if (category.IndexOf("Cartoon", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Anime", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Comedia", StringComparison.OrdinalIgnoreCase) >= 0) return "#fb923c";
            if (category.IndexOf("Golpe", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Impacto", StringComparison.OrdinalIgnoreCase) >= 0) return "#f97316";
            if (category.IndexOf("Naturaleza", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Liquidos", StringComparison.OrdinalIgnoreCase) >= 0) return "#38bdf8";
            if (category.IndexOf("Internet", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Ordenadores", StringComparison.OrdinalIgnoreCase) >= 0) return "#58a6ff";
            if (category.IndexOf("Por Clasificar", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("⚠️", StringComparison.OrdinalIgnoreCase) >= 0) return "#f59e0b";
            return "#39d353";
        }

        public static readonly string[] AllCategories = {
            "Todos los audios", "EFX / Cortos (<30s)", "Música / Largos (>=30s)", "⚠️ Por Clasificar",
            "Abucheos-Insultos", "Animales", "Anime-Manga", "Campanas-Bongs-Alarmas", "Cartoon-Animados",
            "Censuras - Distorsiones - Explosiones", "Drama-Terror", "Efectos Frecuentes",
            "Frases", "Golpes-Puñetazos", "Internet-Ordenadores", "Naturaleza-Liquidos", "Objetos-Herramientas", "Transiciones", "Whoosh",
            "8Bit", "Energeticas", "Epicas-God", "Productividad", "Triste-Fail"
        };
    }
}

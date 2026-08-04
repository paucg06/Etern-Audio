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

        private static readonly Dictionary<string, string[]> SynonymMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "explosion",    new[] {"explosion","explosión","boom","blast","bang","detonate","detonation","kaboom","burst","bomba","estallido","bum","blowup","detonacion"} },
            { "bomba",        new[] {"bomba","bomb","explosion","blast","bang","estallido","explosión","granada"} },
            { "impact",       new[] {"impact","hit","strike","crash","smash","bang","blow","golpe","impacto","choque","colision"} },
            { "impacto",      new[] {"impacto","impact","hit","strike","golpe","choque","crash","smash"} },
            { "hit",          new[] {"hit","impact","punch","strike","blow","slap","golpe","golpear","impacto"} },
            { "golpe",        new[] {"golpe","hit","impact","punch","strike","blow","thud","crash","impacto","bash"} },
            { "bone",         new[] {"bone","hueso","break","crack","fracture","romper","crujido"} },
            { "hueso",        new[] {"hueso","bone","break","crack","fracture","romper","crujido"} },
            { "romperse",     new[] {"romperse","break","bone","hueso","crack","fracture","romper"} },
            { "nature",       new[] {"nature","natural","outdoor","environment","forest","wind","rain","naturaleza","ambiente","bosque","viento","lluvia"} },
            { "viento",       new[] {"viento","wind","breeze","gust","brisa","rafaga","tormenta","storm"} },
            { "rain",         new[] {"rain","drizzle","shower","storm","water","lluvia","chubasco","aguacero","agua","tormenta"} },
            { "lluvia",       new[] {"lluvia","rain","drizzle","shower","agua","chubasco","tormenta","storm"} },
            { "footstep",     new[] {"footstep","step","walk","run","feet","foot","paso","caminar","correr","pie","steps"} },
            { "paso",         new[] {"paso","footstep","step","walk","feet","caminar","pie","steps"} },
            { "gun",          new[] {"gun","shoot","fire","weapon","pistol","rifle","shot","arma","disparo","pistola","fusil","bala"} },
            { "arma",         new[] {"arma","gun","weapon","shoot","fire","pistola","fusil","disparo","bala"} },
            { "disparo",      new[] {"disparo","shoot","shot","gun","fire","arma","bala","pistola","fusil","tiro"} },
            { "sword",        new[] {"sword","blade","slash","slice","clang","espada","hoja","tajo","corte","metal"} },
            { "click",        new[] {"click","button","select","press","mouse","clic","boton","seleccionar","pulsar"} },
            { "clic",         new[] {"clic","click","button","select","press","boton","seleccionar","pulsar"} },
            { "boton",        new[] {"boton","button","click","select","clic","respuesta","ui"} },
            { "error",        new[] {"error","fail","wrong","buzz","incorrect","fallo","equivocacion","incorrecto","respuesta"} },
            { "incorrecto",   new[] {"incorrecto","error","fail","wrong","fallo","respuesta","boton"} },
            { "whoosh",       new[] {"whoosh","swipe","fast","speed","swoosh","transition","rapido","veloz","transicion","silbido"} },
            { "voice",        new[] {"voice","human","speak","talk","vocal","voz","humano","hablar","habla"} },
            { "voz",          new[] {"voz","voice","human","speak","talk","humano","hablar","habla"} },
            { "grito",        new[] {"grito","scream","yell","shout","cry","gritar","chillar","terror","horror"} },
            { "meme",         new[] {"meme","comedy","funny","funny_sound","comedia","humor","gracioso","frase"} },
            { "frase",        new[] {"frase","voice","speech","voz","habla","humano","meme"} },
            { "comedy",       new[] {"comedy","funny","cartoon","silly","humorous","comedia","gracioso","dibujos","tonto","humor","meme"} },
            { "kirby",        new[] {"kirby","cartoon","funny","game","nintendo","comedia","meme"} },
            { "goku",         new[] {"goku","dragonball","anime","drama","meme","comedy"} },
            { "doraemon",     new[] {"doraemon","anime","cartoon","funny","dibujos","comedia"} },
            { "cartoon",      new[] {"cartoon","funny","comedy","animated","silly","dibujos","gracioso","comedia"} },
            { "yunke",        new[] {"yunke","anvil","yunque","metal","hit","golpe","acero"} },
            { "motosierra",   new[] {"motosierra","chainsaw","wood","madera","cortar"} },
            { "music",        new[] {"music","musical","melody","tune","song","musica","melodia","cancion","ritmo"} },
            { "musica",       new[] {"musica","music","musical","melody","melodia","cancion","ritmo"} },
        };

        private static readonly Dictionary<string, string> CategoryMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"explosion","Explosión"},{"bomba","Explosión"},{"boom","Explosión"},
            {"impact","Impacto"},{"impacto","Impacto"},{"hit","Impacto"},{"golpe","Impacto"},{"caida","Impacto"},{"hueso","Impacto"},{"romperse","Impacto"},
            {"nature","Naturaleza"},{"naturaleza","Naturaleza"},{"viento","Naturaleza"},{"lluvia","Naturaleza"},{"trueno","Naturaleza"},{"terremoto","Naturaleza"},
            {"footstep","Pasos"},{"paso","Pasos"},{"caminar","Pasos"},{"correr","Pasos"},
            {"car","Vehículo"},{"coche","Vehículo"},{"engine","Vehículo"},{"motor","Vehículo"},
            {"gun","Arma"},{"arma","Arma"},{"disparo","Arma"},{"espada","Arma"},
            {"click","Interfaz"},{"clic","Interfaz"},{"boton","Interfaz"},{"error","Interfaz"},{"incorrecto","Interfaz"},{"whoosh","Interfaz"},{"teclado","Interfaz"},{"iphone","Interfaz"},
            {"voice","Voz"},{"voz","Voz"},{"grito","Voz"},{"frase","Voz"},{"sniff","Voz"},{"esnifar","Voz"},
            {"comedy","Comedia"},{"comedia","Comedia"},{"cartoon","Comedia"},{"kirby","Comedia"},{"goku","Comedia"},{"doraemon","Comedia"},{"meme","Comedia"},
            {"yunke","Metal"},{"metal","Metal"},
            {"madera","Madera"},{"motosierra","Madera"},
            {"music","Música"},{"musica","Música"},
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
                if (SynonymMap.ContainsKey(token))
                    foreach (var syn in SynonymMap[token])
                        tagSet.Add(syn);

                foreach (var kvp in SynonymMap)
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

            // Determine SubCategory from directory structure if available
            string dirName = Path.GetFileName(Path.GetDirectoryName(filePath));
            string parentDirName = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(filePath)));
            string subCategory = string.IsNullOrEmpty(dirName) ? "General" : dirName;

            double duration = FileOrganizer.GetAudioDurationSeconds(filePath);
            bool isShort = duration < 30.0;

            string category = isShort ? "EFX / Corto" : "Música / Largo";
            if (!string.IsNullOrEmpty(dirName) && dirName != "Efectos Sonido" && dirName != "Fbx" && dirName != "Musica")
            {
                category = dirName;
            }
            else if (categoryCounts.Count > 0)
            {
                int maxCount = 0;
                foreach (var kvp in categoryCounts)
                    if (kvp.Value > maxCount) { maxCount = kvp.Value; category = kvp.Key; }
            }

            string displayName = Path.GetFileNameWithoutExtension(cleanFileName).Replace("_", " ").Trim();

            var tagList = new List<string>(tagSet);
            if (tagList.Count > 25) tagList = tagList.GetRange(0, 25);

            long fileSize = 0;
            try { fileSize = new FileInfo(filePath).Length; } catch { }

            return new SfxFile
            {
                FilePath = filePath,
                FileName = cleanFileName,
                DisplayName = displayName,
                Tags = tagList.ToArray(),
                Category = category,
                SubCategory = subCategory,
                FileSizeBytes = fileSize,
                DurationSeconds = duration,
                IsShortSfx = isShort
            };
        }

        public static string[] ExpandQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new string[0];
            string normalized = NormalizeText(query);
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            expanded.Add(normalized);

            var words = normalized.Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                expanded.Add(word);
                if (SynonymMap.ContainsKey(word))
                    foreach (var syn in SynonymMap[word])
                        expanded.Add(NormalizeText(syn));

                foreach (var kvp in SynonymMap)
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
            if (category.IndexOf("Cartoon", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Comedia", StringComparison.OrdinalIgnoreCase) >= 0) return "#fb923c";
            if (category.IndexOf("Golpe", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Impacto", StringComparison.OrdinalIgnoreCase) >= 0) return "#f97316";
            if (category.IndexOf("Terror", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Drama", StringComparison.OrdinalIgnoreCase) >= 0) return "#7c3aed";
            if (category.IndexOf("Transicion", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Whoosh", StringComparison.OrdinalIgnoreCase) >= 0) return "#58a6ff";
            return "#39d353";
        }

        public static readonly string[] AllCategories = {
            "Todos los audios", "EFX / Cortos (<30s)", "Música / Largos (>=30s)",
            "Abucheos-Insultos", "Animal", "Campanas-Bongs-Alarmas", "Cartoon-Animados",
            "Censuras - Distorsiones - Explosiones", "Drama-Terror", "Efectos Frecuentes",
            "Frases", "Golpes-Puñetazos", "Internet-Ordenadores", "Objetos", "Transiciones", "Whoosh",
            "8Bit", "Energeticas", "Epicas-God", "Productividad", "Triste-Fail"
        };
    }
}

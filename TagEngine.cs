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

        // ─── Comprehensive Multi-Domain Semantic Concept Graph ─────────────────
        private static readonly Dictionary<string, string[]> ConceptGraph =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Computing, Mouse & Interface
            { "raton",        new[] {"raton","mouse","clic","click","boton","button","teclado","keyboard","ui","interfaz","pc","ordenador","roedor","queso","presionar","select"} },
            { "mouse",        new[] {"mouse","raton","clic","click","boton","button","teclado","keyboard","ui","interfaz","pc","ordenador","roedor","queso","select"} },
            { "click",        new[] {"click","clic","mouse","raton","boton","button","select","press","ui","interfaz","teclado","keyboard","pulsar"} },
            { "clic",         new[] {"clic","click","mouse","raton","boton","button","select","press","ui","interfaz","teclado","keyboard","pulsar"} },
            { "teclado",      new[] {"teclado","keyboard","typing","tipear","click","clic","boton","iphone","pc","escritura","persona_escribiendo"} },
            { "keyboard",     new[] {"keyboard","teclado","typing","tipear","click","clic","boton","pc"} },

            // House, Domestic & Buildings
            { "casa",         new[] {"casa","house","home","hogar","puerta","door","ventana","window","cocina","habitacion","pasos","madera","llave","lock","reloj","ambiente","edificio","domestico","pestillo"} },
            { "house",        new[] {"house","casa","home","hogar","puerta","door","ventana","window","room","key","lock","domestic"} },
            { "home",         new[] {"home","house","casa","hogar","puerta","door","room","domestic"} },
            { "puerta",       new[] {"puerta","door","gate","pestillo","lock","llave","porton","madera","cerrar","abrir","golpe_puerta","casa","house"} },
            { "door",         new[] {"door","puerta","gate","lock","key","wood","close","open","house","home"} },
            { "ventana",      new[] {"ventana","window","cristal","glass","abrir","casa","house"} },

            // Animals & Farm
            { "gallo",        new[] {"gallo","pollo","rooster","chicken","kikiriki","kikirikigallo","granja","ave","pajaro","farm","cock","bird","corral"} },
            { "pollo",        new[] {"pollo","gallo","chicken","rooster","kikiriki","kikirikigallo","granja","ave","pajaro","farm","bird"} },
            { "kikiriki",     new[] {"kikiriki","kikirikigallo","gallo","pollo","rooster","chicken","granja","ave"} },
            { "perro",        new[] {"perro","dog","bark","ladridos","ladrido","puppy","hound","canino","mascota","can"} },
            { "dog",          new[] {"dog","perro","bark","ladrido","puppy","hound","canino"} },
            { "gato",         new[] {"gato","cat","meow","maullido","miau","kitten","felino","purr","ronroneo"} },
            { "cat",          new[] {"cat","gato","meow","maullido","miau","kitten","felino"} },
            { "mono",         new[] {"mono","monkey","ape","chimpance","gorila","selva","jungle","primate"} },

            // Culture, Anime, Japan & Cartoons
            { "japon",        new[] {"japon","japan","anime","manga","otaku","tokyo","ninja","samurai","japones","japanese","asian","oriental","goku","doraemon","kirby","nintendo","ching_cheng"} },
            { "japan",        new[] {"japan","japon","anime","manga","otaku","tokyo","ninja","samurai","japones","japanese","asian","oriental","goku","doraemon","kirby","nintendo"} },
            { "anime",        new[] {"anime","manga","japon","japones","japanese","cartoon","dibujos","animados","otaku","goku","doraemon","kirby","naruto","dragonball","one_piece","hora_hora","samurai","ninja"} },
            { "cartoon",      new[] {"cartoon","anime","dibujos","animados","comedia","funny","comedy","silly","humor","meme","kirby","doraemon","looney","animacion"} },
            { "meme",         new[] {"meme","comedia","comedy","funny","funny_sound","viral","humor","gracioso","risas","redes","tiktok","youtube","goku","kirby","doraemon","ching_cheng","fail"} },
            { "kirby",        new[] {"kirby","cartoon","funny","game","nintendo","comedia","meme","dibujos","rosa","super_smash","nintendo_switch"} },
            { "goku",         new[] {"goku","dragonball","anime","drama","meme","comedy","saiyan","goku_meme","kamehameha","japon"} },

            // Combat & Hits
            { "golpe",        new[] {"golpe","hit","punch","impact","impacto","crash","smash","puñetazo","slap","puño","seco","puñetazo_cartoon","golpe_seco","choque"} },
            { "puñetazo",     new[] {"puñetazo","punch","hit","golpe","impacto","boxeo","fight","pelea","puño","puñetazo_cartoon"} },
            { "hit",          new[] {"hit","golpe","punch","impact","impacto","smash","slap","puñetazo","strike"} },
            { "impacto",      new[] {"impacto","impact","hit","strike","golpe","choque","crash","smash","caida","suelo"} },
            { "romperse",     new[] {"romperse","break","bone","hueso","crack","fracture","romper","crujido","ruptura","fractura"} },
            { "bone",         new[] {"bone","hueso","break","crack","fracture","romper","crujido","romperse","cuerpo"} },
            { "hueso",        new[] {"hueso","bone","break","crack","fracture","romper","crujido","romperse","cuerpo"} },

            // Water & Nature
            { "agua",         new[] {"agua","water","lluvia","rain","rio","river","mar","sea","splash","chubasco","gota","drizzle","fluido"} },
            { "water",        new[] {"water","agua","rain","lluvia","sea","river","splash","drizzle"} },
            { "lluvia",       new[] {"lluvia","rain","drizzle","shower","agua","chubasco","tormenta","storm","water"} },
            { "viento",       new[] {"viento","wind","breeze","gust","brisa","rafaga","tormenta","storm","aire"} },

            // Vehicles
            { "coche",        new[] {"coche","auto","car","vehiculo","motor","engine","rueda","freno","claxon","trafico"} },
            { "car",          new[] {"car","coche","auto","vehicle","engine","motor","drive"} },

            // Voice & Scream
            { "grito",        new[] {"grito","scream","yell","shout","cry","gritar","chillar","terror","horror","miedo","ayuda","ahhh"} },
            { "voz",          new[] {"voz","voice","human","speak","talk","humano","hablar","habla","grito","frase","persona"} }
        };

        private static readonly Dictionary<string, string> CategoryMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"explosion","Explosión"},{"bomba","Explosión"},{"boom","Explosión"},
            {"impact","Golpes-Puñetazos"},{"impacto","Golpes-Puñetazos"},{"hit","Golpes-Puñetazos"},{"golpe","Golpes-Puñetazos"},{"puñetazo","Golpes-Puñetazos"},{"caida","Golpes-Puñetazos"},{"hueso","Golpes-Puñetazos"},{"romperse","Golpes-Puñetazos"},
            {"gallo","Animal"},{"pollo","Animal"},{"perro","Animal"},{"gato","Animal"},{"mono","Animal"},{"vaca","Animal"},{"caballo","Animal"},{"pajaro","Animal"},
            {"nature","Naturaleza"},{"naturaleza","Naturaleza"},{"viento","Naturaleza"},{"lluvia","Naturaleza"},{"trueno","Naturaleza"},{"terremoto","Naturaleza"},
            {"footstep","Pasos"},{"paso","Pasos"},{"caminar","Pasos"},{"correr","Pasos"},
            {"car","Vehículo"},{"coche","Vehículo"},{"engine","Vehículo"},{"motor","Vehículo"},
            {"gun","Arma"},{"arma","Arma"},{"disparo","Arma"},{"espada","Arma"},
            {"click","Interfaz"},{"clic","Interfaz"},{"boton","Interfaz"},{"error","Interfaz"},{"incorrecto","Interfaz"},{"whoosh","Whoosh"},{"teclado","Internet-Ordenadores"},{"iphone","Internet-Ordenadores"},{"raton","Internet-Ordenadores"},{"mouse","Internet-Ordenadores"},
            {"voice","Frases"},{"voz","Frases"},{"grito","Frases"},{"frase","Frases"},{"sniff","Frases"},{"esnifar","Frases"},
            {"comedy","Cartoon-Animados"},{"comedia","Cartoon-Animados"},{"cartoon","Cartoon-Animados"},{"kirby","Cartoon-Animados"},{"goku","Cartoon-Animados"},{"doraemon","Cartoon-Animados"},{"meme","Cartoon-Animados"},{"japon","Cartoon-Animados"},{"anime","Cartoon-Animados"},
            {"yunke","Objetos"},{"metal","Objetos"},{"madera","Objetos"},{"motosierra","Objetos"},{"puerta","Objetos"},{"ventana","Objetos"},{"casa","Objetos"},
            {"music","Música"},{"musica","Música"}
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

            // Include directory hierarchy tokens as tags
            string dirName = Path.GetFileName(Path.GetDirectoryName(filePath));
            if (!string.IsNullOrEmpty(dirName))
            {
                var dirToks = Regex.Split(dirName, @"[\s\-_\.]+");
                foreach (var dt in dirToks)
                    if (dt.Length >= 2) tagSet.Add(NormalizeText(dt));
            }

            double duration = FileOrganizer.GetAudioDurationSeconds(filePath);
            bool isShort = duration < 30.0;
            bool needsReview = false;

            string subCategory = string.IsNullOrEmpty(dirName) ? "General" : dirName;
            string category = isShort ? "EFX / Cortos" : "Música / Largos";

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
            else
            {
                category = "⚠️ Por Clasificar";
                needsReview = true;
            }

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
                SubCategory = subCategory,
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

        // Fuzzy Levenshtein Distance for typo matching (e.g. raton vs mouse / ratonsito / escasa vs casa)
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
            if (category.IndexOf("Cartoon", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Comedia", StringComparison.OrdinalIgnoreCase) >= 0) return "#fb923c";
            if (category.IndexOf("Golpe", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Impacto", StringComparison.OrdinalIgnoreCase) >= 0) return "#f97316";
            if (category.IndexOf("Terror", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Drama", StringComparison.OrdinalIgnoreCase) >= 0) return "#7c3aed";
            if (category.IndexOf("Transicion", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("Whoosh", StringComparison.OrdinalIgnoreCase) >= 0) return "#58a6ff";
            if (category.IndexOf("Por Clasificar", StringComparison.OrdinalIgnoreCase) >= 0 || category.IndexOf("⚠️", StringComparison.OrdinalIgnoreCase) >= 0) return "#f59e0b";
            return "#39d353";
        }

        public static readonly string[] AllCategories = {
            "Todos los audios", "EFX / Cortos (<30s)", "Música / Largos (>=30s)", "⚠️ Por Clasificar",
            "Abucheos-Insultos", "Animal", "Campanas-Bongs-Alarmas", "Cartoon-Animados",
            "Censuras - Distorsiones - Explosiones", "Drama-Terror", "Efectos Frecuentes",
            "Frases", "Golpes-Puñetazos", "Internet-Ordenadores", "Objetos", "Transiciones", "Whoosh",
            "8Bit", "Energeticas", "Epicas-God", "Productividad", "Triste-Fail"
        };
    }
}

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

        // ─── Synonym Dictionary ─────────────────────────────────────────────────
        private static readonly Dictionary<string, string[]> SynonymMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // EXPLOSIÓN
            { "explosion",    new[] {"explosion","explosión","boom","blast","bang","detonate","detonation","kaboom","burst","bomba","estallido","bum","blowup","detonacion"} },
            { "explode",      new[] {"explode","explosion","boom","blast","bang","burst","kaboom","explosión","detonar","estalla"} },
            { "boom",         new[] {"boom","explosion","blast","thunder","bang","kaboom","estallido","explosión"} },
            { "blast",        new[] {"blast","explosion","boom","shock","bang","onda","explosión","estallido","wave"} },
            { "bomb",         new[] {"bomb","explosion","grenade","explosive","bomba","granada","explosivo","estallido","blast"} },
            { "bomba",        new[] {"bomba","bomb","explosion","blast","bang","estallido","explosión","granada"} },
            { "grenade",      new[] {"grenade","bomb","explosion","bang","kaboom","granada","bomba"} },
            { "granada",      new[] {"granada","grenade","bomb","explosion","bomba","bang","estallido"} },
            { "missile",      new[] {"missile","rocket","explosion","blast","launch","misil","cohete","lanzamiento"} },
            { "misil",        new[] {"misil","missile","rocket","cohete","explosion","blast","lanzamiento"} },
            { "nuke",         new[] {"nuke","nuclear","atomic","explosion","blast","bomb","bomba","explosión","atomica"} },
            { "dynamite",     new[] {"dynamite","explosion","bomb","blast","kaboom","dinamita","bomba","explosión"} },
            { "dinamita",     new[] {"dinamita","dynamite","bomb","explosion","bomba","explosión","blast"} },
            { "estallido",    new[] {"estallido","explosion","boom","blast","bang","explosión","kaboom","burst"} },
            { "detonacion",   new[] {"detonacion","detonación","explosion","detonate","boom","blast","explosión"} },
            { "kaboom",       new[] {"kaboom","explosion","boom","blast","bang","estallido","explosión"} },

            // IMPACTO
            { "impact",       new[] {"impact","hit","strike","crash","smash","bang","blow","golpe","impacto","choque","colision"} },
            { "impacto",      new[] {"impacto","impact","hit","strike","golpe","choque","crash","smash"} },
            { "hit",          new[] {"hit","impact","punch","strike","blow","slap","golpe","golpear","impacto"} },
            { "punch",        new[] {"punch","hit","impact","slap","bop","strike","puñetazo","golpe"} },
            { "crash",        new[] {"crash","impact","collision","smash","bang","accident","choque","colision","accidente"} },
            { "slam",         new[] {"slam","hit","crash","bang","door","portazo","golpe","smash","thud"} },
            { "thud",         new[] {"thud","impact","fall","drop","hit","golpe","caida","bump","dum"} },
            { "smash",        new[] {"smash","break","crash","hit","destroy","slam","golpe","romper","destroza"} },
            { "crack",        new[] {"crack","break","snap","split","crujido","romper","chasquido"} },
            { "fall",         new[] {"fall","drop","impact","landing","thud","caida","aterrizaje","caer","falling"} },
            { "falling",      new[] {"falling","fall","drop","impact","caida","caer","resbalon"} },
            { "drop",         new[] {"drop","fall","impact","caida","golpe","thud","crash"} },
            { "golpe",        new[] {"golpe","hit","impact","punch","strike","blow","thud","crash","impacto","bash"} },
            { "choque",       new[] {"choque","crash","impact","collision","colision","golpe","bang"} },
            { "colision",     new[] {"colision","crash","impact","choque","collision"} },
            { "caida",        new[] {"caida","fall","drop","thud","landing","impact","golpe"} },
            { "hueso",        new[] {"hueso","bone","break","crack","fracture","romper","crujido"} },
            { "bone",         new[] {"bone","hueso","break","crack","fracture","romper","crujido"} },

            // NATURALEZA
            { "nature",       new[] {"nature","natural","outdoor","environment","forest","wind","rain","naturaleza","ambiente","bosque","viento","lluvia"} },
            { "naturaleza",   new[] {"naturaleza","nature","natural","outdoor","environment","bosque","viento","lluvia","selva"} },
            { "forest",       new[] {"forest","tree","woods","jungle","nature","ambience","bosque","arbol","selva","naturaleza"} },
            { "bosque",       new[] {"bosque","forest","tree","woods","jungle","arbol","selva","naturaleza","nature"} },
            { "wind",         new[] {"wind","breeze","gust","storm","howl","blow","viento","brisa","rafaga","tormenta"} },
            { "viento",       new[] {"viento","wind","breeze","gust","brisa","rafaga","tormenta","storm"} },
            { "rain",         new[] {"rain","drizzle","shower","storm","water","lluvia","chubasco","aguacero","agua","tormenta"} },
            { "lluvia",       new[] {"lluvia","rain","drizzle","shower","agua","chubasco","tormenta","storm"} },
            { "thunder",      new[] {"thunder","storm","lightning","roll","rumble","trueno","tormenta","rayo","relampago"} },
            { "trueno",       new[] {"trueno","thunder","storm","lightning","rayo","tormenta"} },
            { "storm",        new[] {"storm","thunder","wind","rain","tormenta","trueno","viento","lluvia","tempest"} },
            { "tormenta",     new[] {"tormenta","storm","thunder","wind","rain","trueno","viento","lluvia"} },
            { "earthquake",   new[] {"earthquake","terremoto","ground","rumble","shake","destruction","ruptura","tierra","temblor","seismo"} },
            { "terremoto",    new[] {"terremoto","earthquake","ground","rumble","shake","tierra","temblor","seismo","destruction","ruptura"} },
            { "temblor",      new[] {"temblor","earthquake","terremoto","shake","rumble","tierra","seismo"} },

            // PASOS
            { "footstep",     new[] {"footstep","step","walk","run","feet","foot","paso","caminar","correr","pie","steps"} },
            { "paso",         new[] {"paso","footstep","step","walk","feet","caminar","pie","steps"} },
            { "walk",         new[] {"walk","footstep","step","stroll","caminar","paso","andar","walking"} },
            { "caminar",      new[] {"caminar","walk","footstep","step","stroll","paso","andar","walking"} },
            { "run",          new[] {"run","running","sprint","footstep","correr","paso","carrera","sprint"} },
            { "correr",       new[] {"correr","run","running","sprint","paso","carrera","footstep"} },

            // VEHÍCULOS
            { "car",          new[] {"car","vehicle","engine","motor","automobile","vroom","drive","coche","carro","vehiculo","auto"} },
            { "coche",        new[] {"coche","car","vehicle","engine","motor","auto","vehiculo","carro","drive"} },
            { "engine",       new[] {"engine","motor","car","machine","rev","vroom","maquina","coche","vehicle"} },

            // ARMAS
            { "gun",          new[] {"gun","shoot","fire","weapon","pistol","rifle","shot","arma","disparo","pistola","fusil","bala"} },
            { "arma",         new[] {"arma","gun","weapon","shoot","fire","pistola","fusil","disparo","bala"} },
            { "shoot",        new[] {"shoot","shot","gun","fire","weapon","disparar","disparo","arma","bala"} },
            { "disparo",      new[] {"disparo","shoot","shot","gun","fire","arma","bala","pistola","fusil","tiro"} },
            { "sword",        new[] {"sword","blade","slash","slice","clang","espada","hoja","tajo","corte","metal"} },

            // INTERFAZ UI / MEMES
            { "click",        new[] {"click","button","select","press","mouse","clic","boton","seleccionar","pulsar"} },
            { "clic",         new[] {"clic","click","button","select","press","boton","seleccionar","pulsar"} },
            { "boton",        new[] {"boton","button","click","select","clic","respuesta","ui"} },
            { "button",       new[] {"button","boton","click","select","clic","ui"} },
            { "beep",         new[] {"beep","tone","alert","sound","signal","pitido","tono","alerta","señal"} },
            { "notification", new[] {"notification","alert","ding","ping","message","notificacion","alerta","mensaje"} },
            { "error",        new[] {"error","fail","wrong","buzz","incorrect","fallo","equivocacion","incorrecto","respuesta"} },
            { "incorrecto",   new[] {"incorrecto","error","fail","wrong","fallo","respuesta","boton"} },
            { "success",      new[] {"success","confirm","win","complete","correct","exito","confirmar","ganar","completar"} },
            { "whoosh",       new[] {"whoosh","swipe","fast","speed","swoosh","transition","rapido","veloz","transicion","silbido"} },
            { "swoosh",       new[] {"swoosh","whoosh","swipe","fast","transition","silbido","rapido","transicion"} },
            { "pop",          new[] {"pop","bubble","click","burst","ui","burbuja","clic","estallido","interfaz"} },
            { "ding",         new[] {"ding","bell","notification","alert","success","campanilla","notificacion","alerta"} },
            { "alert",        new[] {"alert","warning","danger","notification","alerta","advertencia","peligro"} },
            { "typing",       new[] {"typing","keyboard","click","office","escritura","teclado","clic","iphone","persona"} },
            { "teclado",      new[] {"teclado","keyboard","typing","click","escritura","iphone","persona"} },
            { "iphone",       new[] {"iphone","apple","keyboard","teclado","typing","sound","sonido"} },

            // VOZ / MEMES
            { "voice",        new[] {"voice","human","speak","talk","vocal","voz","humano","hablar","habla"} },
            { "voz",          new[] {"voz","voice","human","speak","talk","humano","hablar","habla"} },
            { "laugh",        new[] {"laugh","giggle","chuckle","comedy","funny","risa","risita","carcajada","comedia"} },
            { "risa",         new[] {"risa","laugh","giggle","chuckle","comedy","risita","carcajada","comedia"} },
            { "scream",       new[] {"scream","yell","shout","cry","horror","grito","gritar","chillar","llorar","terror"} },
            { "grito",        new[] {"grito","scream","yell","shout","cry","gritar","chillar","terror","horror"} },
            { "meme",         new[] {"meme","comedy","funny","funny_sound","comedia","humor","gracioso","frase"} },
            { "frase",        new[] {"frase","voice","speech","voz","habla","humano","meme"} },
            { "ayuda",        new[] {"ayuda","help","voice","scream","grito","voz"} },
            { "miedo",        new[] {"miedo","fear","horror","scary","paralizado","grito","terror"} },
            { "sniff",        new[] {"sniff","esnifar","nose","nariz","voice","humano"} },
            { "esnifar",      new[] {"esnifar","sniff","nariz","voice","humano"} },

            // COMEDIA / ANIME
            { "comedy",       new[] {"comedy","funny","cartoon","silly","humorous","comedia","gracioso","dibujos","tonto","humor","meme"} },
            { "comedia",      new[] {"comedia","comedy","funny","cartoon","gracioso","dibujos","tonto","humor","meme"} },
            { "kirby",        new[] {"kirby","cartoon","funny","game","nintendo","comedia","meme"} },
            { "goku",         new[] {"goku","dragonball","anime","drama","meme","comedy"} },
            { "doraemon",     new[] {"doraemon","anime","cartoon","funny","dibujos","comedia"} },
            { "cartoon",      new[] {"cartoon","funny","comedy","animated","silly","dibujos","gracioso","comedia"} },

            // METAL / HERRAMIENTAS
            { "metal",        new[] {"metal","clang","clank","ring","steel","hit","iron","golpe","acero","hierro"} },
            { "yunke",        new[] {"yunke","anvil","yunque","metal","hit","golpe","acero"} },
            { "yunque",       new[] {"yunque","yunke","anvil","metal","hit","golpe","acero"} },
            { "wood",         new[] {"wood","plank","floor","tree","creak","knock","madera","tabla","suelo","arbol","chainsaw"} },
            { "madera",       new[] {"madera","wood","plank","floor","tree","creak","tabla","suelo","chainsaw"} },
            { "chainsaw",     new[] {"chainsaw","motosierra","wood","madera","cutting","cortar","herramienta"} },
            { "motosierra",   new[] {"motosierra","chainsaw","wood","madera","cortar"} },

            // MÚSICA
            { "music",        new[] {"music","musical","melody","tune","song","musica","melodia","cancion","ritmo"} },
            { "musica",       new[] {"musica","music","musical","melody","melodia","cancion","ritmo"} },
        };

        private static readonly Dictionary<string, string> CategoryMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"explosion","Explosión"},{"explode","Explosión"},{"boom","Explosión"},{"blast","Explosión"},{"bomb","Explosión"},{"bomba","Explosión"},{"grenade","Explosión"},

            {"impact","Impacto"},{"impacto","Impacto"},{"hit","Impacto"},{"punch","Impacto"},{"crash","Impacto"},{"slam","Impacto"},{"thud","Impacto"},{"smash","Impacto"},{"golpe","Impacto"},{"falling","Impacto"},{"caida","Impacto"},{"bone","Impacto"},{"hueso","Impacto"},

            {"nature","Naturaleza"},{"naturaleza","Naturaleza"},{"forest","Naturaleza"},{"wind","Naturaleza"},{"viento","Naturaleza"},{"rain","Naturaleza"},{"lluvia","Naturaleza"},{"thunder","Naturaleza"},{"trueno","Naturaleza"},{"storm","Naturaleza"},{"tormenta","Naturaleza"},{"earthquake","Naturaleza"},{"terremoto","Naturaleza"},

            {"footstep","Pasos"},{"paso","Pasos"},{"walk","Pasos"},{"caminar","Pasos"},{"run","Pasos"},{"correr","Pasos"},

            {"car","Vehículo"},{"coche","Vehículo"},{"engine","Vehículo"},{"motor","Vehículo"},{"truck","Vehículo"},{"plane","Vehículo"},{"avion","Vehículo"},

            {"gun","Arma"},{"arma","Arma"},{"shoot","Arma"},{"disparo","Arma"},{"sword","Arma"},{"espada","Arma"},

            {"click","Interfaz"},{"clic","Interfaz"},{"beep","Interfaz"},{"notification","Interfaz"},{"notificacion","Interfaz"},{"error","Interfaz"},{"incorrecto","Interfaz"},{"boton","Interfaz"},{"button","Interfaz"},{"whoosh","Interfaz"},{"swoosh","Interfaz"},{"ding","Interfaz"},{"typing","Interfaz"},{"teclado","Interfaz"},{"iphone","Interfaz"},

            {"voice","Voz"},{"voz","Voz"},{"laugh","Voz"},{"risa","Voz"},{"scream","Voz"},{"grito","Voz"},{"frase","Voz"},{"ayuda","Voz"},{"miedo","Voz"},{"sniff","Voz"},{"esnifar","Voz"},

            {"comedy","Comedia"},{"comedia","Comedia"},{"cartoon","Comedia"},{"kirby","Comedia"},{"goku","Comedia"},{"doraemon","Comedia"},{"meme","Comedia"},

            {"yunke","Metal"},{"yunque","Metal"},{"metal","Metal"},{"acero","Metal"},

            {"wood","Madera"},{"madera","Madera"},{"chainsaw","Madera"},{"motosierra","Madera"},

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

        public static string CleanDisplayName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "Sonido";
            string name = Path.GetFileNameWithoutExtension(rawName);

            // Clean YouTube video IDs like [WXOXRR4vmwo]
            name = Regex.Replace(name, @"\[[A-Za-z0-9_-]{8,}\]", "");
            // Clean common filler words
            name = Regex.Replace(name, @"(?i)(EFECTO DE SONIDO|SOUND EFFECT|SIN COPYRIGHT|NO COPYRIGHT)", "");
            // Clean numbers with separators
            name = Regex.Replace(name, @"[\-_]+", " ");
            name = Regex.Replace(name, @"\s+", " ").Trim();

            if (string.IsNullOrEmpty(name))
                name = Path.GetFileNameWithoutExtension(rawName);

            return name;
        }

        public static string[] TokenizeFilename(string filename)
        {
            string clean = CleanDisplayName(filename);
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
            string filename = Path.GetFileName(filePath);
            string[] tokens = TokenizeFilename(filename);

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

            string category = "General";
            int maxCount = 0;
            foreach (var kvp in categoryCounts)
                if (kvp.Value > maxCount) { maxCount = kvp.Value; category = kvp.Key; }

            string displayName = CleanDisplayName(filename);

            var tagList = new List<string>(tagSet);
            if (tagList.Count > 25) tagList = tagList.GetRange(0, 25);

            long fileSize = 0;
            try { fileSize = new FileInfo(filePath).Length; } catch { }

            return new SfxFile
            {
                FilePath = filePath, FileName = filename, DisplayName = displayName,
                Tags = tagList.ToArray(), Category = category, FileSizeBytes = fileSize
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
            switch (category)
            {
                case "Explosión":       return "#ef4444";
                case "Impacto":         return "#f97316";
                case "Naturaleza":      return "#22c55e";
                case "Pasos":           return "#84cc16";
                case "Vehículo":        return "#3b82f6";
                case "Arma":            return "#ec4899";
                case "Interfaz":        return "#58a6ff";
                case "Voz":             return "#f59e0b";
                case "Ambiente":        return "#64748b";
                case "Animal":          return "#a3855d";
                case "Agua":            return "#06b6d4";
                case "Fuego":           return "#f97316";
                case "Eléctrico":       return "#facc15";
                case "Vidrio":          return "#818cf8";
                case "Madera":          return "#92400e";
                case "Metal":           return "#94a3b8";
                case "Terror":          return "#7c3aed";
                case "Ciencia Ficción": return "#00d9a0";
                case "Comedia":         return "#fb923c";
                case "Música":          return "#bc8cff";
                default:                return "#969696";
            }
        }

        public static readonly string[] AllCategories = {
            "Explosión","Impacto","Naturaleza","Pasos","Vehículo","Arma",
            "Interfaz","Voz","Ambiente","Animal","Agua","Fuego","Eléctrico",
            "Vidrio","Madera","Metal","Terror","Ciencia Ficción","Comedia","Música","General"
        };
    }
}

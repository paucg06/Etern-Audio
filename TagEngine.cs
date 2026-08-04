using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SfxVault
{
    /// <summary>
    /// Auto-tagger and synonym engine for SFX files.
    /// Contains ~300 synonym entries in English + Spanish covering 21 categories.
    /// </summary>
    public static class TagEngine
    {
        public static readonly string[] AudioExtensions = { ".wav", ".mp3", ".aac", ".ogg", ".flac", ".m4a", ".wma", ".opus" };

        public static bool IsAudioFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            foreach (var ae in AudioExtensions)
                if (ae == ext) return true;
            return false;
        }

        // ─── Synonym Dictionary ─────────────────────────────────────────────────
        // Key: normalized word  →  Value: all related terms (EN + ES)
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
            { "crash",        new[] {"crash","impact","collision","smash","bang","accident","choque","colision","accidente","smash"} },
            { "slam",         new[] {"slam","hit","crash","bang","door","portazo","golpe","smash","thud"} },
            { "thud",         new[] {"thud","impact","fall","drop","hit","golpe","caida","bump","dum"} },
            { "smash",        new[] {"smash","break","crash","hit","destroy","slam","golpe","romper","destroza"} },
            { "crack",        new[] {"crack","break","snap","split","crujido","romper","chasquido"} },
            { "fall",         new[] {"fall","drop","impact","landing","thud","caida","aterrizaje","caer"} },
            { "drop",         new[] {"drop","fall","impact","caida","golpe","thud","crash"} },
            { "golpe",        new[] {"golpe","hit","impact","punch","strike","blow","thud","crash","impacto","bash"} },
            { "choque",       new[] {"choque","crash","impact","collision","colision","golpe","bang"} },
            { "colision",     new[] {"colision","crash","impact","choque","collision"} },
            { "caida",        new[] {"caida","fall","drop","thud","landing","impact","golpe"} },

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
            { "lightning",    new[] {"lightning","thunder","storm","electric","spark","rayo","relampago","trueno","tormenta"} },
            { "rayo",         new[] {"rayo","lightning","thunder","electric","spark","relampago","trueno","tormenta"} },
            { "bird",         new[] {"bird","chirp","tweet","song","fly","pajaro","pio","canto","ave","gorjeo","birdsong"} },
            { "pajaro",       new[] {"pajaro","bird","chirp","tweet","ave","gorjeo","canto","song"} },
            { "chirp",        new[] {"chirp","bird","tweet","song","pio","gorjeo","pajaro","insect","cricket"} },
            { "ocean",        new[] {"ocean","sea","wave","beach","water","splash","oceano","mar","ola","playa","agua"} },
            { "mar",          new[] {"mar","ocean","sea","wave","beach","oceano","ola","playa","agua","water"} },
            { "wave",         new[] {"wave","ocean","sea","water","splash","ola","oceano","mar","agua"} },
            { "ola",          new[] {"ola","wave","ocean","sea","water","splash","oceano","mar","agua"} },
            { "river",        new[] {"river","stream","water","flow","current","rio","arroyo","agua","corriente"} },
            { "rio",          new[] {"rio","river","stream","water","flow","arroyo","agua","corriente"} },
            { "leaves",       new[] {"leaves","rustle","forest","wind","leaf","hojas","susurro","bosque","viento","hoja"} },
            { "hojas",        new[] {"hojas","leaves","rustle","forest","wind","hoja","susurro","bosque","viento"} },
            { "cricket",      new[] {"cricket","insect","night","outdoor","nature","grillo","insecto","noche","naturaleza"} },
            { "grillo",       new[] {"grillo","cricket","insect","night","outdoor","nature","insecto","noche"} },
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
            { "jump",         new[] {"jump","hop","land","spring","leap","saltar","salto","brinco","aterrizaje"} },
            { "saltar",       new[] {"saltar","jump","hop","land","leap","salto","brinco","aterrizaje"} },
            { "crunch",       new[] {"crunch","gravel","leaves","snow","footstep","crujido","grava","hojas","nieve","paso"} },
            { "gravel",       new[] {"gravel","crunch","footstep","outdoor","grava","crujido","paso"} },
            { "sneak",        new[] {"sneak","soft","quiet","footstep","stealth","sigilo","suave","paso"} },

            // VEHÍCULOS
            { "car",          new[] {"car","vehicle","engine","motor","automobile","vroom","drive","coche","carro","vehiculo","auto"} },
            { "coche",        new[] {"coche","car","vehicle","engine","motor","auto","vehiculo","carro","drive"} },
            { "engine",       new[] {"engine","motor","car","machine","rev","vroom","maquina","coche","vehicle"} },
            { "motor",        new[] {"motor","engine","car","machine","rev","vroom","maquina","coche","vehicle"} },
            { "truck",        new[] {"truck","vehicle","engine","heavy","camion","vehiculo","motor"} },
            { "camion",       new[] {"camion","truck","vehicle","engine","vehiculo","motor","pesado"} },
            { "motorcycle",   new[] {"motorcycle","bike","engine","rev","vroom","moto","motocicleta","motor"} },
            { "moto",         new[] {"moto","motorcycle","bike","engine","rev","vroom","motocicleta","motor"} },
            { "helicopter",   new[] {"helicopter","rotor","blade","chopper","air","helicoptero","heli","rotor"} },
            { "helicoptero",  new[] {"helicoptero","helicopter","rotor","blade","chopper","heli"} },
            { "plane",        new[] {"plane","aircraft","engine","jet","fly","air","avion","aeronave","vuelo"} },
            { "avion",        new[] {"avion","plane","aircraft","engine","jet","vuelo","aire"} },
            { "train",        new[] {"train","rail","track","steam","tren","via","ferrocarril","vapor"} },
            { "tren",         new[] {"tren","train","rail","track","via","ferrocarril"} },
            { "boat",         new[] {"boat","ship","water","engine","horn","barco","buque","agua","vapor"} },
            { "barco",        new[] {"barco","boat","ship","water","engine","buque","agua"} },
            { "rocket",       new[] {"rocket","space","launch","blast","engine","cohete","espacio","lanzamiento","misil"} },
            { "cohete",       new[] {"cohete","rocket","space","launch","blast","espacio","lanzamiento"} },
            { "vroom",        new[] {"vroom","car","engine","fast","speed","rev","coche","motor","rapido"} },
            { "siren",        new[] {"siren","police","emergency","alert","warning","sirena","policia","emergencia","alerta"} },
            { "sirena",       new[] {"sirena","siren","police","emergency","alert","policia","emergencia","alerta"} },

            // ARMAS
            { "gun",          new[] {"gun","shoot","fire","weapon","pistol","rifle","shot","arma","disparo","pistola","fusil","bala"} },
            { "arma",         new[] {"arma","gun","weapon","shoot","fire","pistola","fusil","disparo","bala"} },
            { "shoot",        new[] {"shoot","shot","gun","fire","weapon","disparar","disparo","arma","bala"} },
            { "disparo",      new[] {"disparo","shoot","shot","gun","fire","arma","bala","pistola","fusil","tiro"} },
            { "shot",         new[] {"shot","shoot","gun","bullet","bang","disparo","bala","tiro","arma"} },
            { "bullet",       new[] {"bullet","shot","gun","impact","bala","disparo","tiro","impacto"} },
            { "bala",         new[] {"bala","bullet","shot","gun","impacto","disparo","tiro","arma"} },
            { "rifle",        new[] {"rifle","gun","shoot","military","sniper","fusil","arma","disparo"} },
            { "shotgun",      new[] {"shotgun","gun","blast","shoot","escopeta","arma","disparo"} },
            { "escopeta",     new[] {"escopeta","shotgun","gun","blast","arma","disparo"} },
            { "pistol",       new[] {"pistol","gun","shoot","handgun","pistola","arma","disparo"} },
            { "pistola",      new[] {"pistola","pistol","gun","handgun","arma","disparo"} },
            { "reload",       new[] {"reload","gun","click","ammo","weapon","recargar","arma","clic","recarga"} },
            { "recargar",     new[] {"recargar","reload","gun","weapon","arma","recarga"} },
            { "sword",        new[] {"sword","blade","slash","slice","clang","espada","hoja","tajo","corte","metal"} },
            { "espada",       new[] {"espada","sword","blade","slash","slice","clang","hoja","tajo","corte","metal"} },
            { "knife",        new[] {"knife","blade","stab","cut","slash","cuchillo","hoja","punalada","corte"} },
            { "cuchillo",     new[] {"cuchillo","knife","blade","stab","cut","hoja","punalada","corte"} },
            { "arrow",        new[] {"arrow","bow","shoot","shot","whoosh","flecha","arco","disparo"} },
            { "flecha",       new[] {"flecha","arrow","bow","shoot","arco","disparo"} },
            { "laser",        new[] {"laser","beam","zap","energy","scifi","laser","rayo","energia","futurista"} },
            { "blaster",      new[] {"blaster","laser","gun","scifi","shoot","arma","rayo","disparo"} },
            { "machinegun",   new[] {"machinegun","gun","shoot","rapid","military","ametralladora","arma","disparo"} },
            { "ametralladora",new[] {"ametralladora","machinegun","gun","shoot","arma","disparo"} },
            { "cannon",       new[] {"cannon","gun","blast","explosion","boom","canon","arma","explosion"} },
            { "canon",        new[] {"canon","cannon","gun","blast","explosion","arma","explosion"} },

            // INTERFAZ UI
            { "click",        new[] {"click","button","select","press","mouse","clic","boton","seleccionar","pulsar"} },
            { "clic",         new[] {"clic","click","button","select","press","boton","seleccionar","pulsar"} },
            { "beep",         new[] {"beep","tone","alert","sound","signal","pitido","tono","alerta","señal"} },
            { "notification", new[] {"notification","alert","ding","ping","message","notificacion","alerta","mensaje"} },
            { "notificacion", new[] {"notificacion","notification","alert","ding","ping","alerta","mensaje"} },
            { "error",        new[] {"error","fail","wrong","buzz","incorrect","fallo","equivocacion","incorrecto"} },
            { "success",      new[] {"success","confirm","win","complete","correct","exito","confirmar","ganar","completar"} },
            { "exito",        new[] {"exito","success","confirm","win","complete","correcto"} },
            { "whoosh",       new[] {"whoosh","swipe","fast","speed","swoosh","transition","rapido","veloz","transicion","silbido"} },
            { "swoosh",       new[] {"swoosh","whoosh","swipe","fast","transition","silbido","rapido","transicion"} },
            { "pop",          new[] {"pop","bubble","click","burst","ui","burbuja","clic","estallido","interfaz"} },
            { "ding",         new[] {"ding","bell","notification","alert","success","campanilla","notificacion","alerta"} },
            { "alert",        new[] {"alert","warning","danger","notification","alerta","advertencia","peligro"} },
            { "alerta",       new[] {"alerta","alert","warning","danger","notification","advertencia","peligro"} },
            { "typing",       new[] {"typing","keyboard","click","office","escritura","teclado","clic","oficina"} },
            { "teclado",      new[] {"teclado","keyboard","typing","click","escritura","oficina"} },
            { "coin",         new[] {"coin","money","collect","item","moneda","dinero","recoger"} },
            { "moneda",       new[] {"moneda","coin","money","collect","dinero","recoger"} },
            { "swipe",        new[] {"swipe","slide","whoosh","transition","deslizar","transicion"} },
            { "confirm",      new[] {"confirm","success","ok","accept","confirmar","aceptar"} },

            // VOZ
            { "voice",        new[] {"voice","human","speak","talk","vocal","voz","humano","hablar","habla"} },
            { "voz",          new[] {"voz","voice","human","speak","talk","humano","hablar","habla"} },
            { "laugh",        new[] {"laugh","giggle","chuckle","comedy","funny","risa","risita","carcajada","comedia"} },
            { "risa",         new[] {"risa","laugh","giggle","chuckle","comedy","risita","carcajada","comedia"} },
            { "scream",       new[] {"scream","yell","shout","cry","horror","grito","gritar","chillar","llorar","terror"} },
            { "grito",        new[] {"grito","scream","yell","shout","cry","gritar","chillar","terror","horror"} },
            { "crowd",        new[] {"crowd","people","cheer","applause","audience","multitud","gente","aplauso","audiencia"} },
            { "multitud",     new[] {"multitud","crowd","people","cheer","applause","gente","aplauso","audiencia"} },
            { "applause",     new[] {"applause","clap","cheer","crowd","aplauso","palmada","ovacion","multitud"} },
            { "aplauso",      new[] {"aplauso","applause","clap","cheer","crowd","palmada","ovacion","multitud"} },
            { "whisper",      new[] {"whisper","quiet","soft","voice","secret","susurro","silencioso","suave"} },
            { "susurro",      new[] {"susurro","whisper","quiet","soft","secret","voz","suave"} },
            { "grunt",        new[] {"grunt","effort","fight","strain","gruñido","esfuerzo","lucha"} },
            { "cough",        new[] {"cough","sick","throat","ill","tos","enfermo","garganta"} },
            { "tos",          new[] {"tos","cough","sick","throat","enfermo","garganta"} },
            { "breathing",    new[] {"breathing","breath","exhale","inhale","pant","respiracion","aliento"} },
            { "respiracion",  new[] {"respiracion","breathing","breath","exhale","inhale","aliento"} },
            { "cheer",        new[] {"cheer","crowd","applause","happy","victory","vitores","aplauso","multitud","victoria"} },
            { "sneeze",       new[] {"sneeze","achoo","sick","nose","estornudo","enfermo","nariz"} },
            { "estornudo",    new[] {"estornudo","sneeze","achoo","sick","enfermo"} },
            { "burp",         new[] {"burp","belch","funny","comedy","eructo","gracioso","comedia"} },
            { "eructo",       new[] {"eructo","burp","belch","funny","comedia","gracioso"} },

            // AMBIENTE
            { "ambient",      new[] {"ambient","atmosphere","background","ambience","loop","ambiental","atmosfera","fondo","ambiente","bucle"} },
            { "ambiental",    new[] {"ambiental","ambient","atmosphere","background","atmosfera","fondo","ambiente","bucle"} },
            { "atmosfera",    new[] {"atmosfera","ambient","atmosphere","ambience","ambiental","fondo"} },
            { "room",         new[] {"room","indoor","interior","space","hall","habitacion","espacio","interior","sala"} },
            { "habitacion",   new[] {"habitacion","room","indoor","interior","sala","espacio"} },
            { "cave",         new[] {"cave","echo","underground","dark","drip","cueva","eco","subterraneo","oscuro"} },
            { "cueva",        new[] {"cueva","cave","echo","underground","dark","eco","subterraneo","oscuro"} },
            { "space",        new[] {"space","cosmos","ambient","scifi","void","espacio","cosmos","vacio","futurista"} },
            { "espacio",      new[] {"espacio","space","cosmos","scifi","void","cosmos","vacio","futurista"} },
            { "underwater",   new[] {"underwater","bubble","water","splash","deep","bajo_agua","burbuja","agua","profundo"} },
            { "city",         new[] {"city","street","traffic","urban","crowd","ciudad","calle","trafico","urbano","multitud"} },
            { "ciudad",       new[] {"ciudad","city","street","traffic","urban","calle","trafico","urbano"} },
            { "calle",        new[] {"calle","street","city","traffic","urban","ciudad","trafico","urbano","outdoor"} },
            { "office",       new[] {"office","work","typing","keyboard","indoor","oficina","trabajo","teclado"} },
            { "oficina",      new[] {"oficina","office","work","typing","interior","trabajo","teclado"} },
            { "cave_drip",    new[] {"cave_drip","cave","drip","water","underground","cueva","goteo","agua","subterraneo"} },
            { "dungeon",      new[] {"dungeon","dark","cave","underground","drip","mazmorra","oscuro","cueva"} },
            { "mazmorra",     new[] {"mazmorra","dungeon","dark","cave","underground","oscuro","subterraneo"} },

            // ANIMALES
            { "dog",          new[] {"dog","bark","growl","pet","howl","panting","perro","ladrido","gruñido","mascota","aullido"} },
            { "perro",        new[] {"perro","dog","bark","growl","howl","ladrido","gruñido","mascota","aullido"} },
            { "bark",         new[] {"bark","dog","growl","loud","ladrido","perro","gruñido"} },
            { "ladrido",      new[] {"ladrido","bark","dog","growl","perro","gruñido"} },
            { "cat",          new[] {"cat","meow","purr","kitten","hiss","gato","miau","ronroneo","gatito"} },
            { "gato",         new[] {"gato","cat","meow","purr","kitten","miau","ronroneo","gatito"} },
            { "meow",         new[] {"meow","cat","kitten","miau","gato","gatito"} },
            { "miau",         new[] {"miau","meow","cat","kitten","gato","gatito"} },
            { "wolf",         new[] {"wolf","howl","growl","dog","wild","lobo","aullido","gruñido","salvaje"} },
            { "lobo",         new[] {"lobo","wolf","howl","growl","salvaje","aullido","gruñido"} },
            { "howl",         new[] {"howl","wolf","dog","wind","night","aullido","lobo","viento","noche"} },
            { "aullido",      new[] {"aullido","howl","wolf","dog","lobo","viento"} },
            { "lion",         new[] {"lion","roar","cat","wild","jungle","leon","rugido","felino","salvaje","jungla"} },
            { "leon",         new[] {"leon","lion","roar","cat","wild","rugido","felino","salvaje"} },
            { "roar",         new[] {"roar","lion","tiger","bear","wild","rugido","leon","tigre","oso","salvaje"} },
            { "rugido",       new[] {"rugido","roar","lion","tiger","bear","wild","leon","tigre","oso","salvaje"} },
            { "horse",        new[] {"horse","gallop","neigh","hooves","run","caballo","galope","relincho","cascos"} },
            { "caballo",      new[] {"caballo","horse","gallop","neigh","hooves","galope","relincho","cascos"} },
            { "snake",        new[] {"snake","hiss","reptile","slither","serpiente","silbido","reptil","deslizar"} },
            { "serpiente",    new[] {"serpiente","snake","hiss","reptile","silbido","reptil"} },
            { "bee",          new[] {"bee","buzz","fly","swarm","insect","abeja","zumbido","insecto","enjambre"} },
            { "abeja",        new[] {"abeja","bee","buzz","fly","insect","zumbido","insecto"} },
            { "cow",          new[] {"cow","moo","farm","animal","vaca","mugido","granja"} },
            { "vaca",         new[] {"vaca","cow","moo","farm","mugido","granja"} },
            { "frog",         new[] {"frog","croak","pond","nature","night","rana","croar","estanque","naturaleza"} },
            { "rana",         new[] {"rana","frog","croak","pond","estanque","naturaleza"} },
            { "bear",         new[] {"bear","roar","growl","wild","forest","oso","rugido","gruñido","salvaje","bosque"} },
            { "oso",          new[] {"oso","bear","roar","growl","salvaje","bosque","rugido","gruñido"} },

            // AGUA
            { "water",        new[] {"water","splash","drip","flow","stream","wave","liquid","agua","salpicadura","goteo","corriente","ola"} },
            { "agua",         new[] {"agua","water","splash","drip","flow","stream","salpicadura","goteo","corriente","ola"} },
            { "salpicadura",  new[] {"salpicadura","splash","water","drop","wave","agua","gota","ola"} },
            { "drip",         new[] {"drip","drop","water","cave","faucet","goteo","gota","agua","cueva","grifo"} },
            { "goteo",        new[] {"goteo","drip","drop","water","cave","gota","agua","grifo"} },
            { "waterfall",    new[] {"waterfall","cascade","river","rush","flow","cascada","rio","agua","corriente"} },
            { "cascada",      new[] {"cascada","waterfall","cascade","river","rush","flow","rio","agua"} },
            { "bubble",       new[] {"bubble","underwater","water","pop","boil","burbuja","bajo_agua","agua","hervir"} },
            { "burbuja",      new[] {"burbuja","bubble","underwater","water","pop","bajo_agua","hervir"} },
            { "swim",         new[] {"swim","water","splash","pool","nadar","agua","salpicadura","piscina"} },
            { "nadar",        new[] {"nadar","swim","water","splash","pool","agua","salpicadura","piscina"} },
            { "puddle",       new[] {"puddle","water","splash","rain","charco","agua","salpicadura","lluvia"} },
            { "charco",       new[] {"charco","puddle","water","splash","rain","agua","salpicadura"} },

            // FUEGO
            { "fire",         new[] {"fire","flame","burn","hot","smoke","fuego","llama","quemar","caliente","humo"} },
            { "fuego",        new[] {"fuego","fire","flame","burn","hot","smoke","llama","quemar","caliente","humo"} },
            { "flame",        new[] {"flame","fire","burn","light","flicker","llama","fuego","quemar","luz"} },
            { "llama",        new[] {"llama","flame","fire","burn","light","fuego","quemar","luz"} },
            { "smoke",        new[] {"smoke","fire","burn","dark","fog","humo","fuego","quemar","niebla"} },
            { "humo",         new[] {"humo","smoke","fire","burn","dark","fuego","quemar"} },
            { "crackle",      new[] {"crackle","fire","wood","campfire","burning","chisporroteo","fuego","madera","hoguera"} },
            { "campfire",     new[] {"campfire","fire","crackle","wood","outdoor","hoguera","fuego","chisporroteo","madera"} },
            { "hoguera",      new[] {"hoguera","campfire","fire","crackle","wood","fuego","chisporroteo","madera"} },
            { "torch",        new[] {"torch","fire","flame","light","dark","antorcha","fuego","llama","luz"} },
            { "antorcha",     new[] {"antorcha","torch","fire","flame","fuego","llama","luz"} },
            { "sizzle",       new[] {"sizzle","fry","cook","fire","hot","chisporrotear","freir","cocinar","fuego"} },

            // ELÉCTRICO
            { "electric",     new[] {"electric","electricity","spark","zap","buzz","lightning","power","electrico","electricidad","chispa","rayo","energia"} },
            { "electricidad", new[] {"electricidad","electric","electricity","spark","zap","buzz","electrico","chispa","rayo"} },
            { "spark",        new[] {"spark","electric","zap","flash","lightning","chispa","electrico","rayo","destello"} },
            { "chispa",       new[] {"chispa","spark","electric","zap","flash","electrico","destello"} },
            { "zap",          new[] {"zap","electric","spark","lightning","shock","rayo","chispa","electrico","descarga"} },
            { "buzz",         new[] {"buzz","electric","hum","vibrate","fly","zumbido","electrico","vibrar"} },
            { "zumbido",      new[] {"zumbido","buzz","electric","hum","vibrate","electrico","vibrar"} },
            { "static",       new[] {"static","electric","noise","radio","interference","estatico","electrico","ruido"} },
            { "power_up",     new[] {"power_up","energy","charge","start","boot","energizar","energia","cargar","encender"} },
            { "short_circuit",new[] {"short_circuit","electric","spark","fail","cortocircuito","electrico","chispa","fallo"} },

            // VIDRIO
            { "glass",        new[] {"glass","break","shatter","crystal","clink","vidrio","romper","cristal","tintinear"} },
            { "vidrio",       new[] {"vidrio","glass","break","shatter","crystal","romper","cristal"} },
            { "cristal",      new[] {"cristal","crystal","glass","break","shatter","vidrio","romper"} },
            { "shatter",      new[] {"shatter","break","glass","crash","trizas","romper","vidrio","cristal"} },
            { "clink",        new[] {"clink","glass","metal","toast","ring","tintinear","vidrio","metal"} },
            { "tintinear",    new[] {"tintinear","clink","glass","ring","bell","vidrio","campana"} },
            { "window",       new[] {"window","glass","break","ventana","vidrio","romper"} },
            { "ventana",      new[] {"ventana","window","glass","break","vidrio","romper"} },
            { "bottle",       new[] {"bottle","glass","liquid","break","botella","vidrio","liquido","romper"} },
            { "botella",      new[] {"botella","bottle","glass","liquid","vidrio","liquido"} },

            // MADERA
            { "wood",         new[] {"wood","plank","floor","tree","creak","knock","madera","tabla","suelo","arbol","crujido"} },
            { "madera",       new[] {"madera","wood","plank","floor","tree","creak","tabla","suelo","arbol","crujido"} },
            { "creak",        new[] {"creak","door","floor","wood","old","crujido","puerta","suelo","madera","viejo"} },
            { "crujido",      new[] {"crujido","creak","door","floor","wood","snap","madera","puerta","suelo"} },
            { "knock",        new[] {"knock","door","wood","hit","rap","golpe","puerta","madera","impacto"} },
            { "door",         new[] {"door","creak","knock","open","close","puerta","crujido","abrir","cerrar"} },
            { "puerta",       new[] {"puerta","door","creak","knock","open","close","crujido","abrir","cerrar"} },
            { "chop",         new[] {"chop","axe","wood","cut","split","hachazo","hacha","madera","corte"} },
            { "hacha",        new[] {"hacha","axe","chop","wood","cut","madera","corte","hachazo"} },
            { "branch",       new[] {"branch","tree","snap","break","wood","rama","arbol","crujido","romper","madera"} },
            { "rama",         new[] {"rama","branch","tree","snap","break","arbol","crujido","romper"} },

            // METAL
            { "metal",        new[] {"metal","clang","clank","ring","steel","hit","iron","golpe","acero","hierro"} },
            { "acero",        new[] {"acero","steel","metal","clang","sword","hierro","espada"} },
            { "clang",        new[] {"clang","metal","hit","impact","ring","bell","golpe","impacto","campana"} },
            { "clank",        new[] {"clank","metal","chain","machine","rattle","cadena","maquina","traqueteo"} },
            { "cadena",       new[] {"cadena","chain","metal","rattle","clank","traqueteo"} },
            { "chain",        new[] {"chain","metal","rattle","drag","cadena","traqueteo","arrastrar"} },
            { "scrape",       new[] {"scrape","metal","drag","scratch","rough","raspar","arrastrar","aranar"} },
            { "anvil",        new[] {"anvil","blacksmith","metal","hit","bang","yunque","herrero","golpe"} },
            { "armor",        new[] {"armor","metal","knight","clank","armadura","caballero","traqueteo"} },
            { "armadura",     new[] {"armadura","armor","metal","knight","clank","caballero"} },

            // TERROR
            { "horror",       new[] {"horror","scary","dark","fear","eerie","creepy","tension","terror","miedo","oscuro","inquietante"} },
            { "terror",       new[] {"terror","horror","scary","dark","fear","miedo","oscuro","inquietante","susto"} },
            { "creepy",       new[] {"creepy","scary","eerie","dark","unsettling","escalofriante","miedo","oscuro"} },
            { "escalofriante",new[] {"escalofriante","creepy","scary","eerie","dark","horror","miedo","oscuro"} },
            { "ghost",        new[] {"ghost","spirit","supernatural","eerie","haunt","fantasma","espiritu","sobrenatural"} },
            { "fantasma",     new[] {"fantasma","ghost","spirit","supernatural","eerie","espiritu","sobrenatural"} },
            { "monster",      new[] {"monster","roar","growl","creature","horror","monstruo","rugido","criatura","terror"} },
            { "monstruo",     new[] {"monstruo","monster","roar","growl","creature","rugido","criatura","terror"} },
            { "stinger",      new[] {"stinger","jump","scare","shock","horror","susto","salto","terror"} },
            { "susto",        new[] {"susto","scare","jump","stinger","horror","terror"} },
            { "heartbeat",    new[] {"heartbeat","heart","pulse","tension","horror","latido","corazon","pulso","tension"} },
            { "latido",       new[] {"latido","heartbeat","heart","pulse","tension","corazon","pulso"} },

            // CIENCIA FICCIÓN
            { "scifi",        new[] {"scifi","sci_fi","futuristic","space","tech","robot","digital","cyborg","futuro","espacio","tecnologia"} },
            { "futurista",    new[] {"futurista","futuristic","scifi","tech","robot","digital","espacio","tecnologia"} },
            { "robot",        new[] {"robot","machine","tech","digital","drone","servo","maquina","dron"} },
            { "drone",        new[] {"drone","fly","buzz","tech","robot","electric","dron","volar","zumbido","tecnologia"} },
            { "dron",         new[] {"dron","drone","fly","buzz","tech","robot","volar","zumbido"} },
            { "alien",        new[] {"alien","extraterrestrial","space","weird","scifi","extraterrestre","espacio","raro"} },
            { "extraterrestre",new[] {"extraterrestre","alien","extraterrestrial","space","scifi","espacio","raro"} },
            { "portal",       new[] {"portal","whoosh","energy","scifi","teleport","portal","energia","teletransporte"} },
            { "hologram",     new[] {"hologram","scifi","tech","display","holograma","futurista","tecnologia"} },
            { "holograma",    new[] {"holograma","hologram","scifi","tech","display","futurista"} },
            { "teletransporte",new[] {"teletransporte","teleport","portal","scifi","whoosh","futurista"} },

            // COMEDIA
            { "comedy",       new[] {"comedy","funny","cartoon","silly","humorous","comedia","gracioso","dibujos","tonto","humor"} },
            { "comedia",      new[] {"comedia","comedy","funny","cartoon","gracioso","dibujos","tonto","humor"} },
            { "boing",        new[] {"boing","spring","bounce","cartoon","silly","resorte","rebotar","dibujos"} },
            { "squeak",       new[] {"squeak","mouse","toy","funny","rubber","chillido","raton","juguete","gracioso"} },
            { "fart",         new[] {"fart","funny","comedy","body","pedo","gracioso","comedia","cuerpo"} },
            { "pedo",         new[] {"pedo","fart","funny","comedy","gracioso","comedia"} },
            { "slip",         new[] {"slip","fall","comedy","funny","resbalon","resbalar","caida","comedia"} },
            { "resbalon",     new[] {"resbalon","slip","fall","comedy","caida","comedia","gracioso"} },
            { "cartoon",      new[] {"cartoon","funny","comedy","animated","silly","dibujos","gracioso","comedia"} },

            // MÚSICA
            { "music",        new[] {"music","musical","melody","tune","song","musica","melodia","cancion","ritmo"} },
            { "musica",       new[] {"musica","music","musical","melody","melodia","cancion","ritmo"} },
            { "jingle",       new[] {"jingle","music","short","ad","catchy","musica","cancion","anuncio"} },
            { "stab",         new[] {"stab","music","hit","punch","accent","acorde","musica","golpe","acento"} },
            { "sting",        new[] {"sting","stab","music","accent","short","musica","acento","corto"} },
            { "melody",       new[] {"melody","music","tune","song","melodia","musica","cancion"} },
            { "melodia",      new[] {"melodia","melody","music","tune","song","musica","cancion"} },
            { "loop",         new[] {"loop","music","repeat","background","ambient","bucle","musica","repetir","fondo"} },
            { "bucle",        new[] {"bucle","loop","music","repeat","background","musica","repetir","fondo"} },
            { "beat",         new[] {"beat","drum","rhythm","music","bateria","ritmo","musica"} },
            { "drum",         new[] {"drum","beat","rhythm","hit","percussion","bateria","ritmo","golpe"} },
            { "bateria",      new[] {"bateria","drum","beat","rhythm","hit","percussion","ritmo","golpe"} },
            { "guitar",       new[] {"guitar","string","music","riff","guitarra","cuerda","musica"} },
            { "guitarra",     new[] {"guitarra","guitar","string","music","cuerda","musica"} },
            { "piano",        new[] {"piano","keys","music","classical","melodic","teclas","musica","clasico"} },
        };

        // ─── Category Map ────────────────────────────────────────────────────────
        private static readonly Dictionary<string, string> CategoryMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"explosion","Explosión"},{"explode","Explosión"},{"boom","Explosión"},{"blast","Explosión"},
            {"bomb","Explosión"},{"bomba","Explosión"},{"grenade","Explosión"},{"granada","Explosión"},
            {"missile","Explosión"},{"misil","Explosión"},{"nuke","Explosión"},{"dynamite","Explosión"},
            {"dinamita","Explosión"},{"estallido","Explosión"},{"detonacion","Explosión"},{"kaboom","Explosión"},

            {"impact","Impacto"},{"impacto","Impacto"},{"hit","Impacto"},{"punch","Impacto"},
            {"crash","Impacto"},{"slam","Impacto"},{"thud","Impacto"},{"smash","Impacto"},
            {"crack","Impacto"},{"fall","Impacto"},{"drop","Impacto"},{"golpe","Impacto"},
            {"choque","Impacto"},{"colision","Impacto"},{"caida","Impacto"},

            {"nature","Naturaleza"},{"naturaleza","Naturaleza"},{"forest","Naturaleza"},{"bosque","Naturaleza"},
            {"wind","Naturaleza"},{"viento","Naturaleza"},{"rain","Naturaleza"},{"lluvia","Naturaleza"},
            {"thunder","Naturaleza"},{"trueno","Naturaleza"},{"storm","Naturaleza"},{"tormenta","Naturaleza"},
            {"lightning","Naturaleza"},{"rayo","Naturaleza"},{"bird","Naturaleza"},{"pajaro","Naturaleza"},
            {"chirp","Naturaleza"},{"ocean","Naturaleza"},{"mar","Naturaleza"},{"river","Naturaleza"},
            {"rio","Naturaleza"},{"leaves","Naturaleza"},{"hojas","Naturaleza"},{"cricket","Naturaleza"},
            {"grillo","Naturaleza"},{"earthquake","Naturaleza"},{"terremoto","Naturaleza"},{"temblor","Naturaleza"},

            {"footstep","Pasos"},{"paso","Pasos"},{"walk","Pasos"},{"caminar","Pasos"},
            {"run","Pasos"},{"correr","Pasos"},{"jump","Pasos"},{"saltar","Pasos"},
            {"crunch","Pasos"},{"gravel","Pasos"},{"carpet","Pasos"},{"sneak","Pasos"},

            {"car","Vehículo"},{"coche","Vehículo"},{"engine","Vehículo"},{"motor","Vehículo"},
            {"truck","Vehículo"},{"camion","Vehículo"},{"motorcycle","Vehículo"},{"moto","Vehículo"},
            {"helicopter","Vehículo"},{"helicoptero","Vehículo"},{"plane","Vehículo"},{"avion","Vehículo"},
            {"train","Vehículo"},{"tren","Vehículo"},{"boat","Vehículo"},{"barco","Vehículo"},
            {"rocket","Vehículo"},{"cohete","Vehículo"},{"vroom","Vehículo"},{"siren","Vehículo"},
            {"sirena","Vehículo"},

            {"gun","Arma"},{"arma","Arma"},{"shoot","Arma"},{"disparo","Arma"},
            {"shot","Arma"},{"bullet","Arma"},{"bala","Arma"},{"rifle","Arma"},
            {"shotgun","Arma"},{"escopeta","Arma"},{"pistol","Arma"},{"pistola","Arma"},
            {"reload","Arma"},{"recargar","Arma"},{"sword","Arma"},{"espada","Arma"},
            {"knife","Arma"},{"cuchillo","Arma"},{"arrow","Arma"},{"flecha","Arma"},
            {"laser","Arma"},{"blaster","Arma"},{"machinegun","Arma"},{"ametralladora","Arma"},
            {"cannon","Arma"},{"canon","Arma"},

            {"click","Interfaz"},{"clic","Interfaz"},{"beep","Interfaz"},{"notification","Interfaz"},
            {"notificacion","Interfaz"},{"error","Interfaz"},{"success","Interfaz"},{"exito","Interfaz"},
            {"whoosh","Interfaz"},{"swoosh","Interfaz"},{"pop","Interfaz"},{"ding","Interfaz"},
            {"alert","Interfaz"},{"alerta","Interfaz"},{"typing","Interfaz"},{"teclado","Interfaz"},
            {"coin","Interfaz"},{"moneda","Interfaz"},{"swipe","Interfaz"},{"confirm","Interfaz"},

            {"voice","Voz"},{"voz","Voz"},{"laugh","Voz"},{"risa","Voz"},
            {"scream","Voz"},{"grito","Voz"},{"crowd","Voz"},{"multitud","Voz"},
            {"applause","Voz"},{"aplauso","Voz"},{"whisper","Voz"},{"susurro","Voz"},
            {"grunt","Voz"},{"cough","Voz"},{"tos","Voz"},{"breathing","Voz"},
            {"respiracion","Voz"},{"cheer","Voz"},{"sneeze","Voz"},{"estornudo","Voz"},
            {"burp","Voz"},{"eructo","Voz"},

            {"ambient","Ambiente"},{"ambiental","Ambiente"},{"atmosfera","Ambiente"},
            {"room","Ambiente"},{"habitacion","Ambiente"},{"cave","Ambiente"},{"cueva","Ambiente"},
            {"space","Ambiente"},{"espacio","Ambiente"},{"underwater","Ambiente"},
            {"city","Ambiente"},{"ciudad","Ambiente"},{"calle","Ambiente"},{"office","Ambiente"},
            {"oficina","Ambiente"},{"dungeon","Ambiente"},{"mazmorra","Ambiente"},

            {"dog","Animal"},{"perro","Animal"},{"bark","Animal"},{"ladrido","Animal"},
            {"cat","Animal"},{"gato","Animal"},{"meow","Animal"},{"miau","Animal"},
            {"wolf","Animal"},{"lobo","Animal"},{"howl","Animal"},{"aullido","Animal"},
            {"lion","Animal"},{"leon","Animal"},{"roar","Animal"},{"rugido","Animal"},
            {"horse","Animal"},{"caballo","Animal"},{"snake","Animal"},{"serpiente","Animal"},
            {"bee","Animal"},{"abeja","Animal"},{"cow","Animal"},{"vaca","Animal"},
            {"frog","Animal"},{"rana","Animal"},{"bear","Animal"},{"oso","Animal"},

            {"water","Agua"},{"agua","Agua"},{"salpicadura","Agua"},{"drip","Agua"},
            {"goteo","Agua"},{"waterfall","Agua"},{"cascada","Agua"},{"bubble","Agua"},
            {"burbuja","Agua"},{"swim","Agua"},{"nadar","Agua"},{"puddle","Agua"},{"charco","Agua"},

            {"fire","Fuego"},{"fuego","Fuego"},{"flame","Fuego"},{"llama","Fuego"},
            {"smoke","Fuego"},{"humo","Fuego"},{"crackle","Fuego"},{"campfire","Fuego"},
            {"hoguera","Fuego"},{"torch","Fuego"},{"antorcha","Fuego"},{"sizzle","Fuego"},

            {"electric","Eléctrico"},{"electricidad","Eléctrico"},{"spark","Eléctrico"},
            {"chispa","Eléctrico"},{"zap","Eléctrico"},{"buzz","Eléctrico"},{"zumbido","Eléctrico"},
            {"static","Eléctrico"},{"power_up","Eléctrico"},{"short_circuit","Eléctrico"},

            {"glass","Vidrio"},{"vidrio","Vidrio"},{"cristal","Vidrio"},{"shatter","Vidrio"},
            {"clink","Vidrio"},{"tintinear","Vidrio"},{"window","Vidrio"},{"ventana","Vidrio"},
            {"bottle","Vidrio"},{"botella","Vidrio"},

            {"wood","Madera"},{"madera","Madera"},{"creak","Madera"},{"crujido","Madera"},
            {"knock","Madera"},{"door","Madera"},{"puerta","Madera"},{"chop","Madera"},
            {"hacha","Madera"},{"branch","Madera"},{"rama","Madera"},

            {"metal","Metal"},{"acero","Metal"},{"clang","Metal"},{"clank","Metal"},
            {"cadena","Metal"},{"chain","Metal"},{"scrape","Metal"},{"anvil","Metal"},
            {"armor","Metal"},{"armadura","Metal"},

            {"horror","Terror"},{"terror","Terror"},{"creepy","Terror"},{"escalofriante","Terror"},
            {"ghost","Terror"},{"fantasma","Terror"},{"monster","Terror"},{"monstruo","Terror"},
            {"stinger","Terror"},{"susto","Terror"},{"heartbeat","Terror"},{"latido","Terror"},

            {"scifi","Ciencia Ficción"},{"futurista","Ciencia Ficción"},{"robot","Ciencia Ficción"},
            {"drone","Ciencia Ficción"},{"dron","Ciencia Ficción"},{"alien","Ciencia Ficción"},
            {"extraterrestre","Ciencia Ficción"},{"portal","Ciencia Ficción"},{"hologram","Ciencia Ficción"},
            {"holograma","Ciencia Ficción"},{"teletransporte","Ciencia Ficción"},

            {"comedy","Comedia"},{"comedia","Comedia"},{"boing","Comedia"},{"squeak","Comedia"},
            {"fart","Comedia"},{"pedo","Comedia"},{"slip","Comedia"},{"resbalon","Comedia"},
            {"cartoon","Comedia"},

            {"music","Música"},{"musica","Música"},{"jingle","Música"},{"stab","Música"},
            {"sting","Música"},{"melody","Música"},{"melodia","Música"},{"loop","Música"},
            {"bucle","Música"},{"beat","Música"},{"drum","Música"},{"bateria","Música"},
            {"guitar","Música"},{"guitarra","Música"},{"piano","Música"},
        };

        // ─── Public API ──────────────────────────────────────────────────────────

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
            string name = System.IO.Path.GetFileNameWithoutExtension(filename);
            var tokens = Regex.Split(name, @"[\s\-_\.]+");
            var result = new List<string>();
            foreach (var t in tokens)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                if (Regex.IsMatch(t, @"^\d+$")) continue;  // skip numbers
                if (t.Length < 2) continue;
                result.Add(NormalizeText(t));
            }
            return result.ToArray();
        }

        /// <summary>Auto-tags a file from its filename using the synonym dictionary.</summary>
        public static SfxFile AutoTag(string filePath)
        {
            string filename = System.IO.Path.GetFileName(filePath);
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

                // partial match
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
                    var cat = CategoryMap[token];
                    if (!categoryCounts.ContainsKey(cat)) categoryCounts[cat] = 0;
                    categoryCounts[cat]++;
                }
            }

            string category = "General";
            int maxCount = 0;
            foreach (var kvp in categoryCounts)
                if (kvp.Value > maxCount) { maxCount = kvp.Value; category = kvp.Key; }

            string displayName = System.IO.Path.GetFileNameWithoutExtension(filename)
                .Replace("_", " ").Replace("-", " ").Trim();
            displayName = Regex.Replace(displayName, @"^\d+\s*", "").Trim();
            if (string.IsNullOrEmpty(displayName))
                displayName = System.IO.Path.GetFileNameWithoutExtension(filename);

            var tagList = new List<string>(tagSet);
            if (tagList.Count > 25) tagList = tagList.GetRange(0, 25);

            long fileSize = 0;
            try { fileSize = new System.IO.FileInfo(filePath).Length; } catch { }

            return new SfxFile
            {
                FilePath = filePath, FileName = filename, DisplayName = displayName,
                Tags = tagList.ToArray(), Category = category, FileSizeBytes = fileSize
            };
        }

        /// <summary>Expands a search query with synonyms for fuzzy matching.</summary>
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
                case "Interfaz":        return "#8b5cf6";
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
                case "Música":          return "#e879f9";
                default:                return "#475569";
            }
        }

        public static readonly string[] AllCategories = {
            "Explosión","Impacto","Naturaleza","Pasos","Vehículo","Arma",
            "Interfaz","Voz","Ambiente","Animal","Agua","Fuego","Eléctrico",
            "Vidrio","Madera","Metal","Terror","Ciencia Ficción","Comedia","Música","General"
        };
    }
}

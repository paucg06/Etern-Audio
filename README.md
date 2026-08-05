# 🎵 Etern Audio

> **Gestor y Organizador Inteligente de Efectos de Sonido (SFX)**  
> *Parte del ecosistema Etern Apps (Etern Notes, Etern Studio, Etern Audio).*

![Etern Audio Banner](https://raw.githubusercontent.com/paucg06/Etern-Audio/master/preview.png)

---

## 🌟 Características Principales

- **🧠 Grafo Semántico Bilingüe (ES + EN)**: Buscador inteligente con equivalencias conceptuales (*gallo ↔ rooster ↔ chicken ↔ kikiriki*, *caca ↔ shit ↔ poop ↔ dirty*).
- **📂 Auto-Categorización Dinámica**: Detecta audios sueltos y crea/asigna subcarpetas temáticas (`Internet-Ordenadores`, `Animales`, `Anime-Manga`, `Naturaleza-Liquidos`, `Golpes-Puñetazos`).
- **🧹 Limpiador Regex de Nombres**: Renombra archivos automáticamente a español separando palabras con guion bajo (`Mala_Noticia_Mi_Gente.mp3`), eliminando marcas de descargadores YouTube y basura.
- **🖱️ Arrastrar a Carpetas (Drag & Drop)**: Arrastra cualquier audio de la lista a las carpetas del panel lateral para moverlo físicamente en disco.
- **📋 Copia nativa para DaVinci Resolve & Premiere**: Pega clips de audio directamente en la línea de tiempo de tu editor con `Ctrl+C` / `Ctrl+V`.
- **🎨 Diseño UI Spotify Dark**: Tema oscuro premium con tabla estirada al 100% de ancho, barra de reproducción inferior y badges de porcentaje de confianza.

---

## 🛠️ Estructura del Proyecto

```
Etern-Audio/
├── WpfMainWindow.cs       # Interfaz gráfica principal nativa WPF Spotify Dark
├── SearchEngine.cs        # Buscador vectorial semántico con filtrado de ruido
├── TagEngine.cs           # Grafo de conceptos bilingüe (ES / EN) y auto-etiquetado
├── FileOrganizer.cs       # Organizador físico de disco y limpiador Regex
├── Models.cs              # Modelos de datos y serialización JSON
├── compile.ps1            # Script de compilación nativa Windows (csc.exe)
└── .github/workflows/     # Workflows para exportación multiplataforma (Win / Mac / Linux)
```

---

## 🚀 Compilacion Local (Windows)

Abre una consola de PowerShell en la raíz del proyecto y ejecuta:

```powershell
powershell -ExecutionPolicy Bypass -File compile.ps1
```

Se generará el ejecutable `EternAudio.exe`.

---

## 🌐 Exportación Multiplataforma (GitHub Actions)

El proyecto incluye integración continua en `.github/workflows/build.yml` para compilar y empaquetar automáticamente binarios para **Windows**, **macOS** y **Linux** en cada push a la rama `master`.

---

## 📜 Licencia

Desarrollado para el ecosistema **Etern Studio**. Todos los derechos reservados.

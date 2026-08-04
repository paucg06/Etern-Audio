# Etern Audio 🔊

**Gestor de Efectos de Sonido Profesional y Ligero (Etern Studio Suite)**

Aplicación de escritorio nativa diseñada para organizar, etiquetar automáticamente y buscar en colecciones masivas de efectos de sonido (SFX) a máxima velocidad en local, ideal para flujos de trabajo en **DaVinci Resolve**, **Adobe Premiere Pro**, **Final Cut**, etc.

---

## ✨ Características Principales

- **🎨 Paleta e Identidad Identica a Etern-Notes**: Mismos colores dark premium (`#121212` fondo, `#1a1a1a` sidebar, `#212121` tarjetas/paneles, `#58a6ff` acento azul Etern).
- **📂 Auto-importación Automática**: Al iniciar, si detecta la carpeta `Efectos Sonido` o `Efectos de sonido` en el escritorio, la importa y clasifica automáticamente.
- **⚡ Búsqueda Difusa Instantánea (< 5ms)**: Índice invertido en memoria con diccionario bilingüe (Español / Inglés) de **+300 sinónimos** repartidos en 21 categorías (`Explosión`, `Impacto`, `Naturaleza`, `Pasos`, `Vehículo`, `Arma`, `Interfaz`, `Voz`, `Ambiente`, `Animal`, `Agua`, `Fuego`, `Eléctrico`, `Vidrio`, `Madera`, `Metal`, `Terror`, `Ciencia Ficción`, `Comedia`, `Música`, `General`).
- **🏷️ Auto-etiquetado y Limpiador de Títulos**: Limpia nombres complejos de YouTube (códigos `[WXOXRR4vmwo]`, guiones, números) y genera títulos limpios y etiquetas clave.
- **🖱️ Drag & Drop a Editores de Vídeo**: Arrastra directamente cualquier efecto de sonido desde la lista hacia la línea de tiempo de DaVinci Resolve, Premiere Pro o el explorador de archivos.
- **🎵 Reproductor Integrado**: Vista previa instantánea con volumen, progreso interactivo y atajos de teclado.

---

## 🛠️ Estructura del Proyecto

- `Models.cs` — Modelos de datos (`SfxFile`, `SfxLibrary`, `SfxDatabase`) y almacenamiento JSON.
- `TagEngine.cs` — Motor de etiquetado automático, limpiador de nombres y sinónimos (300+ entradas).
- `SearchEngine.cs` — Motor de búsqueda por índice invertido ultra rápido.
- `WpfMainWindow.cs` — Interfaz nativa de Windows (WPF sin XAML runtime).
- `MainWindow.axaml` / `MainWindow.axaml.cs` — Interfaz multiplataforma (Avalonia UI).
- `compile.ps1` — Script de compilación rápida para Windows (`EternAudio.exe`).

---

## 🚀 Compilación y Ejecución

```powershell
.\compile.ps1
.\EternAudio.exe
```

---

## 📄 Licencia
MIT License © 2026 Etern Studio

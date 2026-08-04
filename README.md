# SFX Vault 🔊

**Gestor de Efectos de Sonido Profesional y Ligero para Editores de Vídeo**

Aplicación de escritorio nativa diseñada para organizar, etiquetar automáticamente y buscar en colecciones masivas de efectos de sonido (SFX) a máxima velocidad en local, ideal para flujos de trabajo en **DaVinci Resolve**, **Adobe Premiere Pro**, **Final Cut**, etc.

---

## ✨ Características Principales

- **⚡ Búsqueda Difusa Instantánea (< 5ms)**: Índice invertido en memoria con diccionario bilingüe (Español / Inglés) de **+300 sinónimos** repartidos en 21 categorías (`Explosión`, `Impacto`, `Naturaleza`, `Pasos`, `Vehículo`, `Arma`, `Interfaz`, `Voz`, `Ambiente`, `Animal`, `Agua`, `Fuego`, `Eléctrico`, `Vidrio`, `Madera`, `Metal`, `Terror`, `Ciencia Ficción`, `Comedia`, `Música`, `General`).
- **🏷️ Auto-etiquetado Semántico Inteligente**: Escanea y asigna etiquetas y categorías automáticamente analizando el nombre de los archivos.
- **🖱️ Drag & Drop a Editores de Vídeo**: Arrastra directamente cualquier efecto de sonido desde la lista de SFX Vault hacia la línea de tiempo de DaVinci Resolve, Premiere Pro o el explorador de archivos.
- **🎵 Reproductor Integrado**: Reproducción instantánea de vista previa con control de volumen, barra de progreso interactiva, atajos de teclado y contadores de reproducciones.
- **⭐ Sistema de Favoritos**: Marca tus sonidos más usados para acceso rápido.
- **🎨 Diseño Moderno Oscuro**: Tema dark ultra elegante con acento esmeralda `#00d9a0`, barra superior auto-ocultable al pasar el ratón y panel lateral colapsable.

---

## 🛠️ Estructura del Proyecto

- `Models.cs` — Modelos de datos (`SfxFile`, `SfxLibrary`, `SfxDatabase`) y serialización JSON.
- `TagEngine.cs` — Motor de auto-etiquetado y diccionario de sinónimos (300+ entradas ES/EN).
- `SearchEngine.cs` — Motor de búsqueda por índice invertido ultra rápido.
- `WpfMainWindow.cs` — Interfaz nativa de Windows (WPF sin XAML runtime).
- `MainWindow.axaml` / `MainWindow.axaml.cs` — Interfaz multiplataforma (Avalonia UI para macOS y Linux).
- `compile.ps1` — Script de compilación rápida para Windows (`SfxVault.exe`).
- `.github/workflows/build.yml` — CI/CD automatizado para generar binarios de Windows, macOS y Linux.

---

## 🚀 Compilaciones y Ejecución

### Windows (Nativo)
```powershell
.\compile.ps1
.\SfxVault.exe
```

### Multiplataforma (Avalonia / .NET 8)
```bash
dotnet run
```

---

## 📄 Licencia
MIT License © 2026 Etern Studio

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.Platform.Storage;

namespace EternAudio
{
    public partial class MainWindow : Window
    {
        SfxDatabase db;
        SearchEngine searchEngine = new SearchEngine();
        List<SfxFile> filteredFiles = new List<SfxFile>();
        SfxFile selectedFile;
        string activeCategory = null;
        string activeLibraryId = null;
        bool showFavoritesOnly = false;
        bool isPlaying = false;

        StackPanel sidebarContent;
        TextBlock lblStatus;
        TextBlock lblResultCount;
        TextBlock lblNowPlaying;
        TextBlock lblCurrentTime;
        TextBlock lblTotalTime;
        Slider slProgress;
        Slider slVolume;
        Button btnPlayPause;
        ListBox lstFiles;
        TextBox txtSearch;
        TextBlock searchWatermark;

        Border menuBar;
        bool menuBarVisible = false;
        DispatcherTimer menuHideTimer;

        System.Diagnostics.Process audioProcess;
        DispatcherTimer progressTimer;
        DateTime playStartTime;
        double totalDuration = 0;
        DispatcherTimer searchDebounce;

        static readonly IBrush BG       = SolidColorBrush.Parse("#121212");
        static readonly IBrush SIDEBAR  = SolidColorBrush.Parse("#1a1a1a");
        static readonly IBrush CARD     = SolidColorBrush.Parse("#212121");
        static readonly IBrush CARDHOVER= SolidColorBrush.Parse("#2a2a2a");
        static readonly IBrush ACCENT   = SolidColorBrush.Parse("#58a6ff");
        static readonly IBrush TEXT     = SolidColorBrush.Parse("#ffffff");
        static readonly IBrush MUTED    = SolidColorBrush.Parse("#969696");
        static readonly IBrush DIM      = SolidColorBrush.Parse("#646464");
        static readonly IBrush BORDER   = SolidColorBrush.Parse("#303030");
        static readonly IBrush WARNING  = SolidColorBrush.Parse("#f0883e");

        public MainWindow()
        {
            InitializeComponent();
            db = Storage.Load();
            WireControls();
            SetupMenuHideTimer();
            SetupProgressTimer();
            RefreshSidebar();
            RebuildIndex();
            RefreshFileList();

            if (db.Libraries.Count > 0)
                Dispatcher.UIThread.InvokeAsync(() => RescanAll(), DispatcherPriority.Background);
        }

        void WireControls()
        {
            var titleBar    = this.FindControl<Grid>("TitleBarGrid");
            menuBar         = this.FindControl<Border>("MenuBar");
            sidebarContent  = this.FindControl<StackPanel>("SidebarContent");
            lblStatus       = this.FindControl<TextBlock>("LblStatus");
            lblResultCount  = this.FindControl<TextBlock>("LblResultCount");
            lblNowPlaying   = this.FindControl<TextBlock>("LblNowPlaying");
            lblCurrentTime  = this.FindControl<TextBlock>("LblCurrentTime");
            lblTotalTime    = this.FindControl<TextBlock>("LblTotalTime");
            slProgress      = this.FindControl<Slider>("SlProgress");
            slVolume        = this.FindControl<Slider>("SlVolume");
            btnPlayPause    = this.FindControl<Button>("BtnPlayPause");
            lstFiles        = this.FindControl<ListBox>("LstFiles");
            txtSearch       = this.FindControl<TextBox>("TxtSearch");
            searchWatermark = this.FindControl<TextBlock>("SearchWatermark");

            titleBar.PointerEntered += (_, _) => ShowMenuBar();
            titleBar.PointerExited  += (_, _) => menuHideTimer?.Start();
            titleBar.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };

            menuBar.PointerEntered += (_, _) => { menuHideTimer?.Stop(); ShowMenuBar(); };
            menuBar.PointerExited  += (_, _) => menuHideTimer?.Start();
            BuildMenuBar();

            this.FindControl<Button>("BtnClose").Click   += (_, _) => Close();
            this.FindControl<Button>("BtnMin").Click     += (_, _) => WindowState = WindowState.Minimized;
            this.FindControl<Button>("BtnMax").Click     += (_, _) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            this.FindControl<Button>("BtnHamburger").Click += (_, _) => ToggleSidebar();

            this.FindControl<Button>("BtnAddLib").Click += (_, _) => AddLibrary();

            txtSearch.TextChanged += (_, _) =>
            {
                searchWatermark.IsVisible = string.IsNullOrEmpty(txtSearch.Text);
                SearchDebounce();
            };
            this.FindControl<Button>("BtnClearSearch").Click    += (_, _) => { txtSearch.Text = ""; txtSearch.Focus(); };
            this.FindControl<Button>("BtnFavFilter").Click      += (_, _) => { showFavoritesOnly = !showFavoritesOnly; RefreshFileList(); RefreshSidebar(); };

            this.FindControl<Button>("BtnSortName").Click  += (_, _) => SortFiles("Nombre");
            this.FindControl<Button>("BtnSortCat").Click   += (_, _) => SortFiles("Categoría");
            this.FindControl<Button>("BtnSortSize").Click  += (_, _) => SortFiles("Tamaño");

            btnPlayPause.Click += (_, _) => TogglePlayPause();
            this.FindControl<Button>("BtnStop").Click  += (_, _) => StopPlayer();
            this.FindControl<Button>("BtnPrev").Click  += (_, _) => NavigateFile(-1);
            this.FindControl<Button>("BtnNext").Click  += (_, _) => NavigateFile(1);
            this.FindControl<Button>("BtnCopyFile").Click     += (_, _) => CopyToClipboard(selectedFile);
            this.FindControl<Button>("BtnOpenExplorer").Click += (_, _) => OpenInExplorer(selectedFile);

            lstFiles.SelectionChanged += (_, _) =>
            {
                if (lstFiles.SelectedItem is SfxFile f)
                {
                    selectedFile = f;
                    lblNowPlaying.Text = f.DisplayName + "  ·  " + f.Category + "  ·  " + TagEngine.FormatFileSize(f.FileSizeBytes);
                }
            };
            lstFiles.DoubleTapped += (_, _) => { if (selectedFile != null) PlayFile(selectedFile); };

            this.KeyDown += Window_KeyDown;
        }

        void BuildMenuBar()
        {
            var panel = this.FindControl<StackPanel>("MenuBarPanel");
            if (panel == null) return;
            panel.Children.Clear();

            var menus = new (string, string[])[]
            {
                ("Archivo", new[] { "Importar carpeta...", "Re-escanear librerías", "---", "Exportar lista a CSV", "---", "Salir" }),
                ("Editar",  new[] { "Copiar ruta del archivo", "Copiar archivo al portapapeles", "---", "Abrir en explorador", "---", "Marcar favorito", "Desmarcar favorito" }),
                ("Ver",     new[] { "Todos los archivos", "Solo favoritos", "---", "Mostrar panel lateral", "Ocultar panel lateral" }),
                ("Herramientas", new[] { "Re-etiquetar todo", "Eliminar entradas huérfanas", "---", "Abrir carpeta de datos" }),
                ("Ayuda",   new[] { "Atajos de teclado", "---", "Acerca de Etern Audio v1.0" })
            };

            foreach (var (title, items) in menus)
            {
                var capturedItems = items;
                var btn = new Button
                {
                    Content = title, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Foreground = TEXT, FontSize = 12, Padding = new Thickness(12, 4, 12, 4)
                };
                var flyout = new MenuFlyout();
                foreach (var item in capturedItems)
                {
                    if (item == "---") { flyout.Items.Add(new Separator()); continue; }
                    var capturedItem = item;
                    var mi = new MenuItem { Header = item };
                    mi.Click += (_, _) => HandleMenuAction(capturedItem);
                    flyout.Items.Add(mi);
                }
                btn.Flyout = flyout;
                btn.PointerEntered += (s, _) => ((Button)s).Foreground = ACCENT;
                btn.PointerExited  += (s, _) => ((Button)s).Foreground = TEXT;
                panel.Children.Add(btn);
            }
        }

        void RefreshSidebar()
        {
            if (sidebarContent == null) return;
            sidebarContent.Children.Clear();

            sidebarContent.Children.Add(MakeSidebarItem("🎵 Todos los archivos", null == activeLibraryId && !showFavoritesOnly, () =>
            {
                activeLibraryId = null; showFavoritesOnly = false;
                RefreshFileList(); RefreshSidebar();
            }));

            sidebarContent.Children.Add(MakeSidebarItem("⭐ Favoritos", showFavoritesOnly, () =>
            {
                showFavoritesOnly = !showFavoritesOnly; activeLibraryId = null;
                RefreshFileList(); RefreshSidebar();
            }));

            if (db.Libraries.Count > 0)
            {
                sidebarContent.Children.Add(new TextBlock
                {
                    Text = "─── CARPETAS ───", FontSize = 10, Foreground = DIM,
                    Margin = new Thickness(14, 8, 14, 4)
                });
            }

            foreach (var lib in db.Libraries)
            {
                var capturedLib = lib;
                sidebarContent.Children.Add(MakeSidebarItem("📂 " + lib.Name, activeLibraryId == lib.Id, () =>
                {
                    activeLibraryId = capturedLib.Id; showFavoritesOnly = false;
                    RefreshFileList(); RefreshSidebar();
                }));
            }

            sidebarContent.Children.Add(new Border { Height = 1, Background = BORDER, Margin = new Thickness(10, 10, 10, 10) });

            sidebarContent.Children.Add(new TextBlock
            {
                Text = "CATEGORÍAS", FontSize = 10, FontWeight = FontWeight.Bold,
                Foreground = MUTED, Margin = new Thickness(14, 0, 14, 8)
            });

            sidebarContent.Children.Add(MakeSidebarItem("  Todas", activeCategory == null, () =>
            {
                activeCategory = null; RefreshFileList(); RefreshSidebar();
            }));

            foreach (var cat in TagEngine.AllCategories)
            {
                var captured = cat;
                var catColor = IBrush.Parse(TagEngine.GetCategoryColor(cat));
                var catPanel = new StackPanel { Orientation = Orientation.Horizontal };
                catPanel.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = catColor, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 8, 0) });
                catPanel.Children.Add(new TextBlock { Text = cat, FontSize = 12, Foreground = activeCategory == cat ? TEXT : MUTED, VerticalAlignment = VerticalAlignment.Center });

                var catBtn = new Button
                {
                    Content = catPanel, Background = activeCategory == cat ? SolidColorBrush.Parse("#212121") : Brushes.Transparent,
                    BorderThickness = new Thickness(0), Padding = new Thickness(0, 5, 14, 5),
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                catBtn.Click += (_, _) => { activeCategory = activeCategory == captured ? null : captured; RefreshFileList(); RefreshSidebar(); };
                sidebarContent.Children.Add(catBtn);
            }
        }

        Control MakeSidebarItem(string text, bool isActive, Action onClick)
        {
            var btn = new Button
            {
                Content = text,
                Background = isActive ? SolidColorBrush.Parse("#2858a6ff") : Brushes.Transparent,
                BorderBrush = isActive ? ACCENT : Brushes.Transparent,
                BorderThickness = isActive ? new Thickness(2, 0, 0, 0) : new Thickness(0),
                Foreground = isActive ? ACCENT : MUTED,
                FontSize = 12, Padding = new Thickness(14, 7, 14, 7),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            btn.Click += (_, _) => onClick();
            return btn;
        }

        void SearchDebounce()
        {
            searchDebounce?.Stop();
            searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            searchDebounce.Tick += (_, _) => { searchDebounce.Stop(); RefreshFileList(); };
            searchDebounce.Start();
        }

        void RefreshFileList()
        {
            filteredFiles = searchEngine.Search(txtSearch?.Text ?? "", activeCategory, showFavoritesOnly, activeLibraryId);
            if (lblResultCount != null)
                lblResultCount.Text = filteredFiles.Count == 1 ? "1 archivo" : filteredFiles.Count + " archivos";

            if (lstFiles != null)
                lstFiles.ItemsSource = filteredFiles;
        }

        void SortFiles(string by)
        {
            switch (by)
            {
                case "Nombre":    filteredFiles = filteredFiles.OrderBy(f => f.DisplayName).ToList(); break;
                case "Categoría": filteredFiles = filteredFiles.OrderBy(f => f.Category).ThenBy(f => f.DisplayName).ToList(); break;
                case "Tamaño":    filteredFiles = filteredFiles.OrderByDescending(f => f.FileSizeBytes).ToList(); break;
            }
            lstFiles.ItemsSource = null;
            lstFiles.ItemsSource = filteredFiles;
        }

        async void AddLibrary()
        {
            var opts = new FolderPickerOpenOptions
            {
                Title = "Selecciona la carpeta de efectos de sonido",
                AllowMultiple = false
            };
            var result = await StorageProvider.OpenFolderPickerAsync(opts);
            if (result.Count > 0)
            {
                string path = result[0].TryGetLocalPath() ?? "";
                if (string.IsNullOrEmpty(path)) return;

                if (db.Libraries.Any(l => l.RootPath == path)) return;

                var lib = new SfxLibrary { Name = System.IO.Path.GetFileName(path), RootPath = path };
                db.Libraries.Add(lib);
                Storage.Save(db);
                RefreshSidebar();
                ScanLibrary(lib);
            }
        }

        void ScanLibrary(SfxLibrary lib)
        {
            if (lblStatus != null) lblStatus.Text = "Escaneando...";
            db.Files.RemoveAll(f => f.LibraryId == lib.Id);

            System.Threading.Tasks.Task.Run(() =>
            {
                int count = 0;
                var newFiles = new List<SfxFile>();
                ScanFolder(lib.RootPath, lib.Id, newFiles, ref count);
                return newFiles;
            }).ContinueWith(task =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    db.Files.AddRange(task.Result);
                    lib.FileCount = task.Result.Count;
                    lib.LastScannedTicks = DateTime.Now.Ticks;
                    Storage.Save(db);
                    RebuildIndex();
                    RefreshFileList();
                    if (lblStatus != null) lblStatus.Text = db.Files.Count + " archivos";
                });
            });
        }

        void ScanFolder(string path, string libId, List<SfxFile> results, ref int count)
        {
            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    if (TagEngine.IsAudioFile(file))
                    {
                        var sfx = TagEngine.AutoTag(file);
                        sfx.LibraryId = libId;
                        results.Add(sfx);
                        count++;
                    }
                }
                foreach (var dir in Directory.GetDirectories(path))
                    ScanFolder(dir, libId, results, ref count);
            }
            catch { }
        }

        void RescanAll() { foreach (var lib in db.Libraries.ToList()) ScanLibrary(lib); }
        void RebuildIndex() { searchEngine.BuildIndex(db.Files); }

        void SetupProgressTimer()
        {
            progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            progressTimer.Tick += (_, _) =>
            {
                if (!isPlaying) return;
                var elapsed = (DateTime.Now - playStartTime).TotalSeconds;
                if (totalDuration > 0 && slProgress != null)
                {
                    slProgress.Maximum = totalDuration;
                    slProgress.Value = Math.Min(elapsed, totalDuration);
                    if (lblCurrentTime != null) lblCurrentTime.Text = FormatTime(TimeSpan.FromSeconds(elapsed));
                    if (elapsed >= totalDuration) { isPlaying = false; if (btnPlayPause != null) btnPlayPause.Content = "▶"; }
                }
            };
            progressTimer.Start();
        }

        void PlayFile(SfxFile f)
        {
            if (f == null || !File.Exists(f.FilePath)) return;
            StopPlayer();

            try
            {
                if (lblNowPlaying != null) lblNowPlaying.Text = "▶  " + f.DisplayName + "  ·  " + f.Category;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    audioProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName presentationCore; $m = [System.Windows.Media.MediaPlayer]::new(); $m.Open([System.Uri]::new('{f.FilePath.Replace("'", "''")}'));  $m.Play(); Start-Sleep -Seconds 60; $m.Stop()\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    audioProcess = System.Diagnostics.Process.Start("afplay", "\"" + f.FilePath + "\"");
                }
                else
                {
                    audioProcess = System.Diagnostics.Process.Start("aplay", "\"" + f.FilePath + "\"");
                }

                isPlaying = true;
                playStartTime = DateTime.Now;
                totalDuration = EstimateDuration(f);
                if (slProgress != null) { slProgress.Value = 0; slProgress.Maximum = totalDuration; }
                if (lblTotalTime != null) lblTotalTime.Text = FormatTime(TimeSpan.FromSeconds(totalDuration));
                if (btnPlayPause != null) btnPlayPause.Content = "⏸";

                f.PlayCount++;
                Storage.Save(db);
            }
            catch (Exception ex)
            {
                if (lblNowPlaying != null) lblNowPlaying.Text = "Error: " + ex.Message;
            }
        }

        double EstimateDuration(SfxFile f)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(f.FilePath).ToLower();
                if (ext == ".wav")
                {
                    using (var fs = new FileStream(f.FilePath, FileMode.Open, FileAccess.Read))
                    using (var br = new System.IO.BinaryReader(fs))
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
            return Math.Max(1, f.FileSizeBytes / 16000.0);
        }

        void TogglePlayPause()
        {
            if (selectedFile == null) return;
            if (isPlaying) { StopPlayer(); }
            else { PlayFile(selectedFile); }
        }

        void StopPlayer()
        {
            try { audioProcess?.Kill(); } catch { }
            audioProcess = null;
            isPlaying = false;
            if (btnPlayPause != null) btnPlayPause.Content = "▶";
            if (slProgress != null) slProgress.Value = 0;
            if (lblCurrentTime != null) lblCurrentTime.Text = "0:00";
        }

        void NavigateFile(int direction)
        {
            if (filteredFiles.Count == 0) return;
            int idx = selectedFile == null ? -1 : filteredFiles.FindIndex(f => f.Id == selectedFile.Id);
            int newIdx = (idx + direction + filteredFiles.Count) % filteredFiles.Count;
            selectedFile = filteredFiles[newIdx];
            lstFiles.SelectedItem = selectedFile;
            PlayFile(selectedFile);
        }

        void CopyToClipboard(SfxFile f)
        {
            if (f == null || !File.Exists(f.FilePath)) return;
            try
            {
                Clipboard?.SetTextAsync(f.FilePath);
                if (lblStatus != null) lblStatus.Text = "Copiado: " + f.FileName;
            }
            catch { }
        }

        void OpenInExplorer(SfxFile f)
        {
            if (f == null || !File.Exists(f.FilePath)) return;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + f.FilePath + "\"");
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    System.Diagnostics.Process.Start("open", "-R \"" + f.FilePath + "\"");
                else
                    System.Diagnostics.Process.Start("xdg-open", System.IO.Path.GetDirectoryName(f.FilePath));
            }
            catch { }
        }

        void HandleMenuAction(string action)
        {
            switch (action)
            {
                case "Importar carpeta...": AddLibrary(); break;
                case "Re-escanear librerías": RescanAll(); break;
                case "Salir": Close(); break;
                case "Copiar ruta del archivo": Clipboard?.SetTextAsync(selectedFile?.FilePath ?? ""); break;
                case "Copiar archivo al portapapeles": CopyToClipboard(selectedFile); break;
                case "Abrir en explorador": OpenInExplorer(selectedFile); break;
                case "Marcar favorito": if (selectedFile != null) { selectedFile.IsFavorite = true; Storage.Save(db); RefreshFileList(); } break;
                case "Desmarcar favorito": if (selectedFile != null) { selectedFile.IsFavorite = false; Storage.Save(db); RefreshFileList(); } break;
                case "Todos los archivos": activeCategory = null; activeLibraryId = null; showFavoritesOnly = false; RefreshFileList(); RefreshSidebar(); break;
                case "Solo favoritos": showFavoritesOnly = !showFavoritesOnly; RefreshFileList(); RefreshSidebar(); break;
                case "Mostrar panel lateral": this.FindControl<Border>("SidebarBorder").IsVisible = true; break;
                case "Ocultar panel lateral": this.FindControl<Border>("SidebarBorder").IsVisible = false; break;
                case "Re-etiquetar todo":
                    foreach (var f in db.Files) { var t = TagEngine.AutoTag(f.FilePath); f.Tags = t.Tags; f.Category = t.Category; }
                    Storage.Save(db); RebuildIndex(); RefreshFileList();
                    break;
                case "Eliminar entradas huérfanas":
                    db.Files.RemoveAll(f => !File.Exists(f.FilePath));
                    Storage.Save(db); RebuildIndex(); RefreshFileList();
                    break;
                case "Abrir carpeta de datos":
                    var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EternAudio");
                    if (Directory.Exists(dir)) OpenInExplorer(new SfxFile { FilePath = dir });
                    break;
            }
        }

        void SetupMenuHideTimer()
        {
            menuHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            menuHideTimer.Tick += (_, _) => { menuHideTimer.Stop(); HideMenuBar(); };
        }

        void ShowMenuBar()
        {
            menuHideTimer?.Stop();
            if (menuBarVisible || menuBar == null) return;
            menuBar.IsVisible = true;
            menuBar.Height = 30;
            menuBarVisible = true;
        }

        void HideMenuBar()
        {
            if (!menuBarVisible || menuBar == null) return;
            menuBar.IsVisible = false;
            menuBar.Height = 0;
            menuBarVisible = false;
        }

        void ToggleSidebar()
        {
            var sb = this.FindControl<Border>("SidebarBorder");
            if (sb != null) sb.IsVisible = !sb.IsVisible;
        }

        void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { txtSearch.Text = ""; txtSearch.Focus(); }
            if (e.Key == Key.F5) RescanAll();
            if (e.Key == Key.Space && !(FocusManager?.GetFocusedElement() is TextBox)) { TogglePlayPause(); e.Handled = true; }
        }

        string FormatTime(TimeSpan t)
        {
            if (t.Hours > 0) return $"{t.Hours}:{t.Minutes:00}:{t.Seconds:00}";
            return $"{t.Minutes}:{t.Seconds:00}";
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;
using System.Collections.Specialized;

namespace EternAudio
{
    public class WpfMainWindow : Window
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                var app = new Application();
                app.Run(new WpfMainWindow());
            }
            catch (Exception ex)
            {
                File.WriteAllText("crash_log.txt", ex.ToString());
                MessageBox.Show("Error al iniciar Etern Audio: " + ex.Message);
            }
        }

        // ─── Colors (Identical to Etern-Notes / EternSynth / Spotify Dark) ─────
        static readonly Color BG        = Color.FromRgb(18, 18, 18);     // #121212
        static readonly Color SIDEBAR   = Color.FromRgb(24, 24, 24);     // #181818
        static readonly Color CARD      = Color.FromRgb(28, 28, 28);     // #1c1c1c
        static readonly Color CARDHOVER = Color.FromRgb(40, 40, 40);     // #282828
        static readonly Color BORDER_C  = Color.FromRgb(42, 42, 42);     // #2a2a2a
        static readonly Color ACCENT    = Color.FromRgb(88, 166, 255);   // #58a6ff (Etern Blue)
        static readonly Color ACCENTGREEN= Color.FromRgb(57, 211, 83);   // #39d353
        static readonly Color TEXT_C    = Colors.White;
        static readonly Color TEXTMUTED = Color.FromRgb(160, 160, 160); // #a0a0a0
        static readonly Color TEXTDIM   = Color.FromRgb(110, 110, 110);
        static readonly Color WARNING_C = Color.FromRgb(240, 136, 62);  // #f0883e

        static SolidColorBrush Br(Color c) { return new SolidColorBrush(c); }
        static SolidColorBrush BrH(string hex) { return (SolidColorBrush)(new BrushConverter().ConvertFrom(hex)); }

        // ─── State ──────────────────────────────────────────────────────────────
        SfxDatabase db;
        SearchEngine searchEngine = new SearchEngine();
        List<SfxFile> filteredFiles = new List<SfxFile>();
        SfxFile selectedFile;
        string activeCategory = null;
        string activeFolderPath = null;
        int activeLengthFilter = 0; // 0 = All, 1 = Short (<30s), 2 = Long (>=30s)
        bool showFavoritesOnly = false;
        bool isScanning = false;

        // Player
        System.Windows.Media.MediaPlayer mediaPlayer = new System.Windows.Media.MediaPlayer();
        DispatcherTimer playerTimer;
        bool isDraggingSlider = false;
        bool isPlaying = false;

        // UI controls
        TextBox txtSearch;
        ListView lstFiles;
        Border sidebarBorder;
        Grid contentGrid;
        Grid rootOverlayGrid;
        Border bannerUnorganized;
        Border progressModalOverlay;
        ProgressBar modalProgressBar;
        TextBlock modalProgressPercent;
        TextBlock modalProgressStatus;
        TextBlock lblResultCount;
        TextBlock lblNowPlaying;
        TextBlock lblCurrentTime;
        TextBlock lblTotalTime;
        Slider slProgress;
        Slider slVolume;
        Button btnPlayPause;
        StackPanel sidebarLibraryPanel;
        StackPanel sidebarTreePanel;
        TextBlock lblScanStatus;

        // Menubar
        Border menuBarBorder;
        bool menuBarVisible = false;
        DispatcherTimer menuHideTimer;
        bool isSidebarCollapsed = false;

        // Window drag
        bool isDraggingWindow = false;
        Point dragOffset;

        public WpfMainWindow()
        {
            Width = 1440; Height = 890;
            MinWidth = 980; MinHeight = 650;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Title = "Etern Audio";

            db = Storage.Load();
            BuildUI();
            SetupPlayer();
            SetupMenuHideTimer();

            KeyDown += Window_KeyDown;

            CheckAndAutoImportDesktopFolder();
            RefreshSidebar();
            RebuildIndex();
            RefreshFileList();
            CheckUnorganizedBanner();
        }

        void CheckAndAutoImportDesktopFolder()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string[] candidates = new string[]
            {
                System.IO.Path.Combine(desktop, "Efectos Sonido"),
                System.IO.Path.Combine(desktop, "Efectos de sonido"),
                System.IO.Path.Combine(desktop, "Efectos_de_sonido")
            };

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    bool exists = db.Libraries.Any(delegate(SfxLibrary l) { return l.RootPath == candidate; });
                    if (!exists)
                    {
                        var lib = new SfxLibrary { Name = System.IO.Path.GetFileName(candidate), RootPath = candidate };
                        db.Libraries.Add(lib);
                        Storage.Save(db);
                    }
                    ScanLibrary(db.Libraries[0]);
                    break;
                }
            }
        }

        void CheckUnorganizedBanner()
        {
            if (db.Libraries.Count == 0) { if (bannerUnorganized != null) bannerUnorganized.Visibility = Visibility.Collapsed; return; }

            string rootPath = db.Libraries[0].RootPath;
            int looseCount = 0;
            try
            {
                foreach (var f in Directory.GetFiles(rootPath))
                    if (TagEngine.IsAudioFile(f)) looseCount++;
            }
            catch { }

            int reviewCount = db.Files.Count(delegate(SfxFile f) { return f.NeedsReview; });

            if (bannerUnorganized != null)
            {
                if (looseCount > 0 || reviewCount > 0)
                {
                    bannerUnorganized.Visibility = Visibility.Visible;
                }
                else
                {
                    bannerUnorganized.Visibility = Visibility.Collapsed;
                }
            }
        }

        // ─── UI Building ────────────────────────────────────────────────────────

        void BuildUI()
        {
            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Br(BG),
                BorderBrush = Br(BORDER_C),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0.6, Color = Colors.Black }
            };

            rootOverlayGrid = new Grid();

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });  // TitleBar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });   // MenuBar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // Banner
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

            var titleBar = CreateTitleBar();
            Grid.SetRow(titleBar, 0); mainGrid.Children.Add(titleBar);

            menuBarBorder = CreateMenuBar();
            Grid.SetRow(menuBarBorder, 1); mainGrid.Children.Add(menuBarBorder);

            bannerUnorganized = CreateUnorganizedBanner();
            Grid.SetRow(bannerUnorganized, 2); mainGrid.Children.Add(bannerUnorganized);

            contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(contentGrid, 3); mainGrid.Children.Add(contentGrid);

            sidebarBorder = CreateSidebar();
            Grid.SetColumn(sidebarBorder, 0); contentGrid.Children.Add(sidebarBorder);

            var divider = new Border { Background = Br(BORDER_C), Width = 1 };
            Grid.SetColumn(divider, 1); contentGrid.Children.Add(divider);

            var mainArea = CreateMainArea();
            Grid.SetColumn(mainArea, 2); contentGrid.Children.Add(mainArea);

            rootOverlayGrid.Children.Add(mainGrid);

            progressModalOverlay = CreateProgressModalOverlay();
            rootOverlayGrid.Children.Add(progressModalOverlay);

            outerBorder.Child = rootOverlayGrid;
            Content = outerBorder;
        }

        Border CreateUnorganizedBanner()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 240, 136, 62)),
                BorderBrush = Br(WARNING_C),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 6, 14, 6),
                Visibility = Visibility.Collapsed
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var txt = new TextBlock
            {
                Text = "⚠️ Se han detectado audios sin organizar. Haz clic en [Verificar y Auto-Organizar] para categorizarlos en subcarpetas y renombrarlos con barra baja.",
                FontSize = 11, Foreground = Br(TEXT_C), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txt, 0); grid.Children.Add(txt);

            var btn = new Button
            {
                Content = "\u26a1 Auto-Organizar Ahora", FontSize = 11, FontWeight = FontWeights.Bold,
                Background = Br(WARNING_C), Foreground = Br(BG), BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 3, 10, 3), Cursor = Cursors.Hand
            };
            btn.Click += delegate(object s, RoutedEventArgs e) { RunAutoOrganization(); };
            Grid.SetColumn(btn, 1); grid.Children.Add(btn);

            border.Child = grid;
            return border;
        }

        Border CreateProgressModalOverlay()
        {
            var overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 10, 10, 15)),
                Visibility = Visibility.Collapsed
            };

            var modalCard = new Border
            {
                Width = 460, Height = 180,
                CornerRadius = new CornerRadius(12),
                Background = Br(SIDEBAR),
                BorderBrush = Br(ACCENT),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 0, Opacity = 0.8, Color = Colors.Black }
            };

            var modalGrid = new Grid();
            modalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            modalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            modalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock { Text = "\u26a1 Categorizando Audios y Renombrando con Barra Baja...", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Br(TEXT_C), Margin = new Thickness(0, 0, 0, 14) };
            Grid.SetRow(title, 0); modalGrid.Children.Add(title);

            modalProgressBar = new ProgressBar { Height = 12, Minimum = 0, Maximum = 100, Value = 0, Foreground = Br(ACCENTGREEN), Background = Br(CARD), BorderThickness = new Thickness(0) };
            Grid.SetRow(modalProgressBar, 1); modalGrid.Children.Add(modalProgressBar);

            var infoGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            modalProgressStatus = new TextBlock { Text = "Analizando biblioteca de sonidos...", FontSize = 11, Foreground = Br(TEXTMUTED), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(modalProgressStatus, 0); infoGrid.Children.Add(modalProgressStatus);

            modalProgressPercent = new TextBlock { Text = "0%", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Br(ACCENTGREEN) };
            Grid.SetColumn(modalProgressPercent, 1); infoGrid.Children.Add(modalProgressPercent);

            Grid.SetRow(infoGrid, 2); modalGrid.Children.Add(infoGrid);

            modalCard.Child = modalGrid;
            overlay.Child = modalCard;
            return overlay;
        }

        Grid CreateTitleBar()
        {
            var g = new Grid { Background = Br(SIDEBAR) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnH = MakeWinBtn("=", Colors.Transparent, delegate() { ToggleSidebar(); });
            btnH.FontSize = 14; btnH.ToolTip = "Colapsar panel lateral";
            Grid.SetColumn(btnH, 0); g.Children.Add(btnH);

            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            titlePanel.Children.Add(new TextBlock { Text = "Etern Audio", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Br(TEXT_C), VerticalAlignment = VerticalAlignment.Center });
            titlePanel.Children.Add(new TextBlock { Text = " \u25cf", FontSize = 10, Foreground = Br(ACCENT), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(titlePanel, 1); g.Children.Add(titlePanel);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            btnPanel.Children.Add(MakeWinBtn("\u2500", Colors.Transparent, delegate() { WindowState = WindowState.Minimized; }));
            btnPanel.Children.Add(MakeWinBtn("\u25a1", Colors.Transparent, delegate() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }));
            btnPanel.Children.Add(MakeWinBtn("\u2715", Color.FromRgb(239, 68, 68), delegate() { Close(); }));
            Grid.SetColumn(btnPanel, 2); g.Children.Add(btnPanel);

            g.MouseEnter += delegate(object s, MouseEventArgs e) { ShowMenuBar(); };
            g.MouseLeave += delegate(object s, MouseEventArgs e) { if (menuBarVisible) menuHideTimer.Start(); };
            g.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { isDraggingWindow = true; dragOffset = e.GetPosition(this); g.CaptureMouse(); };
            g.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { isDraggingWindow = false; g.ReleaseMouseCapture(); };
            g.MouseMove += delegate(object s, MouseEventArgs e) { if (isDraggingWindow) { Point p = e.GetPosition(null); Left += p.X - dragOffset.X; Top += p.Y - dragOffset.Y; } };

            return g;
        }

        Border CreateMenuBar()
        {
            var border = new Border
            {
                Background = Br(Color.FromRgb(22, 22, 22)),
                BorderBrush = Br(BORDER_C),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 0
            };
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            AddMenu(panel, "Archivo", new string[] { "Importar carpeta de audios...", "Re-escanear librerías", "---", "Exportar lista a CSV", "---", "Salir" });
            AddMenu(panel, "Editar",  new string[] { "Copiar ruta del archivo", "Copiar archivo al portapapeles", "---", "Abrir en explorador", "---", "Marcar favorito", "Desmarcar favorito" });
            AddMenu(panel, "Ver",     new string[] { "Todos los archivos", "Solo audios cortos (<30s)", "Solo música / largos (>=30s)", "---", "Mostrar panel lateral", "Ocultar panel lateral" });
            AddMenu(panel, "Herramientas", new string[] { "Verificar y Auto-Organizar archivos", "Re-etiquetar todo", "Eliminar entradas huerfanas", "---", "Abrir carpeta de datos" });
            AddMenu(panel, "Ayuda",   new string[] { "Atajos de teclado", "---", "Acerca de Etern Audio v1.0" });

            border.Child = panel;
            border.MouseEnter += delegate(object s, MouseEventArgs e) { menuHideTimer.Stop(); ShowMenuBar(); };
            border.MouseLeave += delegate(object s, MouseEventArgs e) { menuHideTimer.Start(); };
            return border;
        }

        void AddMenu(StackPanel panel, string title, string[] items)
        {
            var btn = new Button { Content = title, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Br(TEXT_C), FontSize = 12, Padding = new Thickness(12, 4, 12, 4), Cursor = Cursors.Hand };
            btn.MouseEnter += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(ACCENT); };
            btn.MouseLeave += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(TEXT_C); };
            var cm = new ContextMenu { Background = Br(CARD), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(1) };
            foreach (var itm in items)
            {
                if (itm == "---") { cm.Items.Add(new Separator()); continue; }
                var mi = new MenuItem { Header = itm, Background = Brushes.Transparent, Foreground = Br(TEXT_C), FontSize = 12 };
                mi.MouseEnter += delegate(object s, MouseEventArgs e) { ((MenuItem)s).Background = Br(CARDHOVER); };
                mi.MouseLeave += delegate(object s, MouseEventArgs e) { ((MenuItem)s).Background = Brushes.Transparent; };
                string captured = itm;
                mi.Click += delegate(object s, RoutedEventArgs e) { HandleMenuAction(captured); };
                cm.Items.Add(mi);
            }
            btn.Click += delegate(object s, RoutedEventArgs e) { cm.PlacementTarget = btn; cm.IsOpen = true; };
            panel.Children.Add(btn);
        }

        Border CreateSidebar()
        {
            var border = new Border { Background = Br(SIDEBAR) };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var libHeader = new Grid { Margin = new Thickness(0, 12, 0, 6) };
            libHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            libHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var txtLibTitle = new TextBlock { Text = "ARBOL DE CARPETAS", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Br(TEXTMUTED), Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(txtLibTitle, 0); libHeader.Children.Add(txtLibTitle);

            var btnAdd = new Button { Content = "+ Importar", FontSize = 11, FontWeight = FontWeights.SemiBold, Background = new SolidColorBrush(Color.FromArgb(40, 88, 166, 255)), BorderThickness = new Thickness(1), BorderBrush = Br(ACCENT), Foreground = Br(ACCENT), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(6, 2, 6, 2), ToolTip = "Importar carpeta de efectos de sonido" };
            btnAdd.Click += delegate(object s, RoutedEventArgs e) { AddLibrary(); };
            Grid.SetColumn(btnAdd, 1); libHeader.Children.Add(btnAdd);
            Grid.SetRow(libHeader, 0); grid.Children.Add(libHeader);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var content = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            sidebarLibraryPanel = new StackPanel();
            content.Children.Add(sidebarLibraryPanel);

            content.Children.Add(new Border { Height = 1, Background = Br(BORDER_C), Margin = new Thickness(10, 8, 10, 8) });

            sidebarTreePanel = new StackPanel();
            content.Children.Add(sidebarTreePanel);

            scroll.Content = content;
            Grid.SetRow(scroll, 1); grid.Children.Add(scroll);

            var footer = new Border { Background = Br(CARD), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(14, 8, 14, 8) };
            lblScanStatus = new TextBlock { Text = "Listo", FontSize = 10, Foreground = Br(TEXTMUTED) };
            footer.Child = lblScanStatus;
            Grid.SetRow(footer, 2); grid.Children.Add(footer);

            border.Child = grid;
            return border;
        }

        Grid CreateMainArea()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });  // Search bar
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // Stats & pills
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });  // Spotify table header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Spotify Track List
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(90) });  // Spotify Player Bar

            var searchBar = CreateSearchBar();
            Grid.SetRow(searchBar, 0); grid.Children.Add(searchBar);

            var statsRow = new Border { Background = Br(SIDEBAR), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(16, 6, 16, 6) };
            var sg = new Grid();
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            lblResultCount = new TextBlock { Text = "0 archivos", FontSize = 12, Foreground = Br(TEXTMUTED), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            leftPanel.Children.Add(lblResultCount);

            var btnOrganize = new Button { Content = "\u26a1 Verificar y Auto-Organizar", FontSize = 11, FontWeight = FontWeights.SemiBold, Background = new SolidColorBrush(Color.FromArgb(35, 57, 211, 83)), BorderBrush = Br(ACCENTGREEN), BorderThickness = new Thickness(1), Foreground = Br(ACCENTGREEN), Padding = new Thickness(8, 2, 8, 2), Cursor = Cursors.Hand, ToolTip = "Organiza archivos sueltos y los renombra automáticamente a español" };
            btnOrganize.Click += delegate(object s, RoutedEventArgs e) { RunAutoOrganization(); };
            leftPanel.Children.Add(btnOrganize);

            Grid.SetColumn(leftPanel, 0); sg.Children.Add(leftPanel);

            var rightPanel = new StackPanel { Orientation = Orientation.Horizontal };
            BuildStatsRowPills(rightPanel);

            Grid.SetColumn(rightPanel, 1); sg.Children.Add(rightPanel);
            statsRow.Child = sg;
            Grid.SetRow(statsRow, 1); grid.Children.Add(statsRow);

            var tableHeader = CreateSpotifyTableHeader();
            Grid.SetRow(tableHeader, 2); grid.Children.Add(tableHeader);

            lstFiles = new ListView
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                SelectionMode = SelectionMode.Single,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            lstFiles.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            lstFiles.SelectionChanged += LstFiles_SelectionChanged;
            lstFiles.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e) { PlaySelectedFile(); };
            lstFiles.MouseMove += LstFiles_MouseMove;
            lstFiles.KeyDown += LstFiles_KeyDown;

            var itemStyle = new Style(typeof(ListViewItem));
            itemStyle.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Br(BG)));
            itemStyle.Setters.Add(new Setter(ListViewItem.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListViewItem.MarginProperty, new Thickness(0, 1, 0, 1)));
            itemStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            var hoverT = new Trigger { Property = ListViewItem.IsMouseOverProperty, Value = true };
            hoverT.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Br(CARDHOVER)));
            var selT = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
            selT.Setters.Add(new Setter(ListViewItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 88, 166, 255))));
            itemStyle.Triggers.Add(hoverT); itemStyle.Triggers.Add(selT);
            lstFiles.ItemContainerStyle = itemStyle;

            var listBorder = new Border { Child = new ScrollViewer { Content = lstFiles, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, Background = Br(BG) };
            Grid.SetRow(listBorder, 3); grid.Children.Add(listBorder);

            var divBorder = new Border { Height = 1, Background = Br(BORDER_C) };
            Grid.SetRow(divBorder, 4); grid.Children.Add(divBorder);

            var player = CreateSpotifyPlayerPanel();
            Grid.SetRow(player, 5); grid.Children.Add(player);

            return grid;
        }

        Border CreateSpotifyTableHeader()
        {
            var border = new Border { Background = Br(SIDEBAR), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(14, 0, 14, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // #
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });  // Icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // TÍTULO
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // CARPETA / CATEGORÍA
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // TAMAÑO
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // COINCIDENCIA
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // DURA
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // ACCIONES

            grid.Children.Add(MakeHeaderLabel("#", 0));
            grid.Children.Add(MakeHeaderLabel("", 1));
            grid.Children.Add(MakeHeaderLabel("TÍTULO", 2));
            grid.Children.Add(MakeHeaderLabel("CARPETA / CATEGORÍA", 3));
            grid.Children.Add(MakeHeaderLabel("TAMAÑO", 4));
            grid.Children.Add(MakeHeaderLabel("COINCIDENCIA", 5));
            grid.Children.Add(MakeHeaderLabel("DURA", 6));
            grid.Children.Add(MakeHeaderLabel("ACCIONES", 7));

            border.Child = grid;
            return border;
        }

        TextBlock MakeHeaderLabel(string text, int col)
        {
            var tb = new TextBlock { Text = text, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Br(TEXTDIM), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tb, col);
            return tb;
        }

        Button MakeFilterPill(string text, bool isActive, Action onClick)
        {
            var btn = new Button
            {
                Content = text, FontSize = 11,
                Background = isActive ? new SolidColorBrush(Color.FromArgb(40, 88, 166, 255)) : Brushes.Transparent,
                BorderBrush = isActive ? Br(ACCENT) : Br(BORDER_C),
                BorderThickness = new Thickness(1),
                Foreground = isActive ? Br(ACCENT) : Br(TEXTMUTED),
                Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(3, 0, 0, 0), Cursor = Cursors.Hand
            };
            btn.Click += delegate(object s, RoutedEventArgs e) { onClick(); };
            return btn;
        }

        void BuildStatsRowPills(StackPanel panel)
        {
            panel.Children.Clear();
            panel.Children.Add(MakeFilterPill("Todos", activeLengthFilter == 0, delegate() { activeLengthFilter = 0; RefreshFileList(); BuildStatsRowPills(panel); }));
            panel.Children.Add(MakeFilterPill("\u26a1 Cortos (<30s)", activeLengthFilter == 1, delegate() { activeLengthFilter = 1; RefreshFileList(); BuildStatsRowPills(panel); }));
            panel.Children.Add(MakeFilterPill("\ud83c\udfb5 Largos (>=30s)", activeLengthFilter == 2, delegate() { activeLengthFilter = 2; RefreshFileList(); BuildStatsRowPills(panel); }));
        }

        Border CreateSearchBar()
        {
            var border = new Border { Background = Br(SIDEBAR), Padding = new Thickness(12, 10, 12, 10), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(0, 0, 0, 1) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            var iconTB = new TextBlock { Text = "\ud83d\udd0d", FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Foreground = Br(TEXTMUTED) };
            Grid.SetColumn(iconTB, 0); grid.Children.Add(iconTB);

            txtSearch = new TextBox { FontSize = 14, Foreground = Br(TEXT_C), Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center, CaretBrush = Br(ACCENT) };
            txtSearch.TextChanged += delegate(object s, TextChangedEventArgs e) { SearchDebounce(); };
            txtSearch.GotFocus  += delegate(object s, RoutedEventArgs e) { border.BorderBrush = Br(ACCENT); };
            txtSearch.LostFocus += delegate(object s, RoutedEventArgs e) { border.BorderBrush = Br(BORDER_C); };

            var wm = new TextBlock { Text = "Buscar en todos los audios... (gallo, pollo, japón, anime, golpear...)", Foreground = Br(TEXTDIM), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            txtSearch.TextChanged += delegate(object s, TextChangedEventArgs e) { wm.Visibility = string.IsNullOrEmpty(txtSearch.Text) ? Visibility.Visible : Visibility.Collapsed; };
            var ig = new Grid(); ig.Children.Add(wm); ig.Children.Add(txtSearch);
            Grid.SetColumn(ig, 1); grid.Children.Add(ig);

            var rp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var btnClear = new Button { Content = "\u2715", FontSize = 12, Foreground = Br(TEXTMUTED), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(6, 2, 6, 2) };
            btnClear.Click += delegate(object s, RoutedEventArgs e) { txtSearch.Text = ""; txtSearch.Focus(); };
            var btnFav = new Button { Content = "\u2b50", FontSize = 14, Foreground = Br(TEXTMUTED), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(6, 2, 6, 2), ToolTip = "Solo favoritos" };
            btnFav.Click += delegate(object s, RoutedEventArgs e) { showFavoritesOnly = !showFavoritesOnly; btnFav.Foreground = showFavoritesOnly ? Br(WARNING_C) : Br(TEXTMUTED); RefreshFileList(); };
            rp.Children.Add(btnClear); rp.Children.Add(btnFav);
            Grid.SetColumn(rp, 2); grid.Children.Add(rp);
            border.Child = grid;
            return border;
        }

        // ─── Spotify Player Panel ────────────────────────────────────────────────

        Border CreateSpotifyPlayerPanel()
        {
            var border = new Border { Background = Br(SIDEBAR), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(0, 1, 0, 0) };
            var grid = new Grid { Margin = new Thickness(16, 8, 16, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            var leftGrid = new Grid();
            leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var coverBorder = new Border { Width = 46, Height = 46, CornerRadius = new CornerRadius(6), Background = Br(CARD), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
            var coverIcon = new TextBlock { Text = "\ud83c\udfb5", FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            coverBorder.Child = coverIcon;
            Grid.SetColumn(coverBorder, 0); leftGrid.Children.Add(coverBorder);

            var trackTextPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            lblNowPlaying = new TextBlock { Text = "Etern Audio", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Br(TEXT_C), TextTrimming = TextTrimming.CharacterEllipsis };
            var lblSub = new TextBlock { Text = "Selecciona un sonido para reproducir", FontSize = 11, Foreground = Br(TEXTMUTED), TextTrimming = TextTrimming.CharacterEllipsis };
            trackTextPanel.Children.Add(lblNowPlaying);
            trackTextPanel.Children.Add(lblSub);
            Grid.SetColumn(trackTextPanel, 1); leftGrid.Children.Add(trackTextPanel);
            Grid.SetColumn(leftGrid, 0); grid.Children.Add(leftGrid);

            var centerPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Width = 520 };

            var pc = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
            pc.Children.Add(MakeCtrlBtn("\ud83d\udd00", 12, delegate() { }));
            pc.Children.Add(MakeCtrlBtn("\u23ee", 14, delegate() { NavigateFile(-1); }));
            btnPlayPause = new Button { Content = "\u25b6", FontSize = 18, Background = Br(TEXT_C), BorderThickness = new Thickness(0), Foreground = Br(BG), Width = 36, Height = 36, Cursor = Cursors.Hand, Padding = new Thickness(0), Margin = new Thickness(12, 0, 12, 0) };
            btnPlayPause.Click += delegate(object s, RoutedEventArgs e) { TogglePlayPause(); }; pc.Children.Add(btnPlayPause);
            pc.Children.Add(MakeCtrlBtn("\u23ed", 14, delegate() { NavigateFile(1); }));
            pc.Children.Add(MakeCtrlBtn("\ud83d\udd01", 12, delegate() { }));
            centerPanel.Children.Add(pc);

            var pr = new Grid();
            pr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            lblCurrentTime = new TextBlock { Text = "0:00", FontSize = 11, Foreground = Br(TEXTMUTED), VerticalAlignment = VerticalAlignment.Center, Width = 36 };
            Grid.SetColumn(lblCurrentTime, 0); pr.Children.Add(lblCurrentTime);
            slProgress = new Slider { Minimum = 0, Maximum = 1, Value = 0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
            slProgress.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { isDraggingSlider = true; };
            slProgress.PreviewMouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { isDraggingSlider = false; if (isPlaying) mediaPlayer.Position = TimeSpan.FromSeconds(slProgress.Value); };
            Grid.SetColumn(slProgress, 1); pr.Children.Add(slProgress);
            lblTotalTime = new TextBlock { Text = "0:00", FontSize = 11, Foreground = Br(TEXTMUTED), VerticalAlignment = VerticalAlignment.Center, Width = 36, TextAlignment = TextAlignment.Right };
            Grid.SetColumn(lblTotalTime, 2); pr.Children.Add(lblTotalTime);
            centerPanel.Children.Add(pr);

            Grid.SetColumn(centerPanel, 1); grid.Children.Add(centerPanel);

            var rightPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var volIcon = new TextBlock { Text = "\ud83d\udd0a", FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            slVolume = new Slider { Minimum = 0, Maximum = 1, Value = 0.85, Width = 80, VerticalAlignment = VerticalAlignment.Center };
            slVolume.ValueChanged += delegate(object s, RoutedPropertyChangedEventArgs<double> e) { mediaPlayer.Volume = slVolume.Value; };
            rightPanel.Children.Add(volIcon); rightPanel.Children.Add(slVolume);

            var btnCopy = new Button { Content = "📋 Copiar", FontSize = 11, Foreground = Br(TEXTMUTED), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
            btnCopy.Click += delegate(object s, RoutedEventArgs e) { CopyFileToClipboard(selectedFile); };
            btnCopy.MouseEnter += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(ACCENT); };
            btnCopy.MouseLeave += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(TEXTMUTED); };

            var btnOpen = new Button { Content = "📁", FontSize = 13, Foreground = Br(TEXTMUTED), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(4, 2, 4, 2), ToolTip = "Abrir en explorador" };
            btnOpen.Click += delegate(object s, RoutedEventArgs e) { OpenSelectedInExplorer(); };
            btnOpen.MouseEnter += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(ACCENT); };
            btnOpen.MouseLeave += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(TEXTMUTED); };

            rightPanel.Children.Add(btnCopy); rightPanel.Children.Add(btnOpen);
            Grid.SetColumn(rightPanel, 2); grid.Children.Add(rightPanel);

            border.Child = grid;
            return border;
        }

        Button MakeCtrlBtn(string content, int size, Action onClick)
        {
            var btn = new Button { Content = content, FontSize = size, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Br(TEXTMUTED), Cursor = Cursors.Hand, Padding = new Thickness(8, 2, 8, 2) };
            btn.Click += delegate(object s, RoutedEventArgs e) { onClick(); };
            btn.MouseEnter += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(TEXT_C); };
            btn.MouseLeave += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(TEXTMUTED); };
            return btn;
        }

        // ─── Modernized Sidebar Tree + Drag & Drop Target + Context Menu ───────

        void RefreshSidebar()
        {
            sidebarLibraryPanel.Children.Clear();
            sidebarTreePanel.Children.Clear();

            sidebarLibraryPanel.Children.Add(MakeSidebarItem("\ud83c\udfb5 Todos los archivos", activeFolderPath == null && activeCategory == null && !showFavoritesOnly, delegate()
            {
                activeFolderPath = null; activeCategory = null; showFavoritesOnly = false;
                RefreshFileList(); RefreshSidebar();
            }));

            sidebarLibraryPanel.Children.Add(MakeSidebarItem("\u2b50 Favoritos", showFavoritesOnly, delegate()
            {
                showFavoritesOnly = !showFavoritesOnly; activeFolderPath = null; activeCategory = null;
                RefreshFileList(); RefreshSidebar();
            }));

            if (db.Libraries.Count == 0) return;

            var rootLib = db.Libraries[0];
            var treeRoot = FileOrganizer.BuildDirectoryTree(rootLib.RootPath);
            if (treeRoot != null)
            {
                RenderTreeNode(sidebarTreePanel, treeRoot, 0);
            }
        }

        void RenderTreeNode(StackPanel container, FolderNode node, int depth)
        {
            var capturedNode = node;
            bool isSelected = activeFolderPath == node.FullPath;

            var itemGrid = new Grid { Margin = new Thickness(depth * 14, 2, 0, 2) };
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string icon = depth == 0 ? "\ud83d\udcc2 " : (node.Children.Count > 0 ? "\ud83d\udcc1 " : "\ud83d\udcc4 ");
            string titleText = icon + node.Name;

            var btnNode = new Button
            {
                Content = titleText,
                Background = isSelected ? new SolidColorBrush(Color.FromArgb(40, 88, 166, 255)) : Brushes.Transparent,
                BorderThickness = isSelected ? new Thickness(2, 0, 0, 0) : new Thickness(0),
                BorderBrush = Br(ACCENT),
                Foreground = isSelected ? Br(ACCENT) : Br(TEXT_C),
                FontSize = depth == 0 ? 12 : 11,
                FontWeight = depth == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                Cursor = Cursors.Hand, Padding = new Thickness(10, 6, 10, 6),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                AllowDrop = true
            };

            // Drag & Drop Target Handling (Drop audio onto folder!)
            btnNode.DragOver += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects = DragDropEffects.Move;
                else e.Effects = DragDropEffects.None;
                e.Handled = true;
            };

            btnNode.Drop += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        foreach (string file in files)
                        {
                            FileOrganizer.MoveFileToFolder(file, capturedNode.FullPath);
                        }
                        if (db.Libraries.Count > 0) ScanLibrary(db.Libraries[0]);
                    }
                }
            };

            btnNode.Click += delegate(object s, RoutedEventArgs e)
            {
                activeFolderPath = capturedNode.FullPath;
                activeCategory = null; showFavoritesOnly = false;
                RefreshFileList(); RefreshSidebar();
            };

            // Folder Context Menu (Right Click)
            var folderCM = new ContextMenu { Background = Br(CARD), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(1) };

            var miRename = new MenuItem { Header = "✏️ Renombrar carpeta", Background = Brushes.Transparent, Foreground = Br(TEXT_C), FontSize = 12 };
            miRename.Click += delegate(object s, RoutedEventArgs e) { PromptRenameFolder(capturedNode); };
            folderCM.Items.Add(miRename);

            var miDelete = new MenuItem { Header = "🗑️ Eliminar carpeta", Background = Brushes.Transparent, Foreground = Br(TEXT_C), FontSize = 12 };
            miDelete.Click += delegate(object s, RoutedEventArgs e) { DeleteFolderPrompt(capturedNode); };
            folderCM.Items.Add(miDelete);

            var miNewSub = new MenuItem { Header = "➕ Crear Subcarpeta", Background = Brushes.Transparent, Foreground = Br(TEXT_C), FontSize = 12 };
            miNewSub.Click += delegate(object s, RoutedEventArgs e) { CreateSubfolderPrompt(capturedNode); };
            folderCM.Items.Add(miNewSub);

            folderCM.Items.Add(new Separator());

            var miOpen = new MenuItem { Header = "📁 Abrir en Explorador", Background = Brushes.Transparent, Foreground = Br(TEXT_C), FontSize = 12 };
            miOpen.Click += delegate(object s, RoutedEventArgs e) { if (Directory.Exists(capturedNode.FullPath)) System.Diagnostics.Process.Start("explorer.exe", capturedNode.FullPath); };
            folderCM.Items.Add(miOpen);

            btnNode.ContextMenu = folderCM;

            Grid.SetColumn(btnNode, 0); itemGrid.Children.Add(btnNode);

            var badge = new TextBlock { Text = node.FileCount.ToString(), FontSize = 10, Foreground = Br(TEXTDIM), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(badge, 1); itemGrid.Children.Add(badge);

            container.Children.Add(itemGrid);

            foreach (var child in node.Children)
            {
                RenderTreeNode(container, child, depth + 1);
            }
        }

        void PromptRenameFolder(FolderNode node)
        {
            if (node == null || !Directory.Exists(node.FullPath)) return;
            string oldName = node.Name;
            string parentDir = System.IO.Path.GetDirectoryName(node.FullPath);
            string input = ShowInputPrompt("Introduce el nuevo nombre para la carpeta:", "Renombrar Carpeta", oldName);
            if (!string.IsNullOrWhiteSpace(input) && input != oldName)
            {
                string cleanNewName = input.Trim();
                string newFolderPath = System.IO.Path.Combine(parentDir, cleanNewName);
                try
                {
                    Directory.Move(node.FullPath, newFolderPath);
                    if (db.Libraries.Count > 0) ScanLibrary(db.Libraries[0]);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al renombrar la carpeta: " + ex.Message);
                }
            }
        }

        void DeleteFolderPrompt(FolderNode node)
        {
            if (node == null || !Directory.Exists(node.FullPath)) return;
            if (db.Libraries.Count > 0 && node.FullPath.Equals(db.Libraries[0].RootPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("No se puede eliminar la carpeta raíz principal.", "Etern Audio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show("¿Seguro que deseas eliminar la carpeta '" + node.Name + "'?", "Eliminar Carpeta", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    Directory.Delete(node.FullPath, true);
                    if (db.Libraries.Count > 0) ScanLibrary(db.Libraries[0]);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al eliminar la carpeta: " + ex.Message);
                }
            }
        }

        void CreateSubfolderPrompt(FolderNode node)
        {
            if (node == null || !Directory.Exists(node.FullPath)) return;
            string input = ShowInputPrompt("Nombre de la nueva subcarpeta:", "Crear Subcarpeta", "Nueva_Categoria");
            if (!string.IsNullOrWhiteSpace(input))
            {
                string newDir = System.IO.Path.Combine(node.FullPath, input.Trim());
                try
                {
                    Directory.CreateDirectory(newDir);
                    if (db.Libraries.Count > 0) ScanLibrary(db.Libraries[0]);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al crear la subcarpeta: " + ex.Message);
                }
            }
        }

        string ShowInputPrompt(string message, string title, string defaultValue)
        {
            var win = new Window
            {
                Title = title, Width = 380, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, WindowStyle = WindowStyle.ToolWindow, Background = Br(SIDEBAR), ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock { Text = message, Foreground = Br(TEXT_C), FontSize = 12, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(lbl, 0); grid.Children.Add(lbl);

            var tb = new TextBox { Text = defaultValue, FontSize = 13, Foreground = Br(TEXT_C), Background = Br(CARD), BorderBrush = Br(BORDER_C), Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetRow(tb, 1); grid.Children.Add(tb);

            var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Aceptar", Width = 80, Height = 28, Background = Br(ACCENT), Foreground = Br(BG), BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0) };
            string result = null;
            btnOk.Click += delegate(object s, RoutedEventArgs e) { result = tb.Text; win.Close(); };
            var btnCancel = new Button { Content = "Cancelar", Width = 80, Height = 28, Background = Br(CARD), Foreground = Br(TEXTMUTED), BorderThickness = new Thickness(1), BorderBrush = Br(BORDER_C), Cursor = Cursors.Hand };
            btnCancel.Click += delegate(object s, RoutedEventArgs e) { win.Close(); };
            bp.Children.Add(btnOk); bp.Children.Add(btnCancel);
            Grid.SetRow(bp, 2); grid.Children.Add(bp);

            win.Content = grid;
            win.ShowDialog();
            return result;
        }

        FrameworkElement MakeSidebarItem(string text, bool isActive, Action onClick)
        {
            var btn = new Button { Content = text, Background = isActive ? new SolidColorBrush(Color.FromArgb(40, 88, 166, 255)) : Brushes.Transparent, BorderThickness = isActive ? new Thickness(2, 0, 0, 0) : new Thickness(0), BorderBrush = Br(ACCENT), Foreground = isActive ? Br(ACCENT) : Br(TEXTMUTED), FontSize = 12, Cursor = Cursors.Hand, Padding = new Thickness(14, 7, 14, 7), HorizontalContentAlignment = HorizontalAlignment.Left };
            btn.MouseEnter += delegate(object s, MouseEventArgs e) { if (!isActive) ((Button)s).Background = Br(CARDHOVER); };
            btn.MouseLeave += delegate(object s, MouseEventArgs e) { if (!isActive) ((Button)s).Background = Brushes.Transparent; };
            btn.Click += delegate(object s, RoutedEventArgs e) { onClick(); };
            return btn;
        }

        // ─── File List (Exact Spotify Table Row Layout - Full Width Stretch) ───

        DispatcherTimer searchTimer;
        void SearchDebounce() { if (searchTimer != null) searchTimer.Stop(); searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) }; searchTimer.Tick += delegate(object s, EventArgs e) { searchTimer.Stop(); RefreshFileList(); }; searchTimer.Start(); }

        void RefreshFileList()
        {
            filteredFiles = searchEngine.Search(txtSearch != null ? txtSearch.Text : "", activeCategory, showFavoritesOnly, activeFolderPath, activeLengthFilter);
            lstFiles.Items.Clear();

            int trackNumber = 1;
            bool isSearching = txtSearch != null && !string.IsNullOrWhiteSpace(txtSearch.Text);

            foreach (var f in filteredFiles)
            {
                var item = new ListViewItem { Tag = f, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                item.Content = BuildSpotifyRowItem(f, trackNumber, isSearching);
                lstFiles.Items.Add(item);
                trackNumber++;
            }

            if (lblResultCount != null) lblResultCount.Text = filteredFiles.Count.ToString() + " archivo" + (filteredFiles.Count != 1 ? "s" : "");
        }

        UIElement BuildSpotifyRowItem(SfxFile f, int indexNumber, bool isSearching)
        {
            var border = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                Background = Br(CARD),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 1, 0, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // 0: # Index Number
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });  // 1: Icon Artwork
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 2: Track Title + Subtitle
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // 3: Subfolder / Category
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // 4: File Size
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // 5: Match Score
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // 6: Duration
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // 7: Actions

            // Col 0: Index Number
            var numTB = new TextBlock { Text = indexNumber.ToString(), FontSize = 12, Foreground = Br(TEXTDIM), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetColumn(numTB, 0); grid.Children.Add(numTB);

            // Col 1: Cover Icon
            string catColor = TagEngine.GetCategoryColor(f.Category);
            var iconBox = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(6), Background = BrH(catColor), Opacity = 0.85, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            string iconStr = f.IsShortSfx ? "\u26a1" : "\ud83c\udfb5";
            iconBox.Child = new TextBlock { Text = iconStr, FontSize = 16, Foreground = Br(BG), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(iconBox, 1); grid.Children.Add(iconBox);

            // Col 2: Title & Subtitle Stack (DisplayName without underscores on preview)
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var titleTB = new TextBlock { Text = f.DisplayName, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Br(TEXT_C), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = f.FileName };

            var subStack = new StackPanel { Orientation = Orientation.Horizontal };
            string lenText = f.IsShortSfx ? "⚡ Corto" : "🎵 Largo";
            subStack.Children.Add(new TextBlock { Text = lenText + " • ", FontSize = 11, Foreground = Br(TEXTMUTED) });
            subStack.Children.Add(new TextBlock { Text = f.FileName, FontSize = 11, Foreground = Br(TEXTDIM), TextTrimming = TextTrimming.CharacterEllipsis });
            textStack.Children.Add(titleTB);
            textStack.Children.Add(subStack);
            Grid.SetColumn(textStack, 2); grid.Children.Add(textStack);

            // Col 3: Subfolder / Category
            var folderTB = new TextBlock { Text = f.SubCategory, FontSize = 11, Foreground = Br(TEXTMUTED), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(folderTB, 3); grid.Children.Add(folderTB);

            // Col 4: File Size
            var sizeBlock = new TextBlock { Text = TagEngine.FormatFileSize(f.FileSizeBytes), FontSize = 11, Foreground = Br(TEXTDIM), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(sizeBlock, 4); grid.Children.Add(sizeBlock);

            // Col 5: Match Score or Confidence
            if (isSearching)
            {
                var scoreBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(30, 88, 166, 255)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
                scoreBorder.Child = new TextBlock { Text = "⭐ " + f.MatchScore.ToString("F1") + " / 10", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Br(ACCENT) };
                Grid.SetColumn(scoreBorder, 5); grid.Children.Add(scoreBorder);
            }
            else if (f.NeedsReview)
            {
                int confPct = (int)(f.ConfidenceScore * 100);
                var revBadge = new Border { Background = Br(WARNING_C), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
                revBadge.Child = new TextBlock { Text = "⚠️ Revisa (" + confPct + "%)", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Br(BG) };
                Grid.SetColumn(revBadge, 5); grid.Children.Add(revBadge);
            }
            else
            {
                int confPct = (int)(f.ConfidenceScore * 100);
                var okBadge = new Border { Background = new SolidColorBrush(Color.FromArgb(30, 57, 211, 83)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
                okBadge.Child = new TextBlock { Text = confPct.ToString() + "% Confianza", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Br(ACCENTGREEN) };
                Grid.SetColumn(okBadge, 5); grid.Children.Add(okBadge);
            }

            // Col 6: Duration
            var durTB = new TextBlock { Text = FormatTime(TimeSpan.FromSeconds(f.DurationSeconds)), FontSize = 11, Foreground = Br(TEXTMUTED), VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            Grid.SetColumn(durTB, 6); grid.Children.Add(durTB);

            // Col 7: Action Buttons
            SfxFile capturedF = f;
            var ap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var bPlay = new Button { Content = "\u25b6", FontSize = 13, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Br(ACCENT), Cursor = Cursors.Hand, Padding = new Thickness(6, 2, 6, 2), ToolTip = "Reproducir" };
            bPlay.Click += delegate(object s, RoutedEventArgs e) { e.Handled = true; SelectAndPlay(capturedF); };
            var bFav = new Button { Content = f.IsFavorite ? "\u2b50" : "\u2606", FontSize = 13, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = f.IsFavorite ? Br(WARNING_C) : Br(TEXTDIM), Cursor = Cursors.Hand, Padding = new Thickness(4, 2, 4, 2), ToolTip = "Favorito" };
            bFav.Click += delegate(object s, RoutedEventArgs e) { e.Handled = true; ToggleFavorite(capturedF); RefreshFileList(); };
            var bCopy = new Button { Content = "\ud83d\udccb", FontSize = 13, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Br(TEXTDIM), Cursor = Cursors.Hand, Padding = new Thickness(4, 2, 4, 2), ToolTip = "Copiar archivo" };
            bCopy.Click += delegate(object s, RoutedEventArgs e) { e.Handled = true; CopyFileToClipboard(capturedF); };
            ap.Children.Add(bPlay); ap.Children.Add(bFav); ap.Children.Add(bCopy);
            Grid.SetColumn(ap, 7); grid.Children.Add(ap);

            border.Child = grid;
            return border;
        }

        // ─── Auto-Organization Action ──────────────────────────────────────────

        void RunAutoOrganization()
        {
            if (db.Libraries.Count == 0)
            {
                MessageBox.Show("Primero importa una carpeta principal de audios.", "Etern Audio", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var rootLib = db.Libraries[0];
            progressModalOverlay.Visibility = Visibility.Visible;
            modalProgressBar.Value = 0;
            modalProgressPercent.Text = "0%";
            modalProgressStatus.Text = "Analizando biblioteca y categorizando audios...";

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object s, DoWorkEventArgs e)
            {
                int count = FileOrganizer.PerformAutoOrganization(rootLib.RootPath, delegate(int current, int total, string file)
                {
                    Dispatcher.BeginInvoke(new Action(delegate()
                    {
                        int pct = total > 0 ? (int)((current / (double)total) * 100) : 100;
                        modalProgressBar.Value = pct;
                        modalProgressPercent.Text = pct.ToString() + "%";
                        modalProgressStatus.Text = "(" + current.ToString() + "/" + total.ToString() + ") " + file;
                    }));
                });
                e.Result = count;
            };
            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                int count = e.Result != null ? (int)e.Result : 0;
                Dispatcher.BeginInvoke(new Action(delegate()
                {
                    progressModalOverlay.Visibility = Visibility.Collapsed;
                    ScanLibrary(rootLib);
                    CheckUnorganizedBanner();

                    if (count == 0)
                    {
                        MessageBox.Show("✅ Todos los " + db.Files.Count + " archivos de tu biblioteca ya están perfectamente categorizados y renombrados con barra baja.", "Etern Audio", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("✅ Organización completada con éxito. Se han reorganizado o renombrado " + count + " archivos en español.", "Etern Audio", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }));
            };
            worker.RunWorkerAsync();
        }

        // ─── Library Management ──────────────────────────────────────────────────

        void AddLibrary()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Selecciona la carpeta de efectos de sonido", ShowNewFolderButton = false, SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = dlg.SelectedPath;
                if (db.Libraries.Any(delegate(SfxLibrary l) { return l.RootPath == path; })) { MessageBox.Show("Esta carpeta ya está añadida."); return; }
                var lib = new SfxLibrary { Name = System.IO.Path.GetFileName(path), RootPath = path };
                db.Libraries.Add(lib); Storage.Save(db); RefreshSidebar(); ScanLibrary(lib);
            }
        }

        void ScanLibrary(SfxLibrary lib)
        {
            if (isScanning) return;
            isScanning = true;
            if (lblScanStatus != null) lblScanStatus.Text = "Escaneando...";
            db.Files.RemoveAll(delegate(SfxFile f) { return f.LibraryId == lib.Id; });
            var worker = new BackgroundWorker { WorkerReportsProgress = true };
            int found = 0; var newFiles = new List<SfxFile>();
            worker.DoWork += delegate(object s, DoWorkEventArgs e) { ScanFolder(lib.RootPath, lib.Id, newFiles, ref found, worker); };
            worker.ProgressChanged += delegate(object s, ProgressChangedEventArgs e)
            {
                int currentFound = e.ProgressPercentage;
                Dispatcher.BeginInvoke(new Action(delegate()
                {
                    if (lblScanStatus != null) lblScanStatus.Text = "Escaneando... " + currentFound.ToString() + " archivos";
                }));
            };
            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                Dispatcher.BeginInvoke(new Action(delegate()
                {
                    db.Files.AddRange(newFiles); lib.FileCount = newFiles.Count; lib.LastScannedTicks = DateTime.Now.Ticks;
                    Storage.Save(db); RebuildIndex(); RefreshFileList(); RefreshSidebar(); isScanning = false;
                    CheckUnorganizedBanner();
                    if (lblScanStatus != null) lblScanStatus.Text = db.Files.Count.ToString() + " archivos en total";
                }));
            };
            worker.RunWorkerAsync();
        }

        void ScanFolder(string path, string libId, List<SfxFile> results, ref int count, BackgroundWorker worker)
        {
            try
            {
                foreach (var file in Directory.GetFiles(path))
                    if (TagEngine.IsAudioFile(file)) { var sfx = TagEngine.AutoTag(file); sfx.LibraryId = libId; results.Add(sfx); count++; if (count % 50 == 0) worker.ReportProgress(count); }
                foreach (var dir in Directory.GetDirectories(path)) ScanFolder(dir, libId, results, ref count, worker);
            }
            catch { }
        }

        void RescanAllLibraries() { foreach (var lib in db.Libraries.ToList()) ScanLibrary(lib); }
        void RebuildIndex() { searchEngine.BuildIndex(db.Files); }

        // ─── Player ──────────────────────────────────────────────────────────────

        void SetupPlayer()
        {
            mediaPlayer.Volume = 0.85;
            mediaPlayer.MediaEnded += delegate(object s, EventArgs e) { isPlaying = false; btnPlayPause.Content = "\u25b6"; slProgress.Value = 0; lblCurrentTime.Text = "0:00"; };
            playerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            playerTimer.Tick += delegate(object s, EventArgs e)
            {
                if (isPlaying && !isDraggingSlider && mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    double total = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    if (total > 0) { slProgress.Maximum = total; slProgress.Value = mediaPlayer.Position.TotalSeconds; lblCurrentTime.Text = FormatTime(mediaPlayer.Position); lblTotalTime.Text = FormatTime(mediaPlayer.NaturalDuration.TimeSpan); }
                }
            };
            playerTimer.Start();
        }

        void SelectAndPlay(SfxFile f)
        {
            selectedFile = f;
            foreach (ListViewItem item in lstFiles.Items) if ((item.Tag as SfxFile) != null && (item.Tag as SfxFile).Id == f.Id) { lstFiles.SelectedItem = item; break; }
            PlayFile(f);
        }

        void PlayFile(SfxFile f)
        {
            if (f == null || !File.Exists(f.FilePath)) return;
            try { mediaPlayer.Stop(); mediaPlayer.Close(); mediaPlayer.Open(new Uri(f.FilePath)); mediaPlayer.Play(); isPlaying = true; btnPlayPause.Content = "\u23f8"; lblNowPlaying.Text = f.DisplayName; slProgress.Value = 0; f.PlayCount++; Storage.Save(db); }
            catch (Exception ex) { lblNowPlaying.Text = "Error: " + ex.Message; }
        }

        void TogglePlayPause()
        {
            if (selectedFile == null) return;
            if (isPlaying) { mediaPlayer.Pause(); isPlaying = false; btnPlayPause.Content = "\u25b6"; }
            else { mediaPlayer.Play(); isPlaying = true; btnPlayPause.Content = "\u23f8"; }
        }

        void StopPlayer() { mediaPlayer.Stop(); isPlaying = false; btnPlayPause.Content = "\u25b6"; slProgress.Value = 0; lblCurrentTime.Text = "0:00"; }
        void NavigateFile(int dir) { if (filteredFiles.Count == 0) return; int idx = selectedFile == null ? -1 : filteredFiles.FindIndex(delegate(SfxFile f) { return f.Id == selectedFile.Id; }); SelectAndPlay(filteredFiles[(idx + dir + filteredFiles.Count) % filteredFiles.Count]); }

        // ─── File Ops & Robust Clipboard Copier ──────────────────────────────────

        void CopyFileToClipboard(SfxFile f)
        {
            if (f == null || !File.Exists(f.FilePath)) return;
            try
            {
                var data = new DataObject();
                var sc = new StringCollection();
                sc.Add(f.FilePath);
                data.SetFileDropList(sc);
                data.SetText(f.FilePath);
                Clipboard.SetDataObject(data, true);
                if (lblScanStatus != null) lblScanStatus.Text = "Copiado al portapapeles: " + f.FileName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al copiar: " + ex.Message);
            }
        }

        void OpenSelectedInExplorer() { if (selectedFile != null && File.Exists(selectedFile.FilePath)) System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + selectedFile.FilePath + "\""); }
        void ToggleFavorite(SfxFile f) { if (f == null) return; f.IsFavorite = !f.IsFavorite; Storage.Save(db); }
        void PlaySelectedFile() { if (selectedFile != null) PlayFile(selectedFile); }

        // ─── Drag & Drop OUT (to DaVinci Resolve etc) ────────────────────────────

        Point dragStart;
        bool draggingFile;
        void LstFiles_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && selectedFile != null && !draggingFile)
            {
                Point pos = e.GetPosition(null);
                if (Math.Abs(pos.X - dragStart.X) > 4 || Math.Abs(pos.Y - dragStart.Y) > 4)
                {
                    draggingFile = true;
                    var data = new DataObject(DataFormats.FileDrop, new string[] { selectedFile.FilePath });
                    DragDrop.DoDragDrop(lstFiles, data, DragDropEffects.Copy);
                    draggingFile = false;
                }
            }
            if (e.LeftButton == MouseButtonState.Pressed) dragStart = e.GetPosition(null);
        }

        // ─── Event Handlers ──────────────────────────────────────────────────────

        void LstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstFiles.SelectedItem is ListViewItem && (lstFiles.SelectedItem as ListViewItem).Tag is SfxFile)
            {
                selectedFile = (SfxFile)(lstFiles.SelectedItem as ListViewItem).Tag;
                lblNowPlaying.Text = selectedFile.DisplayName;
            }
        }

        void LstFiles_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Space) { TogglePlayPause(); e.Handled = true; } if (e.Key == Key.Return) PlaySelectedFile(); }

        void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { txtSearch.Text = ""; txtSearch.Focus(); }
            if (e.Key == Key.F5) RescanAllLibraries();
            if (e.Key == Key.Space && !(FocusManager.GetFocusedElement(this) is TextBox)) { TogglePlayPause(); e.Handled = true; }
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C) CopyFileToClipboard(selectedFile);
        }

        // ─── Menu Actions ────────────────────────────────────────────────────────

        void HandleMenuAction(string action)
        {
            if (action == "Importar carpeta de audios...") AddLibrary();
            else if (action == "Re-escanear librerías") RescanAllLibraries();
            else if (action == "Verificar y Auto-Organizar archivos") RunAutoOrganization();
            else if (action == "Salir") Close();
            else if (action == "Copiar ruta del archivo") { if (selectedFile != null) Clipboard.SetText(selectedFile.FilePath); }
            else if (action == "Copiar archivo al portapapeles") CopyFileToClipboard(selectedFile);
            else if (action == "Abrir en explorador") OpenSelectedInExplorer();
            else if (action == "Marcar favorito") { if (selectedFile != null) { selectedFile.IsFavorite = true; Storage.Save(db); RefreshFileList(); } }
            else if (action == "Desmarcar favorito") { if (selectedFile != null) { selectedFile.IsFavorite = false; Storage.Save(db); RefreshFileList(); } }
            else if (action == "Todos los archivos") { activeFolderPath = null; activeCategory = null; showFavoritesOnly = false; RefreshFileList(); RefreshSidebar(); }
            else if (action == "Solo audios cortos (<30s)") { activeLengthFilter = 1; RefreshFileList(); }
            else if (action == "Solo música / largos (>=30s)") { activeLengthFilter = 2; RefreshFileList(); }
            else if (action == "Mostrar panel lateral") sidebarBorder.Visibility = Visibility.Visible;
            else if (action == "Ocultar panel lateral") sidebarBorder.Visibility = Visibility.Collapsed;
            else if (action == "Re-etiquetar todo")
            {
                foreach (var f in db.Files) { var t = TagEngine.AutoTag(f.FilePath); f.Tags = t.Tags; f.Category = t.Category; }
                Storage.Save(db); RebuildIndex(); RefreshFileList();
            }
            else if (action == "Eliminar entradas huerfanas")
            {
                db.Files.RemoveAll(delegate(SfxFile f) { return !File.Exists(f.FilePath); }); Storage.Save(db); RebuildIndex(); RefreshFileList();
            }
            else if (action == "Abrir carpeta de datos")
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EternAudio");
                if (Directory.Exists(dir)) System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            else if (action == "Exportar lista a CSV")
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv", FileName = "etern_audio_library.csv" };
                if (dlg.ShowDialog() == true)
                {
                    var lines = new List<string> { "Nombre,Ruta,Categoria,Tags,Tamano,Duracion" };
                    foreach (var f in db.Files) lines.Add("\"" + f.DisplayName + "\",\"" + f.FilePath + "\",\"" + f.Category + "\",\"" + string.Join(" ", f.Tags) + "\",\"" + TagEngine.FormatFileSize(f.FileSizeBytes) + "\",\"" + Math.Round(f.DurationSeconds) + "s\"");
                    File.WriteAllLines(dlg.FileName, lines, System.Text.Encoding.UTF8);
                    MessageBox.Show("Exportado: " + dlg.FileName);
                }
            }
            else if (action == "Atajos de teclado") MessageBox.Show("Space = Play/Pausa\nArrow Up/Down = Navegar lista\nCtrl+C = Copiar archivo\nEsc = Limpiar busqueda\nF5 = Re-escanear\nDoble clic = Reproducir", "Atajos de teclado");
            else if (action == "Acerca de Etern Audio v1.0") MessageBox.Show("Etern Audio v1.0\nGestor y Organizador de Efectos de Sonido\nAuto-renombrado en Español + Busqueda Bilingue EN/ES\nFiltros por duracion (Cortos vs Largos)", "Acerca de Etern Audio");
        }

        // ─── Menubar ────────────────────────────────────────────────────────────

        void SetupMenuHideTimer() { menuHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) }; menuHideTimer.Tick += delegate(object s, EventArgs e) { menuHideTimer.Stop(); HideMenuBar(); }; }
        void ShowMenuBar() { menuHideTimer.Stop(); if (menuBarVisible) return; menuBarBorder.Height = 30; ((Grid)menuBarBorder.Parent).RowDefinitions[1].Height = new GridLength(30); menuBarVisible = true; }
        void HideMenuBar() { if (!menuBarVisible) return; menuBarBorder.Height = 0; ((Grid)menuBarBorder.Parent).RowDefinitions[1].Height = new GridLength(0); menuBarVisible = false; }
        void ToggleSidebar() { isSidebarCollapsed = !isSidebarCollapsed; contentGrid.ColumnDefinitions[0].Width = isSidebarCollapsed ? new GridLength(0) : new GridLength(280); sidebarBorder.Visibility = isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible; }

        // ─── Helpers ────────────────────────────────────────────────────────────

        string FormatTime(TimeSpan t) { if (t.Hours > 0) return t.Hours.ToString() + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00"); return t.Minutes.ToString() + ":" + t.Seconds.ToString("00"); }

        Button MakeWinBtn(string content, Color hoverColor, Action onClick)
        {
            var btn = new Button { Content = content, Width = 42, Height = 42, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Br(TEXTMUTED), FontSize = 13, Cursor = Cursors.Hand };
            btn.MouseEnter += delegate(object s, MouseEventArgs e) { ((Button)s).Background = hoverColor == Colors.Transparent ? Br(CARDHOVER) : new SolidColorBrush(hoverColor); ((Button)s).Foreground = Br(TEXT_C); };
            btn.MouseLeave += delegate(object s, MouseEventArgs e) { ((Button)s).Background = Brushes.Transparent; ((Button)s).Foreground = Br(TEXTMUTED); };
            btn.Click += delegate(object s, RoutedEventArgs e) { onClick(); };
            return btn;
        }
    }
}

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

        // ─── Colors (Identical to Etern-Notes / EternSynth) ───────────────────
        static readonly Color BG        = Color.FromRgb(18, 18, 18);     // #121212
        static readonly Color SIDEBAR   = Color.FromRgb(26, 26, 26);     // #1a1a1a
        static readonly Color CARD      = Color.FromRgb(33, 33, 33);     // #212121
        static readonly Color CARDHOVER = Color.FromRgb(42, 42, 42);     // #2a2a2a
        static readonly Color BORDER_C  = Color.FromRgb(48, 48, 48);     // #303030
        static readonly Color ACCENT    = Color.FromRgb(88, 166, 255);   // #58a6ff (Etern Blue)
        static readonly Color ACCENTGREEN= Color.FromRgb(57, 211, 83);   // #39d353
        static readonly Color TEXT_C    = Colors.White;
        static readonly Color TEXTMUTED = Color.FromRgb(150, 150, 150); // #969696
        static readonly Color TEXTDIM   = Color.FromRgb(100, 100, 100);
        static readonly Color WARNING_C = Color.FromRgb(240, 136, 62);  // #f0883e

        static SolidColorBrush Br(Color c) { return new SolidColorBrush(c); }
        static SolidColorBrush BrH(string hex) { return (SolidColorBrush)(new BrushConverter().ConvertFrom(hex)); }

        // ─── State ──────────────────────────────────────────────────────────────
        SfxDatabase db;
        SearchEngine searchEngine = new SearchEngine();
        List<SfxFile> filteredFiles = new List<SfxFile>();
        SfxFile selectedFile;
        string activeCategory = null;
        string activeLibraryId = null;
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
        TextBlock lblResultCount;
        TextBlock lblNowPlaying;
        TextBlock lblCurrentTime;
        TextBlock lblTotalTime;
        Slider slProgress;
        Slider slVolume;
        Button btnPlayPause;
        StackPanel sidebarLibraryPanel;
        StackPanel sidebarCategoryPanel;
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
            Width = 1340; Height = 820;
            MinWidth = 900; MinHeight = 600;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Title = "Etern Audio";

            db = Storage.Load();
            BuildUI();
            SetupPlayer();
            SetupMenuHideTimer();
            RefreshSidebar();

            KeyDown += Window_KeyDown;

            // Auto-detect "Efectos Sonido" or "Efectos de sonido" on Desktop if no libraries exist
            CheckAndAutoImportDesktopFolder();

            RebuildIndex();
            RefreshFileList();

            if (db.Libraries.Count > 0)
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate() { RescanAllLibraries(); }));
        }

        void CheckAndAutoImportDesktopFolder()
        {
            if (db.Libraries.Count == 0)
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
                        var lib = new SfxLibrary
                        {
                            Name = System.IO.Path.GetFileName(candidate),
                            RootPath = candidate
                        };
                        db.Libraries.Add(lib);
                        Storage.Save(db);
                        RefreshSidebar();
                        ScanLibrary(lib);
                        break;
                    }
                }
            }
        }

        // ─── UI Building ────────────────────────────────────────────────────────

        void BuildUI()
        {
            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Br(BG),
                BorderBrush = Br(BORDER_C),
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0.6, Color = Colors.Black }
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = CreateTitleBar();
            Grid.SetRow(titleBar, 0);
            mainGrid.Children.Add(titleBar);

            menuBarBorder = CreateMenuBar();
            Grid.SetRow(menuBarBorder, 1);
            mainGrid.Children.Add(menuBarBorder);

            contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(contentGrid, 2);
            mainGrid.Children.Add(contentGrid);

            sidebarBorder = CreateSidebar();
            Grid.SetColumn(sidebarBorder, 0);
            contentGrid.Children.Add(sidebarBorder);

            var divider = new Border { Background = Br(BORDER_C), Width = 1 };
            Grid.SetColumn(divider, 1);
            contentGrid.Children.Add(divider);

            var mainArea = CreateMainArea();
            Grid.SetColumn(mainArea, 2);
            contentGrid.Children.Add(mainArea);

            outerBorder.Child = mainGrid;
            Content = outerBorder;
        }

        Grid CreateTitleBar()
        {
            var g = new Grid { Background = Br(SIDEBAR) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnH = MakeWinBtn("=", Colors.Transparent, delegate() { ToggleSidebar(); });
            btnH.FontSize = 14; btnH.ToolTip = "Colapsar panel lateral";
            Grid.SetColumn(btnH, 0);
            g.Children.Add(btnH);

            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            titlePanel.Children.Add(new TextBlock { Text = "Etern Audio", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Br(TEXT_C), VerticalAlignment = VerticalAlignment.Center });
            titlePanel.Children.Add(new TextBlock { Text = " \u25cf", FontSize = 10, Foreground = Br(ACCENT), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(titlePanel, 1);
            g.Children.Add(titlePanel);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            btnPanel.Children.Add(MakeWinBtn("\u2500", Colors.Transparent, delegate() { WindowState = WindowState.Minimized; }));
            btnPanel.Children.Add(MakeWinBtn("\u25a1", Colors.Transparent, delegate() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }));
            btnPanel.Children.Add(MakeWinBtn("\u2715", Color.FromRgb(239, 68, 68), delegate() { Close(); }));
            Grid.SetColumn(btnPanel, 2);
            g.Children.Add(btnPanel);

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
            AddMenu(panel, "Ver",     new string[] { "Todos los archivos", "Solo favoritos", "---", "Mostrar panel lateral", "Ocultar panel lateral" });
            AddMenu(panel, "Herramientas", new string[] { "Re-etiquetar todo", "Eliminar entradas huerfanas", "---", "Abrir carpeta de datos" });
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

            var libHeader = new Grid { Margin = new Thickness(0, 14, 0, 6) };
            libHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            libHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var txtLibTitle = new TextBlock { Text = "CARPETAS", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Br(TEXTMUTED), Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(txtLibTitle, 0);
            libHeader.Children.Add(txtLibTitle);

            var btnAdd = new Button { Content = "+ Importar", FontSize = 11, FontWeight = FontWeights.SemiBold, Background = new SolidColorBrush(Color.FromArgb(40, 88, 166, 255)), BorderThickness = new Thickness(1), BorderBrush = Br(ACCENT), Foreground = Br(ACCENT), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(6, 2, 6, 2), ToolTip = "Importar carpeta de efectos de sonido" };
            btnAdd.Click += delegate(object s, RoutedEventArgs e) { AddLibrary(); };
            Grid.SetColumn(btnAdd, 1);
            libHeader.Children.Add(btnAdd);
            Grid.SetRow(libHeader, 0);
            grid.Children.Add(libHeader);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var content = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            sidebarLibraryPanel = new StackPanel();
            content.Children.Add(sidebarLibraryPanel);
            content.Children.Add(new Border { Height = 1, Background = Br(BORDER_C), Margin = new Thickness(10, 10, 10, 10) });
            content.Children.Add(new TextBlock { Text = "CATEGORIAS", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Br(TEXTMUTED), Margin = new Thickness(14, 0, 14, 8) });
            sidebarCategoryPanel = new StackPanel();
            content.Children.Add(sidebarCategoryPanel);
            scroll.Content = content;
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            var footer = new Border { Background = Br(CARD), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(14, 8, 14, 8) };
            lblScanStatus = new TextBlock { Text = "Listo", FontSize = 10, Foreground = Br(TEXTMUTED) };
            footer.Child = lblScanStatus;
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            border.Child = grid;
            return border;
        }

        Grid CreateMainArea()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) });

            var searchBar = CreateSearchBar();
            Grid.SetRow(searchBar, 0); grid.Children.Add(searchBar);

            var statsRow = new Border { Background = Br(SIDEBAR), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(16, 6, 16, 6) };
            var sg = new Grid();
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            lblResultCount = new TextBlock { Text = "0 archivos", FontSize = 12, Foreground = Br(TEXTMUTED), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lblResultCount, 0); sg.Children.Add(lblResultCount);
            var sortPanel = new StackPanel { Orientation = Orientation.Horizontal };
            sortPanel.Children.Add(new TextBlock { Text = "Ordenar: ", FontSize = 11, Foreground = Br(TEXTDIM), VerticalAlignment = VerticalAlignment.Center });
            foreach (var so in new string[] { "Nombre", "Categoria", "Tamano" })
            {
                var btn = new Button { Content = so, FontSize = 11, Foreground = Br(TEXTMUTED), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(6, 2, 6, 2) };
                string cap = so;
                btn.Click += delegate(object s, RoutedEventArgs e) { SortFiles(cap); };
                btn.MouseEnter += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(ACCENT); };
                btn.MouseLeave += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(TEXTMUTED); };
                sortPanel.Children.Add(btn);
            }
            Grid.SetColumn(sortPanel, 1); sg.Children.Add(sortPanel);
            statsRow.Child = sg;
            Grid.SetRow(statsRow, 1); grid.Children.Add(statsRow);

            lstFiles = new ListView { Background = Brushes.Transparent, BorderThickness = new Thickness(0), SelectionMode = SelectionMode.Single };
            lstFiles.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            lstFiles.SelectionChanged += LstFiles_SelectionChanged;
            lstFiles.MouseDoubleClick += delegate(object s, MouseButtonEventArgs e) { PlaySelectedFile(); };
            lstFiles.MouseMove += LstFiles_MouseMove;
            lstFiles.KeyDown += LstFiles_KeyDown;

            var itemStyle = new Style(typeof(ListViewItem));
            itemStyle.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Br(BG)));
            itemStyle.Setters.Add(new Setter(ListViewItem.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListViewItem.MarginProperty, new Thickness(0, 1, 0, 0)));
            itemStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
            var hoverT = new Trigger { Property = ListViewItem.IsMouseOverProperty, Value = true };
            hoverT.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Br(CARDHOVER)));
            var selT = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
            selT.Setters.Add(new Setter(ListViewItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 88, 166, 255))));
            itemStyle.Triggers.Add(hoverT); itemStyle.Triggers.Add(selT);
            lstFiles.ItemContainerStyle = itemStyle;

            var listBorder = new Border { Child = new ScrollViewer { Content = lstFiles, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Background = Br(BG) };
            Grid.SetRow(listBorder, 2); grid.Children.Add(listBorder);

            var divBorder = new Border { Height = 1, Background = Br(BORDER_C) };
            Grid.SetRow(divBorder, 3); grid.Children.Add(divBorder);

            var player = CreatePlayerPanel();
            Grid.SetRow(player, 4); grid.Children.Add(player);

            return grid;
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

            var wm = new TextBlock { Text = "Buscar sonidos... (explosion, lluvia, impacto, meme, risa...)", Foreground = Br(TEXTDIM), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
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

        Border CreatePlayerPanel()
        {
            var border = new Border { Background = Br(SIDEBAR), BorderBrush = Br(BORDER_C), BorderThickness = new Thickness(0, 1, 0, 0) };
            var grid = new Grid { Margin = new Thickness(14, 8, 14, 8) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            lblNowPlaying = new TextBlock { Text = "Ningun archivo seleccionado", FontSize = 12, Foreground = Br(TEXTMUTED), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 4) };
            Grid.SetRow(lblNowPlaying, 0); grid.Children.Add(lblNowPlaying);

            var pr = new Grid();
            pr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            lblCurrentTime = new TextBlock { Text = "0:00", FontSize = 11, Foreground = Br(TEXTMUTED), VerticalAlignment = VerticalAlignment.Center, Width = 38 };
            Grid.SetColumn(lblCurrentTime, 0); pr.Children.Add(lblCurrentTime);
            slProgress = new Slider { Minimum = 0, Maximum = 1, Value = 0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
            slProgress.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { isDraggingSlider = true; };
            slProgress.PreviewMouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { isDraggingSlider = false; if (isPlaying) mediaPlayer.Position = TimeSpan.FromSeconds(slProgress.Value); };
            Grid.SetColumn(slProgress, 1); pr.Children.Add(slProgress);
            lblTotalTime = new TextBlock { Text = "0:00", FontSize = 11, Foreground = Br(TEXTMUTED), VerticalAlignment = VerticalAlignment.Center, Width = 38, TextAlignment = TextAlignment.Right };
            Grid.SetColumn(lblTotalTime, 2); pr.Children.Add(lblTotalTime);
            Grid.SetRow(pr, 1); grid.Children.Add(pr);

            var cr = new Grid();
            cr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            cr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var volPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            volPanel.Children.Add(new TextBlock { Text = "\ud83d\udd0a", FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            slVolume = new Slider { Minimum = 0, Maximum = 1, Value = 0.85, Width = 80, VerticalAlignment = VerticalAlignment.Center };
            slVolume.ValueChanged += delegate(object s, RoutedPropertyChangedEventArgs<double> e) { mediaPlayer.Volume = slVolume.Value; };
            volPanel.Children.Add(slVolume);
            Grid.SetColumn(volPanel, 0); cr.Children.Add(volPanel);

            var pc = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            pc.Children.Add(MakeCtrlBtn("\u23ee", 14, delegate() { NavigateFile(-1); }));
            btnPlayPause = new Button { Content = "\u25b6", FontSize = 18, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Br(ACCENT), Cursor = Cursors.Hand, Padding = new Thickness(12, 2, 12, 2) };
            btnPlayPause.Click += delegate(object s, RoutedEventArgs e) { TogglePlayPause(); }; pc.Children.Add(btnPlayPause);
            pc.Children.Add(MakeCtrlBtn("\u23f9", 14, delegate() { StopPlayer(); }));
            pc.Children.Add(MakeCtrlBtn("\u23ed", 14, delegate() { NavigateFile(1); }));
            Grid.SetColumn(pc, 1); cr.Children.Add(pc);

            var ap = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var btnCopy = new Button { Content = "Copiar", FontSize = 11, Foreground = Br(TEXTMUTED), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(8, 2, 8, 2) };
            btnCopy.Click += delegate(object s, RoutedEventArgs e) { CopyFileToClipboard(selectedFile); };
            btnCopy.MouseEnter += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(ACCENT); };
            btnCopy.MouseLeave += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(TEXTMUTED); };
            var btnOpen = new Button { Content = "Abrir", FontSize = 11, Foreground = Br(TEXTMUTED), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(8, 2, 8, 2) };
            btnOpen.Click += delegate(object s, RoutedEventArgs e) { OpenSelectedInExplorer(); };
            btnOpen.MouseEnter += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(ACCENT); };
            btnOpen.MouseLeave += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(TEXTMUTED); };
            ap.Children.Add(btnCopy); ap.Children.Add(btnOpen);
            Grid.SetColumn(ap, 2); cr.Children.Add(ap);
            Grid.SetRow(cr, 2); grid.Children.Add(cr);

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

        // ─── Sidebar ────────────────────────────────────────────────────────────

        void RefreshSidebar()
        {
            sidebarLibraryPanel.Children.Clear();
            sidebarLibraryPanel.Children.Add(MakeSidebarItem("\ud83c\udfb5 Todos los archivos", activeLibraryId == null && !showFavoritesOnly, delegate() { activeLibraryId = null; showFavoritesOnly = false; RefreshFileList(); RefreshSidebar(); }));
            sidebarLibraryPanel.Children.Add(MakeSidebarItem("\u2b50 Favoritos", showFavoritesOnly, delegate() { showFavoritesOnly = !showFavoritesOnly; activeLibraryId = null; RefreshFileList(); RefreshSidebar(); }));

            if (db.Libraries.Count > 0)
                sidebarLibraryPanel.Children.Add(new TextBlock { Text = "\u2500\u2500\u2500 CARPETAS \u2500\u2500\u2500", FontSize = 10, Foreground = Br(TEXTDIM), Margin = new Thickness(14, 8, 14, 4) });

            foreach (var lib in db.Libraries)
            {
                var cap = lib;
                var iGrid = new Grid();
                iGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                iGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var libItem = MakeSidebarItem("\ud83d\udcc2 " + lib.Name, activeLibraryId == lib.Id, delegate() { activeLibraryId = cap.Id; showFavoritesOnly = false; RefreshFileList(); RefreshSidebar(); });
                iGrid.Children.Add(libItem);
                var btnR = new Button { Content = "\u21ba", FontSize = 13, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Br(TEXTDIM), Cursor = Cursors.Hand, Padding = new Thickness(4) };
                btnR.Click += delegate(object s, RoutedEventArgs e) { ScanLibrary(cap); };
                btnR.MouseEnter += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(ACCENT); };
                btnR.MouseLeave += delegate(object s, MouseEventArgs e) { ((Button)s).Foreground = Br(TEXTDIM); };
                Grid.SetColumn(btnR, 1); iGrid.Children.Add(btnR);
                sidebarLibraryPanel.Children.Add(iGrid);
            }

            sidebarCategoryPanel.Children.Clear();
            sidebarCategoryPanel.Children.Add(MakeSidebarItem("  Todas", activeCategory == null, delegate() { activeCategory = null; RefreshFileList(); RefreshSidebar(); }));

            foreach (var cat in TagEngine.AllCategories)
            {
                string captured = cat;
                string catColor = TagEngine.GetCategoryColor(cat);
                var catPanel = new StackPanel { Orientation = Orientation.Horizontal };
                catPanel.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = BrH(catColor), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 8, 0) });
                var lbl = new TextBlock { Text = cat, FontSize = 12, Foreground = activeCategory == cat ? Br(TEXT_C) : Br(TEXTMUTED), VerticalAlignment = VerticalAlignment.Center };
                catPanel.Children.Add(lbl);
                var catBtn = new Button { Content = catPanel, Background = activeCategory == cat ? Br(CARD) : Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Padding = new Thickness(0, 5, 14, 5), HorizontalContentAlignment = HorizontalAlignment.Left };
                catBtn.MouseEnter += delegate(object s, MouseEventArgs e) { if (activeCategory != captured) { ((Button)s).Background = Br(CARDHOVER); lbl.Foreground = Br(TEXT_C); } };
                catBtn.MouseLeave += delegate(object s, MouseEventArgs e) { if (activeCategory != captured) { ((Button)s).Background = Brushes.Transparent; lbl.Foreground = Br(TEXTMUTED); } };
                catBtn.Click += delegate(object s, RoutedEventArgs e) { activeCategory = activeCategory == captured ? null : captured; RefreshFileList(); RefreshSidebar(); };
                sidebarCategoryPanel.Children.Add(catBtn);
            }
        }

        FrameworkElement MakeSidebarItem(string text, bool isActive, Action onClick)
        {
            var btn = new Button { Content = text, Background = isActive ? new SolidColorBrush(Color.FromArgb(40, 88, 166, 255)) : Brushes.Transparent, BorderThickness = isActive ? new Thickness(2, 0, 0, 0) : new Thickness(0), BorderBrush = Br(ACCENT), Foreground = isActive ? Br(ACCENT) : Br(TEXTMUTED), FontSize = 12, Cursor = Cursors.Hand, Padding = new Thickness(14, 7, 14, 7), HorizontalContentAlignment = HorizontalAlignment.Left };
            btn.MouseEnter += delegate(object s, MouseEventArgs e) { if (!isActive) ((Button)s).Background = Br(CARDHOVER); };
            btn.MouseLeave += delegate(object s, MouseEventArgs e) { if (!isActive) ((Button)s).Background = Brushes.Transparent; };
            btn.Click += delegate(object s, RoutedEventArgs e) { onClick(); };
            return btn;
        }

        // ─── File List ───────────────────────────────────────────────────────────

        DispatcherTimer searchTimer;
        void SearchDebounce() { if (searchTimer != null) searchTimer.Stop(); searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) }; searchTimer.Tick += delegate(object s, EventArgs e) { searchTimer.Stop(); RefreshFileList(); }; searchTimer.Start(); }

        void RefreshFileList()
        {
            filteredFiles = searchEngine.Search(txtSearch != null ? txtSearch.Text : "", activeCategory, showFavoritesOnly, activeLibraryId);
            lstFiles.Items.Clear();
            foreach (var f in filteredFiles) { var item = new ListViewItem { Tag = f }; item.Content = BuildFileRow(f); lstFiles.Items.Add(item); }
            if (lblResultCount != null) lblResultCount.Text = filteredFiles.Count.ToString() + " archivo" + (filteredFiles.Count != 1 ? "s" : "");
        }

        UIElement BuildFileRow(SfxFile f)
        {
            var grid = new Grid { Margin = new Thickness(10, 6, 10, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

            string ext = System.IO.Path.GetExtension(f.FileName).ToLower();
            string icon = ext == ".mp3" ? "\ud83c\udfb5" : ext == ".wav" ? "\ud83d\udd0a" : ext == ".ogg" ? "\ud83c\udfb6" : "\ud83c\udfa7";
            var iconTB = new TextBlock { Text = icon, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(iconTB, 0); grid.Children.Add(iconTB);

            var nameBlock = new TextBlock { Text = f.DisplayName, FontSize = 13, Foreground = Br(TEXT_C), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = f.FileName };
            Grid.SetColumn(nameBlock, 1); grid.Children.Add(nameBlock);

            string catColor = TagEngine.GetCategoryColor(f.Category);
            var catBadge = new Border { Background = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)), BorderBrush = BrH(catColor), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) };
            catBadge.Child = new TextBlock { Text = f.Category, FontSize = 10, Foreground = BrH(catColor) };
            Grid.SetColumn(catBadge, 2); grid.Children.Add(catBadge);

            var tagPanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            int shown = 0;
            foreach (var tag in f.Tags)
            {
                if (shown >= 4) break; if (tag.Length > 14) continue;
                var pill = new Border { Background = Br(CARD), CornerRadius = new CornerRadius(8), Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 3, 0) };
                pill.Child = new TextBlock { Text = tag, FontSize = 10, Foreground = Br(TEXTMUTED) };
                tagPanel.Children.Add(pill); shown++;
            }
            Grid.SetColumn(tagPanel, 3); grid.Children.Add(tagPanel);

            var sizeBlock = new TextBlock { Text = TagEngine.FormatFileSize(f.FileSizeBytes), FontSize = 11, Foreground = Br(TEXTDIM), VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(sizeBlock, 4); grid.Children.Add(sizeBlock);

            SfxFile capturedF = f;
            var ap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var bPlay = new Button { Content = "\u25b6", FontSize = 12, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Br(ACCENT), Cursor = Cursors.Hand, Padding = new Thickness(5, 1, 5, 1) };
            bPlay.Click += delegate(object s, RoutedEventArgs e) { e.Handled = true; SelectAndPlay(capturedF); };
            var bFav = new Button { Content = f.IsFavorite ? "\u2b50" : "\u2606", FontSize = 12, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = f.IsFavorite ? Br(WARNING_C) : Br(TEXTDIM), Cursor = Cursors.Hand, Padding = new Thickness(4, 1, 4, 1) };
            bFav.Click += delegate(object s, RoutedEventArgs e) { e.Handled = true; ToggleFavorite(capturedF); RefreshFileList(); };
            var bCopy = new Button { Content = "\ud83d\udccb", FontSize = 12, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Br(TEXTDIM), Cursor = Cursors.Hand, Padding = new Thickness(4, 1, 4, 1) };
            bCopy.Click += delegate(object s, RoutedEventArgs e) { e.Handled = true; CopyFileToClipboard(capturedF); };
            ap.Children.Add(bPlay); ap.Children.Add(bFav); ap.Children.Add(bCopy);
            Grid.SetColumn(ap, 5); grid.Children.Add(ap);
            return grid;
        }

        void SortFiles(string by)
        {
            if (by == "Nombre") filteredFiles = filteredFiles.OrderBy(f => f.DisplayName).ToList();
            else if (by == "Categoria") filteredFiles = filteredFiles.OrderBy(f => f.Category).ThenBy(f => f.DisplayName).ToList();
            else if (by == "Tamano") filteredFiles = filteredFiles.OrderByDescending(f => f.FileSizeBytes).ToList();

            lstFiles.Items.Clear();
            foreach (var f in filteredFiles) { var item = new ListViewItem { Tag = f }; item.Content = BuildFileRow(f); lstFiles.Items.Add(item); }
        }

        // ─── Library Management ──────────────────────────────────────────────────

        void AddLibrary()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Selecciona la carpeta de efectos de sonido", ShowNewFolderButton = false, SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = dlg.SelectedPath;
                if (db.Libraries.Any(l => l.RootPath == path)) { MessageBox.Show("Esta carpeta ya está añadida."); return; }
                var lib = new SfxLibrary { Name = System.IO.Path.GetFileName(path), RootPath = path };
                db.Libraries.Add(lib); Storage.Save(db); RefreshSidebar(); ScanLibrary(lib);
            }
        }

        void ScanLibrary(SfxLibrary lib)
        {
            if (isScanning) return;
            isScanning = true;
            lblScanStatus.Text = "Escaneando...";
            db.Files.RemoveAll(f => f.LibraryId == lib.Id);
            var worker = new BackgroundWorker { WorkerReportsProgress = true };
            int found = 0; var newFiles = new List<SfxFile>();
            worker.DoWork += delegate(object s, DoWorkEventArgs e) { ScanFolder(lib.RootPath, lib.Id, newFiles, ref found, worker); };
            worker.ProgressChanged += delegate(object s, ProgressChangedEventArgs e) { lblScanStatus.Text = "Escaneando... " + e.ProgressPercentage.ToString() + " archivos"; };
            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                db.Files.AddRange(newFiles); lib.FileCount = newFiles.Count; lib.LastScannedTicks = DateTime.Now.Ticks;
                Storage.Save(db); RebuildIndex(); RefreshFileList(); isScanning = false;
                lblScanStatus.Text = db.Files.Count.ToString() + " archivos totales";
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
            try { mediaPlayer.Stop(); mediaPlayer.Close(); mediaPlayer.Open(new Uri(f.FilePath)); mediaPlayer.Play(); isPlaying = true; btnPlayPause.Content = "\u23f8"; lblNowPlaying.Text = "\u25b6  " + f.DisplayName + "  \u00b7  " + f.Category; slProgress.Value = 0; f.PlayCount++; Storage.Save(db); }
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

        // ─── File Ops ────────────────────────────────────────────────────────────

        void CopyFileToClipboard(SfxFile f) { if (f == null || !File.Exists(f.FilePath)) return; try { var sc = new StringCollection(); sc.Add(f.FilePath); Clipboard.SetFileDropList(sc); lblScanStatus.Text = "Copiado: " + f.FileName; } catch { } }
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
                lblNowPlaying.Text = selectedFile.DisplayName + "  \u00b7  " + selectedFile.Category + "  \u00b7  " + TagEngine.FormatFileSize(selectedFile.FileSizeBytes);
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
            else if (action == "Salir") Close();
            else if (action == "Copiar ruta del archivo") { if (selectedFile != null) Clipboard.SetText(selectedFile.FilePath); }
            else if (action == "Copiar archivo al portapapeles") CopyFileToClipboard(selectedFile);
            else if (action == "Abrir en explorador") OpenSelectedInExplorer();
            else if (action == "Marcar favorito") { if (selectedFile != null) { selectedFile.IsFavorite = true; Storage.Save(db); RefreshFileList(); } }
            else if (action == "Desmarcar favorito") { if (selectedFile != null) { selectedFile.IsFavorite = false; Storage.Save(db); RefreshFileList(); } }
            else if (action == "Todos los archivos") { activeCategory = null; activeLibraryId = null; showFavoritesOnly = false; RefreshFileList(); RefreshSidebar(); }
            else if (action == "Solo favoritos") { showFavoritesOnly = !showFavoritesOnly; RefreshFileList(); RefreshSidebar(); }
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
                    var lines = new List<string> { "Nombre,Ruta,Categoria,Tags,Tamano" };
                    foreach (var f in db.Files) lines.Add("\"" + f.DisplayName + "\",\"" + f.FilePath + "\",\"" + f.Category + "\",\"" + string.Join(" ", f.Tags.Take(8)) + "\",\"" + TagEngine.FormatFileSize(f.FileSizeBytes) + "\"");
                    File.WriteAllLines(dlg.FileName, lines, System.Text.Encoding.UTF8);
                    MessageBox.Show("Exportado: " + dlg.FileName);
                }
            }
            else if (action == "Atajos de teclado") MessageBox.Show("Space = Play/Pausa\nArrow Up/Down = Navegar lista\nCtrl+C = Copiar archivo\nEsc = Limpiar busqueda\nF5 = Re-escanear\nDoble clic = Reproducir", "Atajos de teclado");
            else if (action == "Acerca de Etern Audio v1.0") MessageBox.Show("Etern Audio v1.0\nGestor de efectos de sonido profesional\nBusqueda inteligente con 300+ sinonimos EN + ES\n21 categorias automaticas", "Acerca de Etern Audio");
        }

        // ─── Menubar ────────────────────────────────────────────────────────────

        void SetupMenuHideTimer() { menuHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) }; menuHideTimer.Tick += delegate(object s, EventArgs e) { menuHideTimer.Stop(); HideMenuBar(); }; }
        void ShowMenuBar() { menuHideTimer.Stop(); if (menuBarVisible) return; menuBarBorder.Height = 30; ((Grid)menuBarBorder.Parent).RowDefinitions[1].Height = new GridLength(30); menuBarVisible = true; }
        void HideMenuBar() { if (!menuBarVisible) return; menuBarBorder.Height = 0; ((Grid)menuBarBorder.Parent).RowDefinitions[1].Height = new GridLength(0); menuBarVisible = false; }
        void ToggleSidebar() { isSidebarCollapsed = !isSidebarCollapsed; contentGrid.ColumnDefinitions[0].Width = isSidebarCollapsed ? new GridLength(0) : new GridLength(240); sidebarBorder.Visibility = isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible; }

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

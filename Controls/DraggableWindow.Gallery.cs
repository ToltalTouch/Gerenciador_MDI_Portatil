using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace AvaloniaMdiApp
{
    public class GalleryCardState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string MediaType { get; set; } = "image"; // "image" ou "video"
        public Border CardBorder { get; init; } = null!;
        public Border ThumbContainer { get; init; } = null!;
        public Image? ThumbImage { get; init; }
        public TextBlock TitleLabel { get; init; } = null!;
        public Button BtnOpenWindow { get; init; } = null!;
        public Button BtnSetBg { get; init; } = null!;
        public Button BtnRemove { get; init; } = null!;
        public Bitmap? LoadedBitmap { get; set; }
        public DraggableWindow? OpenImageWindow { get; set; }
    }

    public partial class DraggableWindow
    {
        public event Action<DraggableWindow, GalleryCardState>? OnRequestOpenGalleryImage;
        public event Action<string>? OnRequestSetWorkspaceBackground;

        // Campos do Widget de Galeria
        private Border? _galleryToolbar;
        private Button? _btnGalleryAddFiles;
        private TextBox? _txtGalleryPath;
        private Button? _btnGalleryAddPath;
        private TextBlock? _lblGalleryCount;
        private WrapPanel? _galleryWrapPanel;
        private StackPanel? _galleryEmptyState;
        private TextBlock? _lblGalleryEmptyTitle;
        private TextBlock? _lblGalleryEmptySub;
        private readonly List<GalleryCardState> _galleryCards = new();

        // Campos da Janela de Imagem ("image")
        private Border? _imageToolbar;
        private Button? _btnImageFit;
        private Button? _btnImageZoomOut;
        private Button? _btnImageZoomReset;
        private Button? _btnImageZoomIn;
        private Button? _btnImageSetBg;
        private TextBlock? _lblImageInfo;
        private Image? _imageViewerControl;
        private ScrollViewer? _imageScrollViewer;
        private Bitmap? _singleImageBitmap;
        private string _singleImagePath = string.Empty;
        private double _imageZoomFactor = 1.0;
        private Stretch _imageStretchMode = Stretch.Uniform;

        // Campos da Janela de Vídeo ("video")
        private Border? _videoToolbar;
        private Button? _btnVideoReload;
        private Button? _btnVideoExternal;
        private TextBlock? _lblVideoInfo;
        private string _singleVideoPath = string.Empty;
        private string _tempVideoHtmlPath = string.Empty;

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".ogv"
        };

        public static bool EhArquivoDeVideo(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            string ext = Path.GetExtension(filePath);
            return !string.IsNullOrEmpty(ext) && VideoExtensions.Contains(ext);
        }

        private Control CriarConteudoGaleria(List<GalleryItemData>? initialItems)
        {
            var galleryDock = new DockPanel();

            _galleryToolbar = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 6)
            };

            var toolbarGrid = new Grid();
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _btnGalleryAddFiles = new Button
            {
                Content = AppIcons.WithText(AppIcons.Plus, "Adicionar Mídias...", 10.5, 5, ThemePalette.IconLightGray),
                Height = 28,
                Padding = new Thickness(10, 0),
                Margin = new Thickness(0, 0, 6, 0),
                CornerRadius = new CornerRadius(5),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnGalleryAddFiles, "Selecionar imagens ou vídeos do computador para a galeria");
            _btnGalleryAddFiles.Click += async (s, e) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.StorageProvider == null) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Selecionar Imagens ou Vídeos para a Galeria",
                    AllowMultiple = true,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Imagens e Vídeos")
                        {
                            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif", "*.mp4", "*.webm", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.m4v", "*.ogv" }
                        },
                        new FilePickerFileType("Imagens")
                        {
                            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif" },
                            MimeTypes = new[] { "image/png", "image/jpeg", "image/webp", "image/bmp", "image/gif" }
                        },
                        new FilePickerFileType("Vídeos")
                        {
                            Patterns = new[] { "*.mp4", "*.webm", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.m4v", "*.ogv" }
                        },
                        FilePickerFileTypes.All
                    }
                });

                int adicionadas = 0;
                foreach (var file in files)
                {
                    string? localPath = file.TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(localPath) && AdicionarImagemNaGaleria(localPath, Path.GetFileNameWithoutExtension(localPath)))
                    {
                        adicionadas++;
                    }
                }

                if (adicionadas > 0)
                {
                    ToastNotification.Mostrar(Workspace, $"{adicionadas} mídia(s) adicionada(s) à galeria!", "success");
                }
            };

            _txtGalleryPath = new TextBox
            {
                Watermark = "Ou cole o caminho da imagem ou vídeo aqui...",
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 4),
                MinHeight = 28,
                Height = 28,
                FontSize = 11.5,
                CornerRadius = new CornerRadius(5)
            };

            _btnGalleryAddPath = new Button
            {
                Content = "Incluir",
                Height = 28,
                Padding = new Thickness(10, 0),
                Margin = new Thickness(6, 0, 8, 0),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnGalleryAddPath, "Adicionar imagem pelo caminho digitado");

            void AdicionarPorCaminho()
            {
                string caminho = (_txtGalleryPath?.Text ?? string.Empty).Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(caminho)) return;

                if (!File.Exists(caminho))
                {
                    ToastNotification.Mostrar(Workspace, "Arquivo de mídia não encontrado.", "warning");
                    return;
                }

                if (AdicionarImagemNaGaleria(caminho, Path.GetFileNameWithoutExtension(caminho)))
                {
                    if (_txtGalleryPath != null) _txtGalleryPath.Text = string.Empty;
                    ToastNotification.Mostrar(Workspace, "Mídia adicionada à galeria!", "success");
                }
                else
                {
                    ToastNotification.Mostrar(Workspace, "Não foi possível carregar essa mídia.", "warning");
                }
            }

            _btnGalleryAddPath.Click += (s, e) => AdicionarPorCaminho();
            _txtGalleryPath.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    AdicionarPorCaminho();
                }
            };

            _lblGalleryCount = new TextBlock
            {
                Text = "0 imagens",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 4, 0)
            };

            Grid.SetColumn(_btnGalleryAddFiles, 0);
            Grid.SetColumn(_txtGalleryPath, 1);
            Grid.SetColumn(_btnGalleryAddPath, 2);
            Grid.SetColumn(_lblGalleryCount, 3);
            toolbarGrid.Children.Add(_btnGalleryAddFiles);
            toolbarGrid.Children.Add(_txtGalleryPath);
            toolbarGrid.Children.Add(_btnGalleryAddPath);
            toolbarGrid.Children.Add(_lblGalleryCount);

            _galleryToolbar.Child = toolbarGrid;
            DockPanel.SetDock(_galleryToolbar, Dock.Top);
            galleryDock.Children.Add(_galleryToolbar);

            var bodyPanel = new Panel();

            _galleryEmptyState = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20)
            };

            _lblGalleryEmptyTitle = new TextBlock
            {
                Text = "Sua galeria está vazia",
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            _lblGalleryEmptySub = new TextBlock
            {
                Text = "Clique em \"+ Adicionar Mídias...\", cole um caminho acima\nou arraste arquivos de imagem ou vídeo para cá.\nDepois, clique em \"Janela\" para abrir na área principal!",
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                LineHeight = 17
            };

            _galleryEmptyState.Children.Add(_lblGalleryEmptyTitle);
            _galleryEmptyState.Children.Add(_lblGalleryEmptySub);

            _galleryWrapPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10)
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _galleryWrapPanel
            };

            bodyPanel.Children.Add(_galleryEmptyState);
            bodyPanel.Children.Add(scrollViewer);

            // Suporte a arrastar e soltar (Drag & Drop) arquivos de imagem
            DragDrop.SetAllowDrop(bodyPanel, true);
            bodyPanel.AddHandler(DragDrop.DropEvent, (s, e) =>
            {
                var files = e.Data.GetFiles();
                if (files == null) return;

                int count = 0;
                foreach (var item in files)
                {
                    string? localPath = item.TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
                    {
                        if (AdicionarImagemNaGaleria(localPath, Path.GetFileNameWithoutExtension(localPath)))
                        {
                            count++;
                        }
                    }
                }

                if (count > 0)
                {
                    ToastNotification.Mostrar(Workspace, $"{count} mídia(s) adicionada(s)!", "success");
                }
            });

            galleryDock.Children.Add(bodyPanel);

            if (initialItems != null)
            {
                foreach (var item in initialItems)
                {
                    AdicionarImagemNaGaleria(item.FilePath, item.Title, item.Id, item.MediaType);
                }
            }

            AtualizarContadorGaleria();
            return galleryDock;
        }

        public bool AdicionarImagemNaGaleria(string filePath, string? titulo = null, string? id = null, string? mediaType = null)
        {
            if (_galleryWrapPanel == null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            bool isVideo = string.Equals(mediaType, "video", StringComparison.OrdinalIgnoreCase) || EhArquivoDeVideo(filePath);
            Bitmap? bitmap = null;
            Control thumbVisual;
            Image? imgControl = null;

            if (isVideo)
            {
                var videoThumb = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = SolidColorBrush.Parse("#0B1120")
                };

                var playCircle = new Border
                {
                    Width = 42,
                    Height = 42,
                    CornerRadius = new CornerRadius(21),
                    Background = SolidColorBrush.Parse("#AA1E293B"),
                    BorderBrush = ThemePalette.AccentLight,
                    BorderThickness = new Thickness(1.5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = AppIcons.Create(AppIcons.Play, 18, ThemePalette.IconLightGray)
                };

                string ext = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
                var badge = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5, 1),
                    Background = ThemePalette.Accent,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(6),
                    Child = new TextBlock
                    {
                        Text = string.IsNullOrEmpty(ext) ? "VÍDEO" : ext,
                        FontSize = 9.5,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    }
                };

                videoThumb.Children.Add(playCircle);
                videoThumb.Children.Add(badge);
                thumbVisual = videoThumb;
            }
            else
            {
                try
                {
                    using var fs = File.OpenRead(filePath);
                    bitmap = Bitmap.DecodeToWidth(fs, 220);
                }
                catch
                {
                    return false;
                }

                imgControl = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                thumbVisual = imgControl;
            }

            string nomeExibicao = string.IsNullOrWhiteSpace(titulo)
                ? Path.GetFileNameWithoutExtension(filePath)
                : titulo.Trim();

            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition(new GridLength(104)));
            cardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            cardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var thumbBorder = new Border
            {
                CornerRadius = new CornerRadius(5),
                ClipToBounds = true,
                Margin = new Thickness(6, 6, 6, 4),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = thumbVisual
            };
            ToolTip.SetTip(thumbBorder, isVideo
                ? "Duplo clique para abrir o vídeo em uma janela"
                : "Duplo clique para colocar a imagem em janela na janela principal");

            var lblTitle = new TextBlock
            {
                Text = nomeExibicao,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(8, 2, 8, 4),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ToolTip.SetTip(lblTitle, $"{nomeExibicao}\n{filePath}");

            var actionsGrid = new Grid
            {
                Margin = new Thickness(6, 0, 6, 6)
            };
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            actionsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var btnOpenWin = new Button
            {
                Content = AppIcons.WithText(isVideo ? AppIcons.Play : AppIcons.Window, isVideo ? "Assistir" : "Janela", 10.5, 4, ThemePalette.IconLightGray),
                Height = 24,
                Padding = new Thickness(6, 0),
                Margin = new Thickness(0, 0, 4, 0),
                CornerRadius = new CornerRadius(4),
                Background = ThemePalette.Accent,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btnOpenWin, isVideo
                ? "Abrir este vídeo em uma janela reprodutora"
                : "Abrir esta imagem em uma janela na janela principal");

            var btnSetBg = new Button
            {
                Content = AppIcons.Create(AppIcons.Image, 11, ThemePalette.IconLightGray),
                Width = 26,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 4, 0),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsVisible = !isVideo
            };
            ToolTip.SetTip(btnSetBg, "Aplicar/remover imagem como plano de fundo da janela principal");

            var btnRemove = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                FontSize = 10,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btnRemove, "Remover da galeria");

            Grid.SetColumn(btnOpenWin, 0);
            Grid.SetColumn(btnSetBg, 1);
            Grid.SetColumn(btnRemove, 2);
            actionsGrid.Children.Add(btnOpenWin);
            actionsGrid.Children.Add(btnSetBg);
            actionsGrid.Children.Add(btnRemove);

            Grid.SetRow(thumbBorder, 0);
            Grid.SetRow(lblTitle, 1);
            Grid.SetRow(actionsGrid, 2);
            cardGrid.Children.Add(thumbBorder);
            cardGrid.Children.Add(lblTitle);
            cardGrid.Children.Add(actionsGrid);

            var cardBorder = new Border
            {
                Width = 158,
                Margin = new Thickness(5),
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1),
                Child = cardGrid
            };

            var state = new GalleryCardState
            {
                Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id,
                Title = nomeExibicao,
                FilePath = filePath,
                MediaType = isVideo ? "video" : "image",
                CardBorder = cardBorder,
                ThumbContainer = thumbBorder,
                ThumbImage = imgControl,
                TitleLabel = lblTitle,
                BtnOpenWindow = btnOpenWin,
                BtnSetBg = btnSetBg,
                BtnRemove = btnRemove,
                LoadedBitmap = bitmap
            };

            _galleryCards.Add(state);
            _galleryWrapPanel.Children.Add(cardBorder);

            btnOpenWin.Click += (s, e) => OnRequestOpenGalleryImage?.Invoke(this, state);
            thumbBorder.PointerPressed += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    OnRequestOpenGalleryImage?.Invoke(this, state);
                }
            };

            btnSetBg.Click += (s, e) => OnRequestSetWorkspaceBackground?.Invoke(state.FilePath);

            btnRemove.Click += (s, e) =>
            {
                state.OpenImageWindow?.Fechar();
                _galleryCards.Remove(state);
                _galleryWrapPanel.Children.Remove(cardBorder);
                state.LoadedBitmap?.Dispose();
                state.LoadedBitmap = null;
                AtualizarContadorGaleria();
            };

            AtualizarEstiloCardGaleria(state);
            AtualizarContadorGaleria();
            return true;
        }

        private void AtualizarContadorGaleria()
        {
            if (_lblGalleryCount != null)
            {
                int total = _galleryCards.Count;
                int videos = _galleryCards.Count(c => c.MediaType == "video");
                int imgs = total - videos;

                if (total == 0)
                    _lblGalleryCount.Text = "0 mídias";
                else if (videos == 0)
                    _lblGalleryCount.Text = imgs == 1 ? "1 imagem" : $"{imgs} imagens";
                else if (imgs == 0)
                    _lblGalleryCount.Text = videos == 1 ? "1 vídeo" : $"{videos} vídeos";
                else
                    _lblGalleryCount.Text = $"{imgs} img • {videos} vid";
            }

            if (_galleryEmptyState != null)
            {
                _galleryEmptyState.IsVisible = _galleryCards.Count == 0;
            }
        }

        private void AtualizarEstiloCardGaleria(GalleryCardState card)
        {
            var cardBg = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            var borderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            var textPrimary = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var btnBg = _isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg;

            card.CardBorder.Background = cardBg;
            card.CardBorder.BorderBrush = borderBrush;
            card.ThumbContainer.Background = _isDarkMode ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;
            card.TitleLabel.Foreground = textPrimary;

            card.BtnSetBg.Background = btnBg;
            card.BtnSetBg.BorderBrush = borderBrush;
            card.BtnSetBg.Foreground = textPrimary;

            card.BtnRemove.Background = btnBg;
            card.BtnRemove.BorderBrush = borderBrush;
            card.BtnRemove.Foreground = textPrimary;
        }

        private Control CriarConteudoImagem(string imagePath)
        {
            _singleImagePath = imagePath ?? string.Empty;
            var imageDock = new DockPanel();

            _imageToolbar = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 5),
                Height = 36
            };

            var barGrid = new Grid();
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var leftControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnImageFit = CriarBotaoFormatacao("Ajustar", "Alternar modo de ajuste (Ajustar / Preencher / Original)", FontWeight.SemiBold, FontStyle.Normal, width: 64);
            _btnImageZoomOut = CriarBotaoFormatacao("", "Diminuir zoom", FontWeight.Bold, FontStyle.Normal, width: 26);
            _btnImageZoomOut.Content = AppIcons.Create(AppIcons.Minus, 10, ThemePalette.IconLightGray);
            _btnImageZoomReset = CriarBotaoFormatacao("100%", "Redefinir zoom para 100%", FontWeight.SemiBold, FontStyle.Normal, width: 48);
            _btnImageZoomIn = CriarBotaoFormatacao("", "Aumentar zoom", FontWeight.Bold, FontStyle.Normal, width: 26);
            _btnImageZoomIn.Content = AppIcons.Create(AppIcons.Plus, 10, ThemePalette.IconLightGray);

            leftControls.Children.Add(_btnImageFit);
            leftControls.Children.Add(_btnImageZoomOut);
            leftControls.Children.Add(_btnImageZoomReset);
            leftControls.Children.Add(_btnImageZoomIn);

            _lblImageInfo = new TextBlock
            {
                Text = Path.GetFileName(_singleImagePath),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            _btnImageSetBg = new Button
            {
                Content = AppIcons.WithText(AppIcons.Image, "Fundo da Janela Principal", 11, 5, ThemePalette.IconLightGray),
                Height = 25,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnImageSetBg, "Aplicar/remover esta imagem como fundo da janela principal");
            _btnImageSetBg.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_singleImagePath))
                {
                    OnRequestSetWorkspaceBackground?.Invoke(_singleImagePath);
                }
            };

            Grid.SetColumn(leftControls, 0);
            Grid.SetColumn(_lblImageInfo, 1);
            Grid.SetColumn(_btnImageSetBg, 2);
            barGrid.Children.Add(leftControls);
            barGrid.Children.Add(_lblImageInfo);
            barGrid.Children.Add(_btnImageSetBg);

            _imageToolbar.Child = barGrid;
            DockPanel.SetDock(_imageToolbar, Dock.Top);
            imageDock.Children.Add(_imageToolbar);

            _imageViewerControl = new Image
            {
                Stretch = _imageStretchMode,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrWhiteSpace(_singleImagePath) && File.Exists(_singleImagePath))
            {
                try
                {
                    _singleImageBitmap = new Bitmap(_singleImagePath);
                    _imageViewerControl.Source = _singleImageBitmap;
                    _lblImageInfo.Text = $"{_singleImageBitmap.PixelSize.Width} × {_singleImageBitmap.PixelSize.Height} px";
                }
                catch
                {
                    _lblImageInfo.Text = "Erro ao carregar imagem";
                }
            }
            else
            {
                _lblImageInfo.Text = "Arquivo não encontrado";
            }

            _imageScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _imageViewerControl
            };

            void AtualizarModoVisualizacaoImagem()
            {
                if (_imageViewerControl == null || _singleImageBitmap == null) return;

                if (Math.Abs(_imageZoomFactor - 1.0) < 0.01)
                {
                    _imageViewerControl.Width = double.NaN;
                    _imageViewerControl.Height = double.NaN;
                    _imageViewerControl.Stretch = _imageStretchMode;
                }
                else
                {
                    _imageViewerControl.Stretch = Stretch.Uniform;
                    double baseW = Math.Max(100, Bounds.Width - 28);
                    double baseH = Math.Max(100, Bounds.Height - 90);
                    _imageViewerControl.Width = baseW * _imageZoomFactor;
                    _imageViewerControl.Height = baseH * _imageZoomFactor;
                }

                if (_btnImageZoomReset != null)
                {
                    _btnImageZoomReset.Content = $"{(int)Math.Round(_imageZoomFactor * 100)}%";
                }
            }

            _btnImageFit.Click += (s, e) =>
            {
                _imageZoomFactor = 1.0;
                _imageStretchMode = _imageStretchMode switch
                {
                    Stretch.Uniform => Stretch.UniformToFill,
                    Stretch.UniformToFill => Stretch.None,
                    _ => Stretch.Uniform
                };
                _btnImageFit.Content = _imageStretchMode switch
                {
                    Stretch.Uniform => "Ajustar",
                    Stretch.UniformToFill => "Preencher",
                    _ => "Original"
                };
                AtualizarModoVisualizacaoImagem();
            };

            _btnImageZoomOut.Click += (s, e) =>
            {
                _imageZoomFactor = Math.Max(0.25, _imageZoomFactor - 0.25);
                AtualizarModoVisualizacaoImagem();
            };

            _btnImageZoomIn.Click += (s, e) =>
            {
                _imageZoomFactor = Math.Min(4.0, _imageZoomFactor + 0.25);
                AtualizarModoVisualizacaoImagem();
            };

            _btnImageZoomReset.Click += (s, e) =>
            {
                _imageZoomFactor = 1.0;
                AtualizarModoVisualizacaoImagem();
            };

            imageDock.Children.Add(_imageScrollViewer);
            return imageDock;
        }

        private Control CriarConteudoVideo(string videoPath)
        {
            _singleVideoPath = videoPath ?? string.Empty;
            var videoDock = new DockPanel();

            _videoToolbar = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 5),
                Height = 36
            };

            var barGrid = new Grid();
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            barGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _btnVideoReload = CriarBotaoFormatacao("", "Recarregar vídeo", FontWeight.SemiBold, FontStyle.Normal, width: 28);
            _btnVideoReload.Content = AppIcons.Create(AppIcons.Refresh, 11, ThemePalette.IconLightGray);

            _lblVideoInfo = new TextBlock
            {
                Text = File.Exists(_singleVideoPath) ? Path.GetFileName(_singleVideoPath) : "Arquivo de vídeo não encontrado",
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            _btnVideoExternal = new Button
            {
                Content = AppIcons.WithText(AppIcons.ExternalLink, "Player Externo", 11, 5, ThemePalette.IconLightGray),
                Height = 25,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_btnVideoExternal, "Abrir vídeo no reprodutor padrão do sistema");
            _btnVideoExternal.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_singleVideoPath) && File.Exists(_singleVideoPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = _singleVideoPath,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            };

            Grid.SetColumn(_btnVideoReload, 0);
            Grid.SetColumn(_lblVideoInfo, 1);
            Grid.SetColumn(_btnVideoExternal, 2);
            barGrid.Children.Add(_btnVideoReload);
            barGrid.Children.Add(_lblVideoInfo);
            barGrid.Children.Add(_btnVideoExternal);

            _videoToolbar.Child = barGrid;
            DockPanel.SetDock(_videoToolbar, Dock.Top);
            videoDock.Children.Add(_videoToolbar);

            string playerUrl = "about:blank";
            if (!string.IsNullOrWhiteSpace(_singleVideoPath) && File.Exists(_singleVideoPath))
            {
                try
                {
                    string folder = Path.Combine(Path.GetTempPath(), "AvaloniaMdiApp_Video");
                    Directory.CreateDirectory(folder);
                    _tempVideoHtmlPath = Path.Combine(folder, $"video_{WindowId}.html");

                    string videoUri = new Uri(Path.GetFullPath(_singleVideoPath)).AbsoluteUri;
                    string safeTitle = WebUtility.HtmlEncode(Path.GetFileName(_singleVideoPath));
                    string html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<title>{safeTitle}</title>
<style>
  html, body {{ margin: 0; padding: 0; width: 100%; height: 100%; background: #090d16; overflow: hidden; display: flex; align-items: center; justify-content: center; }}
  video {{ width: 100%; height: 100%; object-fit: contain; outline: none; }}
</style>
</head>
<body>
  <video controls autoplay playsinline src=""{videoUri}""></video>
</body>
</html>";
                    File.WriteAllText(_tempVideoHtmlPath, html);
                    playerUrl = new Uri(_tempVideoHtmlPath).AbsoluteUri;
                }
                catch
                {
                    playerUrl = new Uri(Path.GetFullPath(_singleVideoPath)).AbsoluteUri;
                }
            }

            _browserHost = new ChromiumWebViewHost(playerUrl, this)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _btnVideoReload.Click += (s, e) => _browserHost?.Reload();

            var videoBodyHost = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            videoBodyHost.Children.Add(new TextBlock
            {
                Text = File.Exists(_singleVideoPath) ? "Carregando player de vídeo..." : "Arquivo de vídeo não encontrado",
                FontSize = 12,
                Foreground = ThemePalette.IconMutedGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            videoBodyHost.Children.Add(_browserHost);

            videoDock.Children.Add(videoBodyHost);
            return videoDock;
        }

        private void AtualizarCoresGaleriaEImagem()
        {
            var barBg = _isDarkMode ? ThemePalette.DarkContentBg : ThemePalette.LightContentBg;
            var borderBrush = _isDarkMode ? ThemePalette.DarkBorder : ThemePalette.LightBorder;
            var btnBg = _isDarkMode ? ThemePalette.DarkButtonBg : ThemePalette.LightButtonBg;
            var textPrimary = _isDarkMode ? ThemePalette.DarkTextPrimary : ThemePalette.LightTextPrimary;
            var textSecondary = _isDarkMode ? ThemePalette.DarkTextSecondary : ThemePalette.LightTextSecondary;
            var inputBg = _isDarkMode ? ThemePalette.DarkWindowBg : ThemePalette.LightWindowBg;

            if (_galleryToolbar != null)
            {
                _galleryToolbar.Background = barBg;
                _galleryToolbar.BorderBrush = borderBrush;

                if (_txtGalleryPath != null)
                {
                    _txtGalleryPath.Background = inputBg;
                    _txtGalleryPath.BorderBrush = borderBrush;
                    _txtGalleryPath.Foreground = textPrimary;
                }

                if (_btnGalleryAddPath != null)
                {
                    _btnGalleryAddPath.Background = btnBg;
                    _btnGalleryAddPath.BorderBrush = borderBrush;
                    _btnGalleryAddPath.Foreground = textPrimary;
                }

                if (_lblGalleryCount != null)
                {
                    _lblGalleryCount.Foreground = textSecondary;
                }

                if (_lblGalleryEmptyTitle != null)
                {
                    _lblGalleryEmptyTitle.Foreground = textPrimary;
                }

                if (_lblGalleryEmptySub != null)
                {
                    _lblGalleryEmptySub.Foreground = textSecondary;
                }

                foreach (var card in _galleryCards)
                {
                    AtualizarEstiloCardGaleria(card);
                }
            }

            if (_imageToolbar != null)
            {
                _imageToolbar.Background = barBg;
                _imageToolbar.BorderBrush = borderBrush;

                foreach (var btn in new[] { _btnImageFit, _btnImageZoomOut, _btnImageZoomReset, _btnImageZoomIn, _btnImageSetBg })
                {
                    if (btn != null)
                    {
                        btn.Background = btnBg;
                        btn.BorderBrush = borderBrush;
                        btn.Foreground = textPrimary;
                    }
                }

                if (_lblImageInfo != null)
                {
                    _lblImageInfo.Foreground = textSecondary;
                }
            }

            if (_videoToolbar != null)
            {
                _videoToolbar.Background = barBg;
                _videoToolbar.BorderBrush = borderBrush;

                foreach (var btn in new[] { _btnVideoReload, _btnVideoExternal })
                {
                    if (btn != null)
                    {
                        btn.Background = btnBg;
                        btn.BorderBrush = borderBrush;
                        btn.Foreground = textPrimary;
                    }
                }

                if (_lblVideoInfo != null)
                {
                    _lblVideoInfo.Foreground = textSecondary;
                }
            }
        }

        private void AtualizarHibernacaoGaleriaEImagem(bool hibernar)
        {
            if (WindowType == "image" && _imageViewerControl != null)
            {
                if (hibernar)
                {
                    _imageViewerControl.Source = null;
                    _singleImageBitmap?.Dispose();
                    _singleImageBitmap = null;
                }
                else if (_singleImageBitmap == null && !string.IsNullOrWhiteSpace(_singleImagePath) && File.Exists(_singleImagePath))
                {
                    try
                    {
                        _singleImageBitmap = new Bitmap(_singleImagePath);
                        _imageViewerControl.Source = _singleImageBitmap;
                    }
                    catch { }
                }
            }
            else if (WindowType == "gallery")
            {
                if (hibernar)
                {
                    foreach (var card in _galleryCards)
                    {
                        if (card.ThumbImage != null)
                        {
                            card.ThumbImage.Source = null;
                        }
                        card.LoadedBitmap?.Dispose();
                        card.LoadedBitmap = null;
                    }
                }
                else
                {
                    foreach (var card in _galleryCards)
                    {
                        if (card.MediaType != "video" && card.ThumbImage != null && card.LoadedBitmap == null && !string.IsNullOrWhiteSpace(card.FilePath) && File.Exists(card.FilePath))
                        {
                            try
                            {
                                using var fs = File.OpenRead(card.FilePath);
                                card.LoadedBitmap = Bitmap.DecodeToWidth(fs, 220);
                                card.ThumbImage.Source = card.LoadedBitmap;
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private void LiberarRecursosGaleriaEImagem()
        {
            foreach (var card in _galleryCards)
            {
                card.LoadedBitmap?.Dispose();
                card.LoadedBitmap = null;
            }
            _galleryCards.Clear();

            _singleImageBitmap?.Dispose();
            _singleImageBitmap = null;

            if (!string.IsNullOrWhiteSpace(_tempVideoHtmlPath) && File.Exists(_tempVideoHtmlPath))
            {
                try { File.Delete(_tempVideoHtmlPath); } catch { }
                _tempVideoHtmlPath = string.Empty;
            }
        }
    }
}

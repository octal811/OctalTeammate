using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using ProgressBar = System.Windows.Controls.ProgressBar;
using Orientation = System.Windows.Controls.Orientation;

namespace OctalPulse.Services;

public static class MarkdownToFlowDocumentHelper
{
    public static FlowDocument ConvertToFlowDocument(string markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(24, 20, 24, 24),
            FontFamily = new FontFamily("Segoe UI, Inter, Arial, sans-serif"),
            FontSize = 14,
            LineHeight = 22,
            Background = Brushes.Transparent
        };

        var textBrush = GetBrush("Brush.TextPrimary", Brushes.Black);
        doc.Foreground = textBrush;

        if (string.IsNullOrWhiteSpace(markdown))
        {
            var emptyPara = new Paragraph(new Run("Empty paper. Click 'Edit Mode' or use formatting tools above to start writing."))
            {
                Foreground = GetBrush("Brush.TextMuted", Brushes.Gray),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 10, 0, 10)
            };
            doc.Blocks.Add(emptyPara);
            return doc;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            // 1. Fenced Code Block
            if (line.TrimStart().StartsWith("```"))
            {
                var lang = line.TrimStart().TrimStart('`').Trim();
                var codeLines = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                {
                    codeLines.Add(lines[i]);
                    i++;
                }
                i++; // consume closing ```

                if (lang.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
                {
                    doc.Blocks.Add(CreateMermaidBlock(string.Join("\n", codeLines)));
                    continue;
                }

                doc.Blocks.Add(CreateCodeBlock(string.Join("\n", codeLines), lang));
                continue;
            }

            // 2. Headings
            if (line.StartsWith("#"))
            {
                var match = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
                if (match.Success)
                {
                    var level = match.Groups[1].Value.Length;
                    var text = match.Groups[2].Value;
                    doc.Blocks.Add(CreateHeading(text, level));
                    i++;
                    continue;
                }
            }

            // 3. Horizontal Rule
            if (Regex.IsMatch(line.Trim(), @"^(---|\*\*\*|___)$"))
            {
                doc.Blocks.Add(CreateHorizontalRule());
                i++;
                continue;
            }

            // 4. Blockquote
            if (line.TrimStart().StartsWith(">"))
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
                {
                    var qLine = lines[i].TrimStart();
                    quoteLines.Add(qLine.Length > 1 && qLine[1] == ' ' ? qLine[2..] : qLine[1..]);
                    i++;
                }

                doc.Blocks.Add(CreateBlockquote(string.Join(" ", quoteLines)));
                continue;
            }

            // 5. Image: standalone ![alt](path)
            var imgMatch = Regex.Match(line.Trim(), @"^!\[(.*?)\]\((.*?)\)$");
            if (imgMatch.Success)
            {
                doc.Blocks.Add(CreateImageBlock(imgMatch.Groups[1].Value, imgMatch.Groups[2].Value));
                i++;
                continue;
            }

            // 6. Table
            if (line.Trim().StartsWith("|") && line.Trim().EndsWith("|") && i + 1 < lines.Length && Regex.IsMatch(lines[i + 1].Trim(), @"^\|?(\s*:?-+:?\s*\|)+\s*$"))
            {
                var tableLines = new List<string>();
                while (i < lines.Length && lines[i].Trim().StartsWith("|") && lines[i].Trim().EndsWith("|"))
                {
                    tableLines.Add(lines[i]);
                    i++;
                }

                var table = CreateTable(tableLines);
                if (table != null)
                {
                    doc.Blocks.Add(table);
                    continue;
                }
            }

            // 7. Checklists: - [ ] or - [x]
            if (Regex.IsMatch(line.TrimStart(), @"^[-*]\s+\[([ xX])\]\s+(.*)$"))
            {
                var taskList = new System.Windows.Documents.List
                {
                    MarkerStyle = TextMarkerStyle.None,
                    Margin = new Thickness(0, 4, 0, 8),
                    Padding = new Thickness(4, 0, 0, 0)
                };

                while (i < lines.Length)
                {
                    var tMatch = Regex.Match(lines[i].TrimStart(), @"^[-*]\s+\[([ xX])\]\s+(.*)$");
                    if (!tMatch.Success) break;

                    var isDone = tMatch.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase);
                    var itemText = tMatch.Groups[2].Value;

                    var p = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                    p.Inlines.Add(new Run(isDone ? "☑ " : "☐ ")
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = isDone ? GetBrush("Brush.Success", Brushes.Green) : GetBrush("Brush.TextMuted", Brushes.Gray)
                    });

                    AppendFormattedInlines(p.Inlines, itemText);
                    if (isDone)
                    {
                        p.Foreground = GetBrush("Brush.TextMuted", Brushes.Gray);
                    }

                    taskList.ListItems.Add(new ListItem(p));
                    i++;
                }

                doc.Blocks.Add(taskList);
                continue;
            }

            // 8. Bullet List (- or *)
            if (Regex.IsMatch(line.TrimStart(), @"^[-*+]\s+(.*)$"))
            {
                var list = new System.Windows.Documents.List
                {
                    MarkerStyle = TextMarkerStyle.Disc,
                    Margin = new Thickness(0, 4, 0, 8),
                    Padding = new Thickness(16, 0, 0, 0)
                };

                while (i < lines.Length)
                {
                    var lMatch = Regex.Match(lines[i].TrimStart(), @"^[-*+]\s+(.*)$");
                    if (!lMatch.Success) break;

                    var p = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                    AppendFormattedInlines(p.Inlines, lMatch.Groups[1].Value);
                    list.ListItems.Add(new ListItem(p));
                    i++;
                }

                doc.Blocks.Add(list);
                continue;
            }

            // 9. Numbered List (1. 2.)
            if (Regex.IsMatch(line.TrimStart(), @"^\d+\.\s+(.*)$"))
            {
                var list = new System.Windows.Documents.List
                {
                    MarkerStyle = TextMarkerStyle.Decimal,
                    Margin = new Thickness(0, 4, 0, 8),
                    Padding = new Thickness(16, 0, 0, 0)
                };

                while (i < lines.Length)
                {
                    var numMatch = Regex.Match(lines[i].TrimStart(), @"^\d+\.\s+(.*)$");
                    if (!numMatch.Success) break;

                    var p = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                    AppendFormattedInlines(p.Inlines, numMatch.Groups[1].Value);
                    list.ListItems.Add(new ListItem(p));
                    i++;
                }

                doc.Blocks.Add(list);
                continue;
            }

            // 10. Normal Paragraph
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            var para = new Paragraph { Margin = new Thickness(0, 4, 0, 6) };
            AppendFormattedInlines(para.Inlines, line);
            doc.Blocks.Add(para);
            i++;
        }

        return doc;
    }

    private static Block CreateHeading(string text, int level)
    {
        var p = new Paragraph
        {
            Margin = level switch
            {
                1 => new Thickness(0, 18, 0, 8),
                2 => new Thickness(0, 14, 0, 6),
                3 => new Thickness(0, 10, 0, 4),
                _ => new Thickness(0, 8, 0, 4)
            },
            FontSize = level switch
            {
                1 => 22,
                2 => 18,
                3 => 15,
                4 => 14,
                _ => 13
            },
            FontWeight = level <= 2 ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = level == 1 ? GetBrush("Brush.Primary", Brushes.DodgerBlue) : GetBrush("Brush.TextPrimary", Brushes.Black)
        };

        AppendFormattedInlines(p.Inlines, text);
        return p;
    }

    private static Block CreateHorizontalRule()
    {
        var border = new Border
        {
            Height = 1,
            Background = GetBrush("Brush.Border", Brushes.LightGray),
            Margin = new Thickness(0, 14, 0, 14)
        };
        return new BlockUIContainer(border);
    }

    private static Block CreateBlockquote(string text)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(3.5, 0, 0, 0),
            BorderBrush = GetBrush("Brush.Primary", Brushes.DodgerBlue),
            Background = GetBrush("Brush.SurfaceElevated", new SolidColorBrush(Color.FromArgb(20, 100, 100, 255))),
            CornerRadius = new CornerRadius(0, 4, 4, 0),
            Padding = new Thickness(14, 8, 12, 8),
            Margin = new Thickness(0, 8, 0, 8)
        };

        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontStyle = FontStyles.Italic,
            Foreground = GetBrush("Brush.TextSecondary", Brushes.DarkGray),
            FontSize = 13.5
        };

        // Append formatted inlines to TextBlock
        var dummyP = new Paragraph();
        AppendFormattedInlines(dummyP.Inlines, text);
        foreach (var inline in dummyP.Inlines.ToList())
        {
            dummyP.Inlines.Remove(inline);
            tb.Inlines.Add(inline);
        }

        border.Child = tb;
        return new BlockUIContainer(border);
    }

    private static Block CreateCodeBlock(string code, string language)
    {
        var border = new Border
        {
            Background = GetBrush("Brush.SurfaceElevated", new SolidColorBrush(Color.FromRgb(30, 30, 30))),
            BorderBrush = GetBrush("Brush.Border", Brushes.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 8, 0, 8)
        };

        var stack = new StackPanel();

        if (!string.IsNullOrWhiteSpace(language))
        {
            var header = new TextBlock
            {
                Text = language.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = GetBrush("Brush.Primary", Brushes.DodgerBlue),
                Margin = new Thickness(0, 0, 0, 6)
            };
            stack.Children.Add(header);
        }

        var tb = new TextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New"),
            FontSize = 12.5,
            LineHeight = 18,
            Foreground = GetBrush("Brush.TextPrimary", Brushes.White),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(tb);

        border.Child = stack;
        return new BlockUIContainer(border);
    }

    private static Block CreateMermaidBlock(string code)
    {
        var typeName = MermaidDiagramService.DetectDiagramType(code);

        var container = new Border
        {
            Background = GetBrush("Brush.SurfaceElevated", new SolidColorBrush(Color.FromRgb(30, 30, 30))),
            BorderBrush = GetBrush("Brush.Border", Brushes.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 12, 0, 16)
        };

        var mainStack = new StackPanel();

        // 1. Header Row
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left Header: Badges & Title
        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var diagramBadge = new Border
        {
            Background = GetBrush("Brush.PrimarySoft", new SolidColorBrush(Color.FromArgb(40, 99, 102, 241))),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var badgeText = new TextBlock
        {
            Text = "📊 " + typeName.ToUpperInvariant(),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = GetBrush("Brush.Primary", Brushes.DodgerBlue)
        };
        diagramBadge.Child = badgeText;
        leftStack.Children.Add(diagramBadge);

        var subText = new TextBlock
        {
            Text = "Mermaid Live Diagram",
            FontSize = 11,
            Foreground = GetBrush("Brush.TextMuted", Brushes.Gray),
            VerticalAlignment = VerticalAlignment.Center
        };
        leftStack.Children.Add(subText);
        Grid.SetColumn(leftStack, 0);
        headerGrid.Children.Add(leftStack);

        // Right Header: Actions (Toggle Code, Copy, Save Image)
        var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var toggleBtn = new Button
        {
            Content = "👁 View Code",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Height = 26,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        SetGhostButtonStyle(toggleBtn);
        rightStack.Children.Add(toggleBtn);

        var copyBtn = new Button
        {
            Content = "📋 Copy",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Height = 26,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        SetGhostButtonStyle(copyBtn);
        copyBtn.Click += (s, e) =>
        {
            try
            {
                System.Windows.Clipboard.SetText(code);
                copyBtn.Content = "✓ Copied!";
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (ts, te) =>
                {
                    copyBtn.Content = "📋 Copy";
                    timer.Stop();
                };
                timer.Start();
            }
            catch { }
        };
        rightStack.Children.Add(copyBtn);

        var saveBtn = new Button
        {
            Content = "💾 Save Image",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Height = 26,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Visibility = Visibility.Collapsed
        };
        SetGhostButtonStyle(saveBtn);
        rightStack.Children.Add(saveBtn);

        Grid.SetColumn(rightStack, 1);
        headerGrid.Children.Add(rightStack);
        mainStack.Children.Add(headerGrid);

        // 2. Diagram Display Card
        var diagramCard = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            MinHeight = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var cardContentGrid = new Grid();

        // Loading State
        var loadingStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 20)
        };
        var loadingText = new TextBlock
        {
            Text = "Rendering " + typeName + "...",
            FontSize = 12,
            Foreground = Brushes.DimGray,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var loadingBar = new ProgressBar
        {
            IsIndeterminate = true,
            Width = 140,
            Height = 4,
            Margin = new Thickness(0, 8, 0, 0)
        };
        loadingStack.Children.Add(loadingText);
        loadingStack.Children.Add(loadingBar);
        cardContentGrid.Children.Add(loadingStack);

        // Diagram Image
        var diagramImage = new Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxHeight = 650,
            Visibility = Visibility.Collapsed
        };
        RenderOptions.SetBitmapScalingMode(diagramImage, BitmapScalingMode.HighQuality);
        cardContentGrid.Children.Add(diagramImage);

        // Error / Offline Text
        var errorPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 20),
            Visibility = Visibility.Collapsed
        };
        var errorText = new TextBlock
        {
            Text = "⚠️ Unable to render diagram preview (offline or invalid syntax).",
            FontSize = 12,
            Foreground = Brushes.DarkRed,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var retryBtn = new Button
        {
            Content = "🔄 Retry",
            FontSize = 11,
            Height = 26,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        SetGhostButtonStyle(retryBtn);
        errorPanel.Children.Add(errorText);
        errorPanel.Children.Add(retryBtn);
        cardContentGrid.Children.Add(errorPanel);

        diagramCard.Child = cardContentGrid;
        mainStack.Children.Add(diagramCard);

        // 3. Raw Code Viewer (collapsed by default)
        var codeViewer = new Border
        {
            Background = GetBrush("Brush.Surface", new SolidColorBrush(Color.FromRgb(20, 20, 20))),
            BorderBrush = GetBrush("Brush.Border", Brushes.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 10, 0, 0),
            Visibility = Visibility.Collapsed
        };
        var codeTb = new TextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New"),
            FontSize = 12,
            LineHeight = 18,
            Foreground = GetBrush("Brush.TextPrimary", Brushes.LightGray),
            TextWrapping = TextWrapping.Wrap
        };
        codeViewer.Child = codeTb;
        mainStack.Children.Add(codeViewer);

        // Toggle Code click handler
        toggleBtn.Click += (s, e) =>
        {
            if (codeViewer.Visibility == Visibility.Visible)
            {
                codeViewer.Visibility = Visibility.Collapsed;
                diagramCard.Visibility = Visibility.Visible;
                toggleBtn.Content = "👁 View Code";
            }
            else
            {
                codeViewer.Visibility = Visibility.Visible;
                toggleBtn.Content = "📊 View Diagram";
            }
        };

        // Save Image click handler
        saveBtn.Click += (s, e) =>
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Diagram Image",
                FileName = $"{typeName.ToLowerInvariant().Replace(" ", "_")}.png",
                Filter = "PNG Image (*.png)|*.png|All Files (*.*)|*.*"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var srcCache = MermaidDiagramService.GetCacheFilePath(code);
                    if (File.Exists(srcCache))
                    {
                        File.Copy(srcCache, sfd.FileName, true);
                    }
                }
                catch { }
            }
        };

        // Async Loader
        async void LoadDiagram()
        {
            loadingStack.Visibility = Visibility.Visible;
            diagramImage.Visibility = Visibility.Collapsed;
            errorPanel.Visibility = Visibility.Collapsed;

            var bitmap = await MermaidDiagramService.GetDiagramBitmapAsync(code);
            if (bitmap != null)
            {
                diagramImage.Source = bitmap;
                diagramImage.Visibility = Visibility.Visible;
                loadingStack.Visibility = Visibility.Collapsed;
                saveBtn.Visibility = Visibility.Visible;
            }
            else
            {
                loadingStack.Visibility = Visibility.Collapsed;
                errorPanel.Visibility = Visibility.Visible;
                codeViewer.Visibility = Visibility.Visible;
                toggleBtn.Content = "📊 View Diagram";
            }
        }

        retryBtn.Click += (s, e) => LoadDiagram();

        container.Loaded += (s, e) => LoadDiagram();
        LoadDiagram();

        container.Child = mainStack;
        return new BlockUIContainer(container);
    }

    private static void SetGhostButtonStyle(Button btn)
    {
        if (System.Windows.Application.Current != null && System.Windows.Application.Current.TryFindResource("Button.Ghost") is Style style)
        {
            btn.Style = style;
        }
        else
        {
            btn.Background = Brushes.Transparent;
            btn.BorderThickness = new Thickness(1);
            btn.BorderBrush = GetBrush("Brush.Border", Brushes.Gray);
            btn.Foreground = GetBrush("Brush.TextSecondary", Brushes.LightGray);
        }
    }

    private static Block CreateImageBlock(string alt, string pathOrUrl)
    {
        var border = new Border
        {
            Margin = new Thickness(0, 10, 0, 10),
            CornerRadius = new CornerRadius(8),
            Background = GetBrush("Brush.SurfaceElevated", Brushes.LightGray),
            BorderBrush = GetBrush("Brush.Border", Brushes.Gray),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var stack = new StackPanel();

        try
        {
            var cleanPath = pathOrUrl.Trim();
            if (cleanPath.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = new Uri(cleanPath).LocalPath;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            if (File.Exists(cleanPath))
            {
                bitmap.UriSource = new Uri(cleanPath, UriKind.Absolute);
            }
            else if (Uri.TryCreate(cleanPath, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                bitmap.UriSource = uri;
            }
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            var img = new Image
            {
                Source = bitmap,
                MaxHeight = 420,
                MaxWidth = 720,
                Stretch = Stretch.Uniform
            };
            stack.Children.Add(img);

            if (!string.IsNullOrWhiteSpace(alt))
            {
                var caption = new TextBlock
                {
                    Text = alt,
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Foreground = GetBrush("Brush.TextMuted", Brushes.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                stack.Children.Add(caption);
            }
        }
        catch
        {
            var fallback = new TextBlock
            {
                Text = $"🖼️ [Image: {alt} ({pathOrUrl})]",
                Foreground = GetBrush("Brush.TextMuted", Brushes.Gray),
                FontStyle = FontStyles.Italic
            };
            stack.Children.Add(fallback);
        }

        border.Child = stack;
        return new BlockUIContainer(border);
    }

    private static Table? CreateTable(List<string> tableLines)
    {
        if (tableLines.Count < 2) return null;

        var headerCells = tableLines[0].Split('|', StringSplitOptions.TrimEntries)
            .Where((c, idx) => idx > 0 && idx < tableLines[0].Split('|').Length - 1)
            .ToList();

        if (headerCells.Count == 0) return null;

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 10, 0, 10),
            BorderBrush = GetBrush("Brush.Border", Brushes.Gray),
            BorderThickness = new Thickness(1)
        };

        for (var c = 0; c < headerCells.Count; c++)
        {
            table.Columns.Add(new TableColumn());
        }

        var rowGroup = new TableRowGroup();
        table.RowGroups.Add(rowGroup);

        // Header Row
        var headerRow = new TableRow
        {
            Background = GetBrush("Brush.SurfaceElevated", Brushes.LightGray)
        };
        foreach (var cellText in headerCells)
        {
            var p = new Paragraph { Margin = new Thickness(8, 6, 8, 6), FontWeight = FontWeights.Bold };
            AppendFormattedInlines(p.Inlines, cellText);
            headerRow.Cells.Add(new TableCell(p)
            {
                BorderBrush = GetBrush("Brush.Border", Brushes.Gray),
                BorderThickness = new Thickness(0, 0, 1, 1)
            });
        }
        rowGroup.Rows.Add(headerRow);

        // Data Rows (skip header separator line)
        for (var r = 2; r < tableLines.Count; r++)
        {
            var rawRow = tableLines[r];
            var cells = rawRow.Split('|', StringSplitOptions.TrimEntries)
                .Where((c, idx) => idx > 0 && idx < rawRow.Split('|').Length - 1)
                .ToList();

            var dataRow = new TableRow();
            for (var c = 0; c < headerCells.Count; c++)
            {
                var text = c < cells.Count ? cells[c] : string.Empty;
                var p = new Paragraph { Margin = new Thickness(8, 5, 8, 5) };
                AppendFormattedInlines(p.Inlines, text);
                dataRow.Cells.Add(new TableCell(p)
                {
                    BorderBrush = GetBrush("Brush.Border", Brushes.Gray),
                    BorderThickness = new Thickness(0, 0, 1, 1)
                });
            }
            rowGroup.Rows.Add(dataRow);
        }

        return table;
    }

    private static void AppendFormattedInlines(InlineCollection inlines, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Parse regex for bold, italic, code, link, strikethrough, image inline
        // Pattern matches:
        // 1. `code`
        // 2. **bold**
        // 3. *italic*
        // 4. ~~strikethrough~~
        // 5. [text](url)
        var pattern = @"(`(?<code>.+?)`)|(\*\*(?<bold>.+?)\*\*)|(\*(?<italic>.+?)\*)|(~~(?<strike>.+?)~~)|(\[(?<linktext>.+?)\]\((?<linkurl>.+?)\))";
        var matches = Regex.Matches(text, pattern);

        var lastIdx = 0;
        foreach (Match m in matches)
        {
            if (m.Index > lastIdx)
            {
                inlines.Add(new Run(text[lastIdx..m.Index]));
            }

            if (m.Groups["code"].Success)
            {
                var codeRun = new Run(m.Groups["code"].Value)
                {
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    Background = GetBrush("Brush.SurfaceElevated", new SolidColorBrush(Color.FromRgb(240, 240, 240))),
                    Foreground = GetBrush("Brush.Primary", Brushes.DarkBlue)
                };
                inlines.Add(codeRun);
            }
            else if (m.Groups["bold"].Success)
            {
                inlines.Add(new Bold(new Run(m.Groups["bold"].Value)));
            }
            else if (m.Groups["italic"].Success)
            {
                inlines.Add(new Italic(new Run(m.Groups["italic"].Value)));
            }
            else if (m.Groups["strike"].Success)
            {
                var strikeRun = new Run(m.Groups["strike"].Value)
                {
                    TextDecorations = TextDecorations.Strikethrough
                };
                inlines.Add(strikeRun);
            }
            else if (m.Groups["linktext"].Success)
            {
                var linkText = m.Groups["linktext"].Value;
                var url = m.Groups["linkurl"].Value;
                var link = new Hyperlink(new Run(linkText))
                {
                    NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null,
                    Foreground = GetBrush("Brush.Primary", Brushes.Blue)
                };
                link.RequestNavigate += (s, e) =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                };
                inlines.Add(link);
            }

            lastIdx = m.Index + m.Length;
        }

        if (lastIdx < text.Length)
        {
            inlines.Add(new Run(text[lastIdx..]));
        }
    }

    private static Brush GetBrush(string resourceKey, Brush fallback)
    {
        try
        {
            var app = System.Windows.Application.Current;
            if (app != null && app.TryFindResource(resourceKey) is Brush b)
            {
                return b;
            }
        }
        catch { }

        return fallback;
    }
}

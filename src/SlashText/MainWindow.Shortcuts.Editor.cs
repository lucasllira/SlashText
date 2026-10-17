using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SlashText.Design;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;
using Button = System.Windows.Controls.Button;

namespace SlashText;

public partial class MainWindow
{
    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e) =>
        RefreshNavigation();

    private void RefreshNavigation()
    {
        if (CategoriesPanel is null || SnippetListPanel is null)
        {
            return;
        }

        var query = SearchBox.Text.Trim();
        if (ShortcutSearchHint is not null) ShortcutSearchHint.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (ShortcutSearchClearButton is not null) ShortcutSearchClearButton.Visibility = SearchBox.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        IEnumerable<Snippet> filtered = _snippets;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item =>
                item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.Trigger.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                ShortcutCategories.Resolve(item.Category).Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.Content.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_selectedCategory))
        {
            filtered = filtered.Where(item =>
                ShortcutCategories.Resolve(item.Category).Name.Equals(_selectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (_showShortcutFavorites) filtered = filtered.Where(item => item.IsFavorite);
        if (_showShortcutPinned) filtered = filtered.Where(item => item.IsPinned);
        if (_showMostUsed)
        {
            filtered = filtered
                .Select(item => new { Snippet = item, Usage = _usageService.For(item.Id)?.Count ?? 0 })
                .Where(item => item.Usage > 0)
                .OrderByDescending(item => item.Usage)
                .ThenBy(item => item.Snippet.Trigger, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => item.Snippet);
        }
        else
        {
            filtered = filtered.OrderBy(item => item.Trigger, StringComparer.CurrentCultureIgnoreCase);
        }

        var visible = filtered.OrderByDescending(item => item.IsPinned).ToList();

        CategoriesPanel.Children.Clear();
        CategoriesPanel.Children.Add(CreateCategoryButton(null, UiText.Get("Todos"), _snippets.Count));
        foreach (var category in ShortcutCategories.All)
        {
            var count = _snippets.Count(item => ShortcutCategories.Resolve(item.Category).Name == category.Name);
            CategoriesPanel.Children.Add(CreateCategoryButton(category.Name, category.DisplayName, count));
        }

        UpdateShortcutItems(visible);

        SnippetCountText.Text = $"{visible.Count}/{_snippets.Count}";
        ShellCollectionStatusText.Text = UiText.Format($"{_snippets.Count} atalhos · {CategoriesPanel.Children.Count - 1} categorias");
        RefreshMostUsed();
    }

    private void RefreshMostUsed()
    {
        ApplyDisplayFilterState(DisplayAllButton, !_showMostUsed && !_showShortcutFavorites && !_showShortcutPinned);
        ApplyDisplayFilterState(DisplayFavoritesButton, _showShortcutFavorites);
        ApplyDisplayFilterState(DisplayPinnedButton, _showShortcutPinned);
        ApplyDisplayFilterState(DisplayMostUsedButton, _showMostUsed);
    }

    private void DisplayAll_OnClick(object sender, RoutedEventArgs e)
    {
        _showMostUsed = false; _showShortcutFavorites = false; _showShortcutPinned = false;
        RefreshNavigation();
    }

    private void DisplayMostUsed_OnClick(object sender, RoutedEventArgs e)
    {
        _showMostUsed = true; _showShortcutFavorites = false; _showShortcutPinned = false;
        RefreshNavigation();
    }

    private void ApplyDisplayFilterState(Button button, bool selected)
    {
        button.SetResourceReference(Button.BackgroundProperty, selected ? "Lab.tint" : "Lab.shell");
        button.SetResourceReference(Button.BorderBrushProperty, selected ? "Lab.line-strong" : "Lab.line");
        button.SetResourceReference(Button.ForegroundProperty, selected ? "Lab.accent-text" : "Lab.muted");
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void SelectSnippet(Snippet snippet)
    {
        if (ReferenceEquals(snippet, _selected) || !CanDiscardShortcutDraft()) return;
        _loadingShortcutDraft = true;
        _selected = snippet;
        _shortcutDuplicateSource = null;
        NameBox.Text = snippet.Name;
        TriggerBox.Text = snippet.Trigger;
        SelectShortcutCategory(snippet.Category);
        FormatBox.SelectedIndex = snippet.Format == SnippetFormat.Markdown ? 1 : 0;
        RichTextMarkdownConverter.Load(ContentEditor, snippet.Content, snippet.Format);
        StatusText.Text = snippet.HasLegacyIncompatibleTrigger
            ? UiText.Get("Atalho legado preservado, mas incompatível com o monitor atual; corrija o gatilho para reativá-lo")
            : snippet.Enabled ? UiText.Get("Atalho ativo") : UiText.Get("Atalho pausado");
        _loadingShortcutDraft = false;
        ResetShortcutDraftBaseline();
        RefreshShortcutListSelection();
        UpdatePreview();
        if (ShortcutEditorPanel.IsLoaded) LabMotion.PlayEntrance(ShortcutEditorPanel);
    }

    private void NewSnippet_OnClick(object sender, RoutedEventArgs e) =>
        BeginNewSnippet();

    private void BeginNewSnippet(bool discardAlreadyConfirmed = false)
    {
        if (!discardAlreadyConfirmed && !CanDiscardShortcutDraft()) return;
        _shortcutDuplicateSource = null;
        _loadingShortcutDraft = true;
        _selected = null;
        NameBox.Clear();
        TriggerBox.Text = "/";
        SelectShortcutCategory("Geral");
        FormatBox.SelectedIndex = 0;
        RichTextMarkdownConverter.Load(ContentEditor, string.Empty, SnippetFormat.Plain);
        StatusText.Text = UiText.Get("Novo atalho");
        _loadingShortcutDraft = false;
        ResetShortcutDraftBaseline();
        RefreshNavigation();
        NameBox.Focus();
        UpdatePreview();
    }

    private async void SaveSnippet_OnClick(object sender, RoutedEventArgs e) => await TrySaveShortcutAsync();

    private async System.Threading.Tasks.Task<bool> TrySaveShortcutAsync()
    {
        if (!EnsureSnippetStorageAvailable())
        {
            return false;
        }

        if (_shortcutOperationInProgress) return false;
        var previous = _selected;
        var format = FormatBox.SelectedIndex == 1 ? SnippetFormat.Markdown : SnippetFormat.Plain;
        var candidate = new Snippet
        {
            Id = previous?.Id ?? Guid.NewGuid(),
            Name = NameBox.Text.Trim(),
            Trigger = TriggerBox.Text.Trim(),
            Category = SelectedShortcutCategory,
            Content = RichTextMarkdownConverter.Save(ContentEditor, format),
            Format = format,
            Enabled = previous?.Enabled ?? _shortcutDuplicateSource?.Enabled ?? true,
            IsFavorite = previous?.IsFavorite ?? false, IsPinned = previous?.IsPinned ?? false,
            ConfirmKeys = previous?.ConfirmKeys.ToList() ?? _shortcutDuplicateSource?.ConfirmKeys.ToList() ?? ["Enter", "Tab", "Space"]
        };

        if (!TriggerRule.TryValidate(candidate.Trigger, out var triggerError))
        {
            ShowAppDialog(
                triggerError,
                UiText.Get("Atalho incompatível"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            TriggerBox.Focus();
            return false;
        }

        var nextState = _snippets
            .Where(item => !ReferenceEquals(item, previous))
            .Append(candidate)
            .ToList();

        try
        {
            _shortcutOperationInProgress = true;
            ShortcutEditorPanel.IsEnabled = false;
            await _repository.SaveAsync(nextState);
            if (previous is null)
            {
                _snippets.Add(candidate);
            }
            else
            {
                _snippets[_snippets.IndexOf(previous)] = candidate;
            }

            _selected = candidate;
            _keyboardHook.UpdateSnippets(_snippets);
            RefreshNavigation();
            RefreshStatistics();
            ResetShortcutDraftBaseline();
            ShortcutLegacyCategoryHint.Visibility = Visibility.Collapsed;
            StatusText.Text = UiText.Format($"Salvo em {AppPaths.SnippetsFile}");
            return true;
        }
        catch (Exception exception)
        {
            ShowAppDialog(
                exception.Message,
                UiText.Get("Não foi possível salvar"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
        finally
        {
            _shortcutOperationInProgress = false;
            ShortcutEditorPanel.IsEnabled = true;
            RefreshShortcutDraftState();
        }
    }

    private async void DeleteSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        if (!EnsureSnippetStorageAvailable())
        {
            return;
        }

        if (_selected is null || _shortcutOperationInProgress)
        {
            return;
        }

        var confirmation = new ShortcutConfirmationWindow(UiText.Get("Excluir atalho?"), UiText.Get("Este atalho será removido da sua lista."),
            $"{_selected.Name}\n{_selected.Trigger} · {_selected.Category}", UiText.Get("Excluir atalho")) { Owner = this };
        LabMotion.SetReduced(confirmation, LabMotion.GetReduced(this)); confirmation.EnableBackdrop();
        if (!ShowCaptureDialog(confirmation))
        {
            return;
        }

        var removed = _selected;
        try
        {
            _shortcutOperationInProgress = true; ShortcutEditorPanel.IsEnabled = false;
            await _repository.SaveAsync(_snippets.Where(item => !ReferenceEquals(item, removed)).ToList());
            _snippets.Remove(removed);
            _shortcutOperationInProgress = false;
            _keyboardHook.UpdateSnippets(_snippets);
            _shortcutDraftBaseline = null;
            BeginNewSnippet();
            RefreshStatistics();
            StatusText.Text = UiText.Get("Atalho excluído");
        }
        catch (Exception exception)
        {
            RefreshNavigation();
            ShowAppDialog(
                exception.Message,
                UiText.Get("Não foi possível excluir"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _shortcutOperationInProgress = false; ShortcutEditorPanel.IsEnabled = true;
            RefreshShortcutDraftState();
        }
    }

    private void FormatBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FormattingToolbar is not null)
        {
            FormattingToolbar.Visibility =
                FormatBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            RefreshShortcutDraftState();
            UpdatePreview();
        }
    }

    private void Bold_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleBold.Execute(null, ContentEditor);

    private void Italic_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleItalic.Execute(null, ContentEditor);

    private void Underline_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleUnderline.Execute(null, ContentEditor);

    private void FontFamilyBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncShortcutFormatting || ContentEditor is null ||
            FontFamilyBox?.SelectedItem is not ComboBoxItem { Tag: string font })
        {
            return;
        }

        ApplyShortcutFormatting(TextElement.FontFamilyProperty, new FontFamily(font));
    }

    private void FontSizeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncShortcutFormatting || ContentEditor is null ||
            FontSizeBox?.SelectedItem is not ComboBoxItem { Tag: string sizeText } ||
            !double.TryParse(sizeText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var size)) return;
        ApplyShortcutFormatting(TextElement.FontSizeProperty, size);
    }

    private void Color_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button anchor) ShowShortcutColorPicker(anchor, TextElement.ForegroundProperty);
    }

    private void Highlight_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button anchor) ShowShortcutColorPicker(anchor, TextElement.BackgroundProperty);
    }

    private void Bullets_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleBullets.Execute(null, ContentEditor);

    private void Numbering_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleNumbering.Execute(null, ContentEditor);

    private void AlignLeft_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.AlignLeft.Execute(null, ContentEditor);

    private void AlignCenter_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.AlignCenter.Execute(null, ContentEditor);

    private void AlignRight_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.AlignRight.Execute(null, ContentEditor);

    private void Table_OnClick(object sender, RoutedEventArgs e)
    {
        var rowsText = PromptDialog.Show(this, UiText.Get("Inserir tabela"), UiText.Get("Quantidade de linhas"), "2");
        if (rowsText is null)
        {
            return;
        }

        var columnsText = PromptDialog.Show(this, UiText.Get("Inserir tabela"), UiText.Get("Quantidade de colunas"), "2");
        if (!int.TryParse(rowsText, out var rows) ||
            !int.TryParse(columnsText, out var columns) ||
            rows is < 1 or > 10 ||
            columns is < 1 or > 8)
        {
            ShowAppDialog(
                UiText.Get("Use de 1 a 10 linhas e de 1 a 8 colunas."),
                UiText.Get("Tabela"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 6, 0, 6)
        };
        for (var column = 0; column < columns; column++)
        {
            table.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        TableCell? firstCell = null;
        for (var rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            var row = new TableRow();
            group.Rows.Add(row);
            for (var columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                var cell = new TableCell(new Paragraph(new Run(
                    rowIndex == 0 ? UiText.Format($"Título {columnIndex + 1}") : UiText.Get("Texto"))))
                {
                    BorderBrush = (Brush)FindResource("DividerBrush"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(7)
                };
                firstCell ??= cell;
                row.Cells.Add(cell);
            }
        }

        var paragraph = ContentEditor.CaretPosition.Paragraph;
        if (paragraph?.Parent is FlowDocument document)
        {
            document.Blocks.InsertAfter(paragraph, table);
        }
        else
        {
            ContentEditor.Document.Blocks.Add(table);
        }

        if (firstCell is not null)
        {
            ContentEditor.CaretPosition = firstCell.ContentStart;
        }
        ContentEditor.Focus();
        UpdatePreview();
    }

    private void Link_OnClick(object sender, RoutedEventArgs e)
    {
        var urlText = PromptDialog.Show(this, UiText.Get("Inserir hiperlink"), UiText.Get("Endereço (https://...)"));
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var uri))
        {
            if (urlText is not null)
            {
                ShowAppDialog(UiText.Get("Informe um endereço completo iniciado por http:// ou https://."));
            }
            return;
        }

        if (ContentEditor.Selection.IsEmpty)
        {
            var label = PromptDialog.Show(this, UiText.Get("Texto do link"), UiText.Get("Texto exibido"), urlText);
            if (label is null)
            {
                return;
            }

            ContentEditor.Selection.Text = $"[{label}]({uri})";
        }
        else
        {
            try
            {
                _ = new Hyperlink(ContentEditor.Selection.Start, ContentEditor.Selection.End)
                {
                    NavigateUri = uri,
                    Foreground = (Brush)FindResource("AccentBrush")
                };
            }
            catch (InvalidOperationException)
            {
                var label = ContentEditor.Selection.Text;
                ContentEditor.Selection.Text = $"[{label}]({uri})";
            }
        }

        ContentEditor.Focus();
    }

    private void Image_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = UiText.Get("Adicionar imagem ao atalho"),
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.gif;*.webp",
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        var info = new System.IO.FileInfo(picker.FileName);
        if (info.Length > 5 * 1024 * 1024)
        {
            ShowAppDialog(
                UiText.Get("Escolha uma imagem com até 5 MB."),
                UiText.Get("Imagem muito grande"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        System.IO.Directory.CreateDirectory(AppPaths.AssetsDirectory);
        var safeName = $"{Guid.NewGuid():N}{info.Extension.ToLowerInvariant()}";
        var destination = System.IO.Path.Combine(AppPaths.AssetsDirectory, safeName);
        System.IO.File.Copy(info.FullName, destination);
        ContentEditor.Selection.Text = $"![{System.IO.Path.GetFileNameWithoutExtension(info.Name)}](assets/{safeName})";
        ContentEditor.Focus();
        UpdatePreview();
    }

    private void VariableChip_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string token })
        {
            ContentEditor.Selection.Text = token;
            ContentEditor.Focus();
        }
    }

    private void ContentEditor_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (ContentEditor is not null)
        {
            foreach (var block in ContentEditor.Document.Blocks.OfType<BlockUIContainer>())
                ShortcutCodeBlockView.AttachBlock(block, view => OpenCodeBlock(view));
            ShortcutEmojiAssets.RestoreDocument(ContentEditor.Document);
        }
        RefreshShortcutDraftState();
        RefreshShortcutFormatting();
        UpdatePreview();
    }

    private void EditorField_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshShortcutDraftState();
    }

    private void UpdatePreview()
    {
        if (PreviewDocument is null || ContentEditor is null || FormatBox is null)
        {
            return;
        }

        var format = FormatBox.SelectedIndex == 1 ? SnippetFormat.Markdown : SnippetFormat.Plain;
        var content = RichTextMarkdownConverter.Save(ContentEditor, format);
        var rendered = new TemplateEngine().Render(content, PreviewValues(content, format), literalCodeBlocks: format == SnippetFormat.Markdown);
        RichTextMarkdownConverter.BuildPreview(PreviewDocument, rendered, format);
    }

    private static IReadOnlyDictionary<string, string> PreviewValues(string template, SnippetFormat format)
    {
        var engine = new TemplateEngine();
        return engine.GetFillableFields(template, literalCodeBlocks: format == SnippetFormat.Markdown)
            .ToDictionary(
                item => item.Name,
                item => item.DefaultValue ?? $"[{item.Name}]",
                StringComparer.CurrentCultureIgnoreCase);
    }

    private void ReplaceList(IEnumerable<Snippet> snippets)
    {
        _snippets.Clear();
        foreach (var snippet in snippets)
        {
            _snippets.Add(snippet);
        }
        RefreshNavigation();
    }
}

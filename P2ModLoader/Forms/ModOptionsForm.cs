using P2ModLoader.Data;
using P2ModLoader.Helper;
using System.Text.Json;
using P2ModLoader.ModList;

namespace P2ModLoader.Forms;

public sealed class ModOptionsForm : Form {
    private readonly Mod _mod;
    private readonly Dictionary<string, Control> _optionControls = new();
    private readonly Dictionary<string, ModOptions.Option> _optionsByName = new(), _optionsByMacro = new();
    private readonly Dictionary<string, Panel> _categoryPanels = new();
    private bool _hasChanges;
    private TextBox? _searchBox;
    private Panel? _categoriesPanel;
    private RichTextBox? _descriptionBox;

    public ModOptionsForm(Mod mod) {
        Icon = AppIcon.Instance;
        _mod = mod;
        BuildOptionLookup();
        InitializeComponents();
        LoadOptions();
        UpdateDependencies();
    }

    private void BuildOptionLookup() {
        if (_mod.Options == null) return;

        foreach (var option in _mod.Options.Categories.SelectMany(category => category.Options)) {
            if (!string.IsNullOrEmpty(option.Macro)) 
                _optionsByMacro[option.Macro] = option;
        }
    }

    private void InitializeComponents() {
        Text = $"{_mod.Info.Name} - Configure";
        Size = new Size(1200, 700);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(800, 500);

        var mainLayout = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 2,
            Padding = new Padding(10)
        };
        
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

        mainLayout.Controls.Add(CreateLeftPanel(), 0, 0);
        mainLayout.Controls.Add(CreateRightPanel(), 1, 0);
        
        var buttonPanel = CreateButtonPanel();
        mainLayout.Controls.Add(buttonPanel, 0, 1);
        mainLayout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(mainLayout);
    }

    private Panel CreateLeftPanel() {
        var panel = new Panel { Dock = DockStyle.Fill };
        
        var layout = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var searchPanel = new FlowLayoutPanel {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };

        var searchLabel = new Label {
            Text = "Search:",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 5, 0)
        };

        _searchBox = new TextBox {
            PlaceholderText = "Type to filter options...",
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 10F),
            Width = 250
        };
        _searchBox.TextChanged += SearchBox_TextChanged;

        searchPanel.Controls.AddRange([searchLabel, _searchBox]);

        var scrollPanel = new Panel {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(0)
        };

        _categoriesPanel = new Panel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(0)
        };

        scrollPanel.Controls.Add(_categoriesPanel);

        layout.Controls.Add(searchPanel, 0, 0);
        layout.Controls.Add(scrollPanel, 0, 1);

        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateRightPanel() {
        var panel = new Panel { Dock = DockStyle.Fill };

        var layout = new TableLayoutPanel {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var descLabel = new Label {
            Text = "Description:",
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft,
            Padding = new Padding(0, 0, 0, 3)
        };

        _descriptionBox = new RichTextBox {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = SystemColors.Window,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 9.5F)
        };

        layout.Controls.Add(descLabel, 0, 0);
        layout.Controls.Add(_descriptionBox, 0, 1);

        panel.Controls.Add(layout);
        return panel;
    }

    private FlowLayoutPanel CreateButtonPanel() {
        var panel = new FlowLayoutPanel {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 5, 0, 5)
        };

        var cancelButton = CreateButton("Cancel", DialogResult.Cancel);
        var saveButton = CreateButton("Save", DialogResult.OK, SaveButton_Click);
        var resetButton = CreateButton("Reset", DialogResult.None, ResetButton_Click);

        panel.Controls.AddRange([cancelButton, saveButton, resetButton]);
        return panel;
    }

    private Button CreateButton(string text, DialogResult result, EventHandler? clickHandler = null) {
        var button = new Button {
            Text = text,
            Width = 100,
            Height = 35,
            DialogResult = result
        };
        if (clickHandler != null) button.Click += clickHandler;
        return button;
    }

    private void LoadOptions() {
        if (_mod.Options == null || _categoriesPanel == null) return;

        var yPos = 5;
        var categoryNumber = 1;
        foreach (var category in _mod.Options.Categories) {
            var categoryPanel = CreateCategoryPanel(category, categoryNumber, ref yPos);
            _categoryPanels[category.Name] = categoryPanel;
            _categoriesPanel.Controls.Add(categoryPanel);
            categoryNumber++;
        }
    }

    private Panel CreateCategoryPanel(ModOptions.Category category, int categoryNumber, ref int yPos) {
        var panel = new Panel {
            Width = _categoriesPanel!.Width - 25,
            Location = new Point(5, yPos),
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
        };

        var headerPanel = CreateCategoryHeader(category, categoryNumber);
        var contentPanel = CreateCategoryContent(category);

        var isCollapsed = false;
        EventHandler toggleCollapse = (s, e) => {
            isCollapsed = !isCollapsed;
            contentPanel.Visible = !isCollapsed;
            var arrow = headerPanel.Controls.OfType<Label>().First();
            arrow.Text = isCollapsed ? "▶" : "▼";
            panel.Height = isCollapsed ? headerPanel.Height + 10 : contentPanel.Bottom + 10;
        };

        foreach (Control ctrl in headerPanel.Controls) {
            ctrl.Click += toggleCollapse;
        }
        headerPanel.Click += toggleCollapse;

        panel.Controls.AddRange([headerPanel, contentPanel]);
        panel.Height = contentPanel.Bottom + 10;

        yPos += panel.Height + 10;
        return panel;
    }

    private Panel CreateCategoryHeader(ModOptions.Category category, int categoryNumber) {
        var headerPanel = new Panel {
            Width = _categoriesPanel!.Width - 35,
            Height = 35,
            Location = new Point(5, 0),
            Cursor = Cursors.Hand,
            BackColor = SystemColors.ControlLight
        };

        var arrow = new Label {
            Text = "▼",
            Location = new Point(8, 5),
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 10F),
            Cursor = Cursors.Hand
        };

        var headerLabel = new Label {
            Text = $"{categoryNumber}. {category.Name}",
            Location = new Point(30, 5),
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 11F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };

        if (!string.IsNullOrEmpty(category.Description)) {
            headerPanel.MouseEnter += (s, e) => SetDescriptionText(category.Description);
        }

        headerPanel.Controls.AddRange([arrow, headerLabel]);
        return headerPanel;
    }

    private Panel CreateCategoryContent(ModOptions.Category category) {
        var contentPanel = new Panel {
            Width = _categoriesPanel!.Width - 35,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = new Point(5, 40),
            Padding = new Padding(10, 5, 5, 5)
        };

        var optionY = 0;
        foreach (var option in category.Options) {
            var optionControl = CreateOptionControl(option, contentPanel.Width - 15);
            optionControl.Location = new Point(0, optionY);
            contentPanel.Controls.Add(optionControl);
            optionY = optionControl.Bottom + 3;
        }

        return contentPanel;
    }

    private Panel CreateOptionControl(ModOptions.Option option, int width) {
        var panel = new Panel {
            Width = width,
            Height = 38,
            Margin = new Padding(0, 2, 0, 2)
        };

        var nameLabel = new Label {
            Text = option.Name + ":",
            Location = new Point(0, 0),
            Width = (int)(width * 0.4),
            Height = 38,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        Control valueControl = CreateValueControl(option, width);
        _optionControls[option.Name] = valueControl;

        AttachDescriptionHandlers(option, nameLabel, valueControl, panel);

        panel.Controls.AddRange([nameLabel, valueControl]);
        return panel;
    }

    private Control CreateValueControl(ModOptions.Option option, int panelWidth) {
        Control control = option.Type switch {
            ModOptions.OptionType.Boolean => new CheckBox { 
                Width = 20, 
                Height = 20, 
                Location = new Point(0, 12) 
            },
            ModOptions.OptionType.Integer => CreateNumeric(option, true),
            ModOptions.OptionType.Decimal => CreateNumeric(option, false),
            ModOptions.OptionType.Combo => CreateCombo(option),
            _ => new Label { Text = "Unknown" }
        };

        if (control is CheckBox cb) {
            cb.Checked = GetBoolValue(option.CurrentValue, option.DefaultValue);
            cb.CheckedChanged += OnValueChanged;
        } else if (control is NumericUpDown nud) {
            nud.Value = GetDecimalValue(option.CurrentValue, option.DefaultValue);
            nud.ValueChanged += OnValueChanged;
        } else if (control is ComboBox combo) {
            LoadComboValues(option, combo);
            combo.SelectedIndexChanged += OnValueChanged;
        }

        control.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        control.Location = new Point(panelWidth - (int)(panelWidth * 0.55) - 10 + 
            (int)(panelWidth * 0.55) - control.Width, control.Location.Y);

        return control;
    }

    private bool GetBoolValue(object? current, object? fallback) {
        if (current is bool b) return b;
        if (current is JsonElement je && je.ValueKind == JsonValueKind.True) return true;
        if (current is JsonElement je2 && je2.ValueKind == JsonValueKind.False) return false;
        if (fallback is bool fb) return fb;
        if (fallback is JsonElement jef && jef.ValueKind == JsonValueKind.True) return true;
        return false;
    }

    private decimal GetDecimalValue(object? current, object? fallback) {
        var value = JsonHelper.TryGetDecimal(current);
        if (value.HasValue) return value.Value;
        
        value = JsonHelper.TryGetDecimal(fallback);
        return value ?? 0;
    }

    private NumericUpDown CreateNumeric(ModOptions.Option option, bool isInteger) {
        var minVal = (decimal)(option.MinValue ?? (isInteger ? int.MinValue : double.MinValue));
        var maxVal = (decimal)(option.MaxValue ?? (isInteger ? int.MaxValue : double.MaxValue));
        
        return new NumericUpDown {
            Width = 100,
            Location = new Point(0, 5),
            DecimalPlaces = isInteger ? 0 : 2,
            Minimum = minVal,
            Maximum = maxVal
        };
    }

    private ComboBox CreateCombo(ModOptions.Option option) {
        var combo = new ComboBox {
            Location = new Point(0, 5),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        if (option.Options != null && option.Options.Count > 0) {
            using var g = combo.CreateGraphics();
            var maxWidth = option.Options.Max(o => (int)g.MeasureString(o.Label, combo.Font).Width);
            combo.Width = Math.Max(100, maxWidth + 30);
        } else {
            combo.Width = 150;
        }

        return combo;
    }

    private void LoadComboValues(ModOptions.Option option, ComboBox combo) {
        if (option.Options == null) return;

        foreach (var opt in option.Options) {
            combo.Items.Add(opt.Label);
        }

        var currentMacro = GetStringValue(option.CurrentValue);
        
        if (!string.IsNullOrEmpty(currentMacro)) {
            var index = option.Options.FindIndex(o => o.Macro == currentMacro);
            if (index >= 0) combo.SelectedIndex = index;
        } else if (combo.Items.Count > 0) {
            combo.SelectedIndex = 0;
        }
    }

    private string? GetStringValue(object? value) {
        if (value is string s) return s;
        if (value is JsonElement je && je.ValueKind == JsonValueKind.String) {
            return je.GetString();
        }
        return null;
    }

    private void AttachDescriptionHandlers(ModOptions.Option option, params Control[] controls) {
        void ShowDescription(object? s, EventArgs e) {
            if (_descriptionBox == null) return;

            if (option.Type == ModOptions.OptionType.Combo && option.Options != null) {
                var combo = _optionControls[option.Name] as ComboBox;
                var selectedIdx = combo?.SelectedIndex ?? -1;
                
                _descriptionBox.Clear();
                _descriptionBox.SelectionIndent = 8;
                _descriptionBox.SelectionRightIndent = 8;
                
                _descriptionBox.AppendText(option.Description);
                _descriptionBox.AppendText("\n\nValues:");
                
                for (var i = 0; i < option.Options.Count; i++) {
                    var opt = option.Options[i];
                    var optDesc = !string.IsNullOrEmpty(opt.Description) ? $" - {opt.Description}" : "";
                    
                    _descriptionBox.AppendText($"\n{opt.Label}{optDesc}");
                    
                    if (i == selectedIdx) {
                        _descriptionBox.AppendText(" (selected)");
                        var text = _descriptionBox.Text;
                        var start = text.LastIndexOf(" (selected)");
                        
                        if (start >= 0) {
                            _descriptionBox.Select(start, 11);
                            _descriptionBox.SelectionFont = new Font(_descriptionBox.Font, FontStyle.Bold);
                            _descriptionBox.Select(_descriptionBox.Text.Length, 0);
                            _descriptionBox.SelectionFont = _descriptionBox.Font;
                        }
                    }
                }
            } else {
                SetDescriptionText(option.Description);
            }
        }

        foreach (var control in controls) {
            control.MouseEnter += ShowDescription;
            control.Enter += ShowDescription;
        }
    }

    private void SetDescriptionText(string text) {
        if (_descriptionBox == null) return;
        _descriptionBox.Clear();
        _descriptionBox.SelectionIndent = 8;
        _descriptionBox.SelectionRightIndent = 8;
        _descriptionBox.Text = text;
    }

    private void OnValueChanged(object? sender, EventArgs e) {
        _hasChanges = true;
        UpdateDependencies();
    }

    private void UpdateDependencies() {
        if (_mod.Options == null) return;

        foreach (var category in _mod.Options.Categories) {
            foreach (var option in category.Options) {
                if (string.IsNullOrEmpty(option.DependsOn)) continue;
                if (!_optionControls.TryGetValue(option.Name, out var control)) continue;

                var isEnabled = IsDependencyMet(option.DependsOn);
                control.Enabled = isEnabled;
                
                if (control.Parent is Panel parentPanel) {
                    foreach (Control c in parentPanel.Controls) {
                        if (c is Label label) {
                            label.Enabled = isEnabled;
                            label.ForeColor = isEnabled ? SystemColors.ControlText : SystemColors.GrayText;
                        }
                    }
                }
            }
        }
    }

    private bool IsDependencyMet(string dependency) {
        if (!_optionsByMacro.TryGetValue(dependency, out var depOption)) return true;
        if (!_optionControls.TryGetValue(dependency, out var depControl)) return true;

        return depOption.Type switch {
            ModOptions.OptionType.Boolean => (depControl as CheckBox)?.Checked ?? false,
            ModOptions.OptionType.Combo => GetCurrentMacro(depOption, depControl as ComboBox) == dependency,
            _ => true
        };
    }

    private string? GetCurrentMacro(ModOptions.Option option, ComboBox? combo) {
        if (combo == null || option.Options == null) return null;
        var idx = combo.SelectedIndex;
        return idx >= 0 && idx < option.Options.Count ? option.Options[idx].Macro : null;
    }

    private void SearchBox_TextChanged(object? sender, EventArgs e) {
        if (_mod.Options == null) return;

        var searchText = _searchBox?.Text.ToLowerInvariant() ?? "";
        
        foreach (var category in _mod.Options.Categories) {
            if (!_categoryPanels.TryGetValue(category.Name, out var panel)) continue;

            var hasVisibleOptions = false;
            foreach (Control control in panel.Controls) {
                if (control is not Panel contentPanel || contentPanel.Controls.Count == 0) continue;

                foreach (Control optionControl in contentPanel.Controls) {
                    if (optionControl is not Panel optPanel) continue;

                    var nameLabel = optPanel.Controls.OfType<Label>().FirstOrDefault();
                    var matches = string.IsNullOrEmpty(searchText) || 
                                  (nameLabel?.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
                    
                    optPanel.Visible = matches;
                    if (matches) hasVisibleOptions = true;
                }
            }

            panel.Visible = hasVisibleOptions;
        }
    }

    private void ResetButton_Click(object? sender, EventArgs e) {
        if (_mod.Options == null) return;

        foreach (var category in _mod.Options.Categories) {
            foreach (var option in category.Options) {
                if (!_optionControls.TryGetValue(option.Name, out var control)) continue;

                switch (option.Type) {
                    case ModOptions.OptionType.Boolean:
                        if (control is CheckBox cb)
                            cb.Checked = GetBoolValue(option.DefaultValue, null);
                        break;
                        
                    case ModOptions.OptionType.Integer:
                    case ModOptions.OptionType.Decimal:
                        if (control is NumericUpDown nud)
                            nud.Value = GetDecimalValue(option.DefaultValue, null);
                        break;
                        
                    case ModOptions.OptionType.Combo:
                        if (control is ComboBox combo && option.Options != null) {
                            var defaultMacro = GetStringValue(option.DefaultValue);
                            
                            if (!string.IsNullOrEmpty(defaultMacro)) {
                                var index = option.Options.FindIndex(o => o.Macro == defaultMacro);
                                if (index >= 0) combo.SelectedIndex = index;
                            }
                        }
                        break;
                }
            }
        }

        _hasChanges = true;
        UpdateDependencies();
    }

    private void SaveButton_Click(object? sender, EventArgs e) {
        if (!_hasChanges) return;

        SaveOptions();
        SettingsHolder.IsPatched = false;
    }

    private void SaveOptions() {
        if (_mod.Options == null) return;

        foreach (var category in _mod.Options.Categories) {
            foreach (var option in category.Options) {
                if (!_optionControls.TryGetValue(option.Name, out var control)) continue;

                option.CurrentValue = option.Type switch {
                    ModOptions.OptionType.Boolean => (control as CheckBox)?.Checked,
                    ModOptions.OptionType.Integer => (int)(control as NumericUpDown)?.Value!,
                    ModOptions.OptionType.Decimal => (double)(control as NumericUpDown)?.Value!,
                    ModOptions.OptionType.Combo => option.Options?[(control as ComboBox)?.SelectedIndex ?? 0].Macro,
                    _ => null
                };

                _mod.OptionValues[option.Name] = option.CurrentValue;
            }
        }

        SettingsHolder.UpdateModState(ModManager.Mods);
    }
}
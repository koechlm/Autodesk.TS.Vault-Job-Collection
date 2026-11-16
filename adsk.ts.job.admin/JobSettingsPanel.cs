using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using Autodesk.Connectivity.Explorer.Extensibility;
using VDF = Autodesk.DataManagement.Client.Framework;
using VDFV = Autodesk.DataManagement.Client.Framework.Vault;
using VDFVF = Autodesk.DataManagement.Client.Framework.Vault.Forms;

namespace adsk.ts.job.admin
{
    /// <summary>Settings of one job: Option | Value grid, description of the focused option, Save and Use local file.</summary>
    internal sealed class JobSettingsPanel : XtraUserControl
    {
        private readonly JobDefinition _job;
        private readonly IApplication _application;
        private readonly bool _canEdit;
        private readonly GridControl _grid = new() { Dock = DockStyle.Fill };
        private readonly GridView _view = new();
        private readonly LabelControl _banner = new() { AutoSizeMode = LabelAutoSizeMode.Vertical, Dock = DockStyle.Top, Visible = false };
        private readonly LabelControl _status = new() { AutoSizeMode = LabelAutoSizeMode.None, Dock = DockStyle.Bottom, Height = 22 };
        private readonly SimpleButton _save = new() { Text = "Save to Vault", Size = new Size(125, 26) };
        private readonly SimpleButton _reset = new() { Text = "Use local file", Size = new Size(100, 26) };
        private readonly MemoEdit _description = new() { Dock = DockStyle.Fill };
        private readonly ComboBoxEdit _engine = new() { Width = 220 };
        private bool _loadingEngine;

        private readonly RepositoryItemTextEdit _textEdit = new();
        private readonly RepositoryItemCheckEdit _checkEdit = new() { ValueChecked = "True", ValueUnchecked = "False" };
        private readonly RepositoryItemSpinEdit _numberEdit = new();
        private readonly RepositoryItemButtonEdit _folderEdit = new();
        private readonly RepositoryItemButtonEdit _fileEdit = new();
        private readonly RepositoryItemButtonEdit _vaultPathEdit = new();
        private readonly Dictionary<string, RepositoryItemComboBox> _comboEdits = new();
        private readonly Dictionary<string, RepositoryItemCheckedComboBoxEdit> _checkedListEdits = new();

        private BindingList<JobSettingRow> _rows = new();

        public JobSettingsPanel(JobDefinition job, IApplication application, bool canEdit)
        {
            _job = job;
            _application = application;
            _canEdit = canEdit;
            Dock = DockStyle.Fill;

            BuildGrid();
            BuildButtons();
            LoadSettings();
        }

        private void BuildGrid()
        {
            _numberEdit.IsFloatValue = false;
            _numberEdit.MinValue = 0;
            _numberEdit.MaxValue = int.MaxValue;
            _folderEdit.ButtonPressed += (s, e) => BrowseFolder((ButtonEdit)s!);
            _fileEdit.ButtonPressed += (s, e) => BrowseFile((ButtonEdit)s!);
            _vaultPathEdit.ButtonPressed += (s, e) => BrowseVaultPath((ButtonEdit)s!);

            foreach (RepositoryItem item in new RepositoryItem[] { _textEdit, _checkEdit, _numberEdit, _folderEdit, _fileEdit, _vaultPathEdit })
                _grid.RepositoryItems.Add(item);

            _grid.MainView = _view;
            _grid.ViewCollection.Add(_view);

            _view.GridControl = _grid;
            _view.OptionsView.ShowGroupPanel = false;
            _view.OptionsView.ShowIndicator = false;
            _view.OptionsView.RowAutoHeight = false;
            _view.OptionsView.ColumnAutoWidth = true;
            _view.OptionsCustomization.AllowSort = false;
            _view.OptionsCustomization.AllowColumnMoving = false;
            _view.OptionsSelection.EnableAppearanceFocusedCell = false;
            _view.CustomRowCellEdit += OnCustomRowCellEdit;
            _view.FocusedRowChanged += (s, e) => ShowDescription();

            Controls.Add(_grid);

            _description.Properties.ReadOnly = true;
            _description.Properties.WordWrap = true;
            _description.Properties.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            GroupControl descriptionGroup = new() { Text = "Description", Dock = DockStyle.Bottom, Height = 90 };
            descriptionGroup.Controls.Add(_description);
            Controls.Add(descriptionGroup);

            if (_job.EngineKey != null) BuildEngineSelector();

            if (!_canEdit)
            {
                _banner.Text = "You need the Vault permission 'Vault Set Options' to change the settings. The settings are read-only.";
                _banner.Visible = true;
                Controls.Add(_banner);
            }
        }

        private void BuildGrid_Columns()
        {
            _view.Columns.Clear();
            _view.Columns.AddVisible(nameof(JobSettingRow.DisplayName), "Option");
            _view.Columns.AddVisible(nameof(JobSettingRow.Value), "Value");

            _view.Columns[nameof(JobSettingRow.DisplayName)].OptionsColumn.AllowEdit = false;
            _view.Columns[nameof(JobSettingRow.DisplayName)].Width = 220;
            _view.Columns[nameof(JobSettingRow.Value)].Width = 330;
        }

        /// <summary>Sets the button icons for the current Vault theme; called again when the theme changes.</summary>
        public void RefreshIcons()
        {
            _save.ImageOptions.Image = JobAdminIcons.Get(JobAdminIcons.CheckIn);
        }

        private void ShowDescription()
        {
            _description.Text = _view.GetFocusedRow() is JobSettingRow row
                ? "Setting: " + row.Key + (row.Description.Length > 0 ? Environment.NewLine + row.Description : "")
                : "Select an option to see its description.";
        }

        /// <summary>Engine pulldown above the grid; the grid lists the general options plus those of the selected engine.</summary>
        private void BuildEngineSelector()
        {
            _engine.Properties.TextEditStyle = TextEditStyles.DisableTextEditor;
            _engine.Properties.Items.AddRange(_job.EngineOptions!.Keys.ToArray());
            _engine.EditValueChanged += (s, e) =>
            {
                if (_loadingEngine) return;
                if (EngineRow != null && _engine.EditValue is string engine) EngineRow.Value = engine;
                ApplyEngineFilter();
            };

            PanelControl top = new() { Dock = DockStyle.Top, Height = 36, BorderStyle = BorderStyles.NoBorder };
            LabelControl caption = new() { Text = "Conversion engine", AutoSizeMode = LabelAutoSizeMode.Default };
            top.Controls.Add(caption);
            top.Controls.Add(_engine);

            // both controls keep their natural height, so center each one on the bar instead of docking them
            caption.Location = new Point(5, (top.Height - caption.Height) / 2);
            _engine.Location = new Point(caption.Right + 10, (top.Height - _engine.Height) / 2);
            Controls.Add(top);
        }

        private JobSettingRow? EngineRow => _rows.FirstOrDefault(r => r.Key == _job.EngineKey);

        private string? SelectedEngine => _job.EngineOptions!.Keys
            .FirstOrDefault(k => string.Equals(k, _engine.EditValue as string, StringComparison.OrdinalIgnoreCase));

        /// <summary>Hides the engine option itself and the options of the engines that are not selected.</summary>
        private void ApplyEngineFilter()
        {
            if (_job.EngineKey == null) return;

            string? selected = SelectedEngine;
            List<string> hidden = new() { _job.EngineKey };
            foreach (KeyValuePair<string, string[]> engine in _job.EngineOptions!)
                if (engine.Key != selected) hidden.AddRange(engine.Value);

            _view.ActiveFilterString = "Not [" + nameof(JobSettingRow.Key) + "] In ("
                + string.Join(", ", hidden.Select(k => "'" + k + "'")) + ")";
        }

        private void LoadEngine()
        {
            if (_job.EngineKey == null) return;

            _loadingEngine = true;
            JobSettingRow? row = EngineRow;
            string? current = _job.EngineOptions!.Keys
                .FirstOrDefault(k => string.Equals(k, row?.Value, StringComparison.OrdinalIgnoreCase));
            _engine.EditValue = current ?? _job.EngineOptions.Keys.First();
            _engine.ToolTip = row?.Description;
            _engine.Enabled = _canEdit && row != null;
            _loadingEngine = false;
            ApplyEngineFilter();
        }

        private void BuildButtons()
        {
            const int margin = 5;
            PanelControl bottom = new() { Dock = DockStyle.Bottom, Height = 26 + 2 * margin, BorderStyle = BorderStyles.NoBorder };
            _save.ImageOptions.ImageToTextAlignment = ImageAlignToText.LeftCenter;
            _save.ImageOptions.ImageToTextIndent = 10; // default 5 plus 5 px
            RefreshIcons();
            _save.Click += (s, e) => Save();
            _reset.Click += (s, e) => Reset();

            // docked buttons would stretch to the bar height and ignore the margin: place them at fixed offsets instead
            SimpleButton[] buttons = { _reset, _save };
            PanelControl buttonBar = new() { Dock = DockStyle.Right, BorderStyle = BorderStyles.NoBorder };
            int x = margin;
            foreach (SimpleButton button in buttons)
            {
                button.Location = new Point(x, margin);
                buttonBar.Controls.Add(button);
                x += button.Width + margin;
            }
            buttonBar.Width = x;

            _status.Appearance.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            _status.Padding = new Padding(margin, 0, 0, 0);

            bottom.Controls.Add(buttonBar);
            Controls.Add(_status);
            Controls.Add(bottom);
        }

        public bool HasUnsavedChanges => _rows.Any(r => r.IsModified);

        private void LoadSettings()
        {
            try
            {
                JobSettingsStore.LoadResult result = JobSettingsStore.Load(_application.Connection.WebServiceManager, _job);
                foreach (JobSettingRow row in result.Rows)
                    if (_job.ListOptions != null && _job.ListOptions.TryGetValue(row.Key, out string[]? choices))
                        row.UseCheckedList(choices);

                _rows = new BindingList<JobSettingRow>(result.Rows);
                _grid.DataSource = _rows;
                BuildGrid_Columns();
                LoadEngine();
                ShowDescription();
                SetReadOnly();

                string text = "Source: " + result.Source;
                if (result.Info.SavedBy != null) text += $" (saved by {result.Info.SavedBy}, {result.Info.SavedUtc})";
                if (result.Warning != null) text += " | " + result.Warning;
                _status.Text = text;
            }
            catch (Exception ex)
            {
                _status.Text = "Settings could not be loaded: " + ex.Message;
                _save.Enabled = _reset.Enabled = false;
                _view.OptionsBehavior.Editable = false;
            }
        }

        private void SetReadOnly()
        {
            _view.OptionsBehavior.Editable = _canEdit;
            _save.Enabled = _canEdit;
            _reset.Enabled = _canEdit;
        }

        private void OnCustomRowCellEdit(object sender, CustomRowCellEditEventArgs e)
        {
            if (e.Column.FieldName != nameof(JobSettingRow.Value)) return;
            if (_view.GetRow(e.RowHandle) is not JobSettingRow row) return;

            e.RepositoryItem = row.Editor switch
            {
                SettingEditor.Check => _checkEdit,
                SettingEditor.Number => _numberEdit,
                SettingEditor.FolderPath => _folderEdit,
                SettingEditor.FilePath => _fileEdit,
                SettingEditor.VaultPath => _vaultPathEdit,
                SettingEditor.Combo => GetComboEdit(row),
                SettingEditor.CheckedList => GetCheckedListEdit(row),
                _ => _textEdit,
            };
        }

        private RepositoryItemCheckedComboBoxEdit GetCheckedListEdit(JobSettingRow row)
        {
            if (!_checkedListEdits.TryGetValue(row.Key, out RepositoryItemCheckedComboBoxEdit? list))
            {
                list = new RepositoryItemCheckedComboBoxEdit { SeparatorChar = ',', TextEditStyle = TextEditStyles.DisableTextEditor };
                list.Items.AddRange(row.Choices.Select(c => new CheckedListBoxItem(c)).ToArray());
                _grid.RepositoryItems.Add(list);
                _checkedListEdits[row.Key] = list;
            }
            return list;
        }

        private RepositoryItemComboBox GetComboEdit(JobSettingRow row)
        {
            if (!_comboEdits.TryGetValue(row.Key, out RepositoryItemComboBox? combo))
            {
                combo = new RepositoryItemComboBox { TextEditStyle = TextEditStyles.DisableTextEditor };
                combo.Items.AddRange(row.Choices);
                _grid.RepositoryItems.Add(combo);
                _comboEdits[row.Key] = combo;
            }
            return combo;
        }

        /// <summary>
        /// Selects a Vault folder (SelectVaultFolder) or file (SelectEntity, filtered to the option's file type)
        /// and writes its $/ path to the cell.
        /// </summary>
        private void BrowseVaultPath(ButtonEdit edit)
        {
            if (_view.GetFocusedRow() is not JobSettingRow row || row.VaultPath is not VaultPathInfo info) return;

            try
            {
                if (info.IsFolder)
                {
                    VDFVF.Settings.SelectVaultFolderSettings folderSettings = new(_application.Connection)
                    {
                        Caption = "Select a Vault folder - " + JobSettingNames.Get(row.Key),
                        PersistenceKey = "adsk.ts.job.admin.SelectVaultFolder." + row.Key,
                        AllowNewFolderCreation = true,
                    };

                    // start at the folder in the text box, or at its nearest existing parent folder
                    VDFV.Currency.Entities.Folder? startFolder = FindNearestVaultFolder(edit.Text);
                    if (startFolder != null) folderSettings.InitialSelectedFolderPath = startFolder.FullName;

                    VDFVF.Results.SelectVaultFolderResults? folderResult = VDFVF.Library.SelectVaultFolder(folderSettings);
                    if (!string.IsNullOrEmpty(folderResult?.SelectedFolderFullName)) edit.EditValue = folderResult.SelectedFolderFullName;
                    return;
                }

                VDFVF.Settings.SelectEntitySettings settings = new()
                {
                    DialogCaption = "Select a Vault file - " + JobSettingNames.Get(row.Key),
                    MultipleSelect = false,
                    PersistenceKey = "adsk.ts.job.admin.SelectEntity." + row.Key,
                };

                // start at the file in the text box: browse its folder and preselect its file name
                if (edit.Text.Trim().StartsWith("$/", StringComparison.Ordinal))
                {
                    string existing = edit.Text.Trim().Replace('\\', '/');
                    settings.InitialSelectionText = existing;
                    int separator = existing.LastIndexOf('/');
                    VDFV.Currency.Entities.Folder? startFolder = FindNearestVaultFolder(separator > 0 ? existing[..separator] : existing);
                    if (startFolder != null) settings.InitialBrowseLocation = startFolder;
                }

                // only a file can be taken over with the action button
                settings.ActionableEntityClassIds.Clear();
                settings.ActionableEntityClassIds.Add(VDFV.Currency.Entities.EntityClassIds.Files);

                // preset the file type of the option, for example *.preset or *.rte
                var filter = new VDFVF.Settings.SelectEntitySettings.EntityRegularExpressionFilter(
                    info.Caption, "(?i).*" + System.Text.RegularExpressions.Regex.Escape(info.Extension!) + "$",
                    VDFV.Currency.Entities.EntityClassIds.Files,
                    VDFVF.Settings.SelectEntitySettings.EntityFilter.MultiSelectFlag.Disabled);
                settings.ConfigureFilters("File type", new[] { filter }, filter);

                VDFVF.Results.SelectEntityResults? result = VDFVF.Library.SelectEntity(_application.Connection, settings);
                string? path = ToVaultPath(result);
                if (!string.IsNullOrEmpty(path)) edit.EditValue = path;
            }
            catch (Exception ex)
            {
                VDF.Forms.Library.ShowError("The Vault selection could not be opened: " + ex.Message, "Job Collection");
            }
        }

        /// <summary>The Vault folder of a $/ path, or the nearest existing parent folder; null if there is none.</summary>
        private VDFV.Currency.Entities.Folder? FindNearestVaultFolder(string vaultPath)
        {
            string path = vaultPath.Trim().Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("$/", StringComparison.Ordinal)) return null;

            try
            {
                var connection = _application.Connection;
                while (path.Length > 1)
                {
                    Autodesk.Connectivity.WebServices.Folder? folder =
                        connection.WebServiceManager.DocumentService.FindFoldersByPaths(new[] { path })?.FirstOrDefault();
                    if (folder != null && folder.Id > 0) return new VDFV.Currency.Entities.Folder(connection, folder);

                    int separator = path.LastIndexOf('/');
                    path = separator > 0 ? path[..separator] : "";
                }
            }
            catch
            {
                // an unreachable or unknown path just means the dialog opens at its default location
            }
            return null;
        }

        private static string? ToVaultPath(VDFVF.Results.SelectEntityResults? result)
        {
            object? entity = result?.SelectedEntities?.FirstOrDefault();
            return entity switch
            {
                VDFV.Currency.Entities.FileIteration file => file.Parent.FullName.TrimEnd('/') + "/" + file.EntityName,
                _ => result?.SelectionText,
            };
        }

        private static void BrowseFolder(ButtonEdit edit)
        {
            using FolderBrowserDialog dialog = new() { SelectedPath = edit.Text };
            if (dialog.ShowDialog() == DialogResult.OK) edit.EditValue = dialog.SelectedPath;
        }

        private static void BrowseFile(ButtonEdit edit)
        {
            using OpenFileDialog dialog = new() { FileName = edit.Text };
            if (dialog.ShowDialog() == DialogResult.OK) edit.EditValue = dialog.FileName;
        }

        private void Save()
        {
            if (!_canEdit) return;
            // commit the value of the open cell editor before the rows are read
            _view.PostEditor();
            _view.CloseEditor();
            _view.UpdateCurrentRow();
            try
            {
                JobSettingsStore.Save(_application.Connection.WebServiceManager, _job, _rows, _application.Connection.UserName);
                LoadSettings();
            }
            catch (Exception ex)
            {
                VDF.Forms.Library.ShowError("The settings could not be saved to Vault: " + Describe(ex), "Job Collection");
            }
        }

        private void Reset()
        {
            if (!_canEdit || !Confirm("Delete the settings stored in Vault and use the local settings file again?")) return;
            try
            {
                JobSettingsStore.Reset(_application.Connection.WebServiceManager, _job);
                LoadSettings();
            }
            catch (Exception ex)
            {
                VDF.Forms.Library.ShowError(Describe(ex), "Job Collection");
            }
        }

        private static string Describe(Exception ex) =>
            ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase)
                ? "Vault rejected the change: your user lacks the Vault permission 'Vault Set Options'."
                : ex.Message;

        private bool Confirm(string message) =>
            VDF.Forms.Library.ShowQuestion(message, "Job Collection", VDF.Forms.Currency.ButtonConfiguration.YesNo, this,
                new Dictionary<VDF.Forms.Currency.CommonMessageBoxButton, string>()) == DialogResult.Yes;
    }
}

using BannerlordExpanded.WandererCreator.Models;
using BannerlordExpanded.WandererCreator.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BannerlordExpanded.WandererCreator.UI
{
    public class CreatorForm : Form
    {
        private MenuStrip _menuStrip;
        private ListBox _wandererList;
        private TextBox _nameBox;
        private TextBox? _idBox; // New ID Field
        private ComboBox _cultureBox;
        private CheckBox _isFemaleBox;
        private TextBox? _ageBox;
        private ComboBox? _defaultGroupBox;
        private TextBox? _skillTemplateDisplay; // Replaces ComboBox

        private Button _saveBtn;
        private Button _exportBtn;
        private Button _addWandererBtn;
        private Button _removeWandererBtn;

        // Nested Tab Controls
        private TabControl? _mainTabControl;
        private TabControl? _wandererDetailTabControl;

        // Project Info Fields
        private TextBox? _projectIdBox;
        private TextBox? _projectNameBox;
        private TextBox? _projectVersionBox;
        private TextBox? _projectUrlBox;

        // Dependency Tracking Fields
        private ListBox? _dependencyList;
        private CheckBox? _autoScanDependencies;
        private ToolTip? _dependencyToolTip;
        private int _lastDependencyIndex = -1;

        // Project File Path Tracking (for save dialog default filename)
        private string? _currentProjectFilePath = null;

        public WandererProject Project { get; private set; }
        public WandererDefinition SelectedWanderer { get; private set; }

        public event Action<WandererDefinition, bool>? OnEditEquipmentRequest; // Legacy
        public event Action<EquipmentTemplate>? OnEditTemplateRequest;
        public event Action<BodyPropertiesTemplate, bool>? OnEditBodyTemplateRequest; // Edit body template (bool = isEditingMax)
        public event Action<BodyPropertiesTemplate>? OnCreateBodyTemplateRequest; // Create new body template with generated properties
        public event Action<WandererProject>? OnSaveRequest;
        public event Action<WandererProject>? OnExportRequest;

        // Dialog Fields
        private TextBox? _txtIntro;
        private TextBox? _txtBackstoryA;
        private TextBox? _txtBackstoryB;
        private TextBox? _txtBackstoryC;
        private TextBox? _txtBackstoryD; // Recruitment
        private TextBox? _txtGeneric;
        private TextBox? _txtResponse1;
        private TextBox? _txtResponse2;
        private TextBox? _costBox;
        private TextBox? _traitTemplateDisplay;
        private TextBox? _bodyTemplateDisplay;

        private RadioButton? _rbBattle;
        private RadioButton? _rbCivilian;

        // Tab pages
        private TabPage? _tabProjectInfo;
        private TabPage? _tabWanderers;
        private TabPage? _tabEquipmentTemplates;
        private TabPage? _tabSkillTemplates;
        private TabPage? _tabTraitTemplates;
        private TabPage? _tabBodyTemplates;

        public CreatorForm()
        {
            InitializeComponent();
            Project = null;
            UpdateControlsState();
        }

        /// <summary>
        /// Generates a sanitized template ID with format: {modname}_{Type}_{Name}
        /// Replaces dots with underscores, applies capitalization (lowercase, then uppercase after each underscore)
        /// </summary>
        private string GenerateTemplateId(string type, string name)
        {
            string moduleId = Project?.ModuleId ?? "mod";
            // Replace dots with underscores
            string sanitized = moduleId.Replace(".", "_");
            // Combine parts
            string raw = $"{sanitized}_{type}_{name}";
            // Apply capitalization: lowercase all, then uppercase after underscores
            var result = new System.Text.StringBuilder();
            bool capitalizeNext = false;
            foreach (char c in raw.ToLowerInvariant())
            {
                if (c == '_') { result.Append(c); capitalizeNext = true; }
                else { result.Append(capitalizeNext ? char.ToUpperInvariant(c) : c); capitalizeNext = false; }
            }
            return result.ToString();
        }


        private void InitializeComponent()
        {
            this.Text = "Wanderer Creator (Bannerlord)";
            this.Size = new Size(1100, 600); // Increased size
            this.TopMost = false; // Ensure it's not always on top

            // Menu
            _menuStrip = new MenuStrip() { Dock = DockStyle.Top };
            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("New Project", null, (s, e) => NewProject()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Open Project", null, (s, e) => OpenProject()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save Project", null, (s, e) => SaveProject()));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Export Mod", null, (s, e) => ExportMod()));
            _menuStrip.Items.Add(fileMenu);

            var helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("Online Guide", null, (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/AxelCawe/BannerlordExpanded.WandererCreator/wiki/Wanderer-Creator-Guide") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open link: " + ex.Message);
                }
            }));
            _menuStrip.Items.Add(helpMenu);
            this.MainMenuStrip = _menuStrip;
            this.Controls.Add(_menuStrip);

            // MAIN TAB CONTROL (Root Layer)
            _mainTabControl = new TabControl() { Dock = DockStyle.Fill, Padding = new Point(10, 5) };

            // Project Info Tab
            _tabProjectInfo = new TabPage("Project Info");
            SetupProjectInfoTab(_tabProjectInfo);
            _mainTabControl.TabPages.Add(_tabProjectInfo);

            // Wanderer Editor Tab
            _tabWanderers = new TabPage("Wanderers");
            SetupWandererEditorTab(_tabWanderers);
            _mainTabControl.TabPages.Add(_tabWanderers);

            // Equipment Templates Tab
            _tabEquipmentTemplates = new TabPage("Equipment Templates");
            SetupSharedTemplatesTab(_tabEquipmentTemplates);
            _mainTabControl.TabPages.Add(_tabEquipmentTemplates);

            // Skill Templates Tab
            _tabSkillTemplates = new TabPage("Skill Templates");
            SetupSharedSkillTemplatesTab(_tabSkillTemplates);
            _mainTabControl.TabPages.Add(_tabSkillTemplates);

            // Trait Templates Tab
            _tabTraitTemplates = new TabPage("Trait Templates");
            SetupSharedTraitTemplatesTab(_tabTraitTemplates);
            _mainTabControl.TabPages.Add(_tabTraitTemplates);

            // Body Templates Tab
            _tabBodyTemplates = new TabPage("Body Templates");
            SetupSharedBodyTemplatesTab(_tabBodyTemplates);
            _mainTabControl.TabPages.Add(_tabBodyTemplates);

            // Note: Bottom panel with Save/Export buttons has been removed.
            // Save and Export are now accessible via File menu.

            // Initialize button fields to avoid null reference issues (they are still used in UpdateControlsState)
            _saveBtn = new Button() { Visible = false };
            _exportBtn = new Button() { Visible = false };

            // Main Panel (Fills remaining space)
            var mainPanel = new Panel() { Dock = DockStyle.Fill };
            mainPanel.Controls.Add(_mainTabControl);
            this.Controls.Add(mainPanel);
            mainPanel.BringToFront();
        }

        private void SetupProjectInfoTab(TabPage tab)
        {
            int labelX = 20;
            int inputX = 150;
            int y = 30;
            int inputWidth = 350;

            // Module ID Field
            var lblId = new Label() { Text = "Module ID:", Left = labelX, Top = y, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            _projectIdBox = new TextBox() { Left = inputX, Top = y, Width = inputWidth };
            _projectIdBox.TextChanged += (s, e) => { if (Project != null) Project.ModuleId = _projectIdBox.Text; };
            var lblIdHint = new Label() { Text = "(Unique identifier, no spaces)", Left = inputX + inputWidth + 10, Top = y, AutoSize = true, ForeColor = Color.Gray };
            tab.Controls.Add(lblId); tab.Controls.Add(_projectIdBox); tab.Controls.Add(lblIdHint);

            y += 50;
            // Project Name Field
            var lblName = new Label() { Text = "Project Name:", Left = labelX, Top = y, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            _projectNameBox = new TextBox() { Left = inputX, Top = y, Width = inputWidth };
            _projectNameBox.TextChanged += (s, e) => { if (Project != null) Project.ProjectName = _projectNameBox.Text; };
            var lblNameHint = new Label() { Text = "(Display name for the mod)", Left = inputX + inputWidth + 10, Top = y, AutoSize = true, ForeColor = Color.Gray };
            tab.Controls.Add(lblName); tab.Controls.Add(_projectNameBox); tab.Controls.Add(lblNameHint);

            y += 50;
            // Version Field
            var lblVersion = new Label() { Text = "Version:", Left = labelX, Top = y, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            _projectVersionBox = new TextBox() { Left = inputX, Top = y, Width = 150 };
            _projectVersionBox.TextChanged += (s, e) => { if (Project != null) Project.Version = _projectVersionBox.Text; };
            var lblVersionHint = new Label() { Text = "(e.g., 1.0.0)", Left = inputX + 160, Top = y, AutoSize = true, ForeColor = Color.Gray };
            tab.Controls.Add(lblVersion); tab.Controls.Add(_projectVersionBox); tab.Controls.Add(lblVersionHint);

            y += 50;
            // URL Field
            var lblUrl = new Label() { Text = "Project URL:", Left = labelX, Top = y, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            _projectUrlBox = new TextBox() { Left = inputX, Top = y, Width = inputWidth };
            _projectUrlBox.TextChanged += (s, e) => { if (Project != null) Project.Url = _projectUrlBox.Text; };
            var lblUrlHint = new Label() { Text = "(Optional, e.g., Nexus Mods page)", Left = inputX + inputWidth + 10, Top = y, AutoSize = true, ForeColor = Color.Gray };
            tab.Controls.Add(lblUrl); tab.Controls.Add(_projectUrlBox); tab.Controls.Add(lblUrlHint);

            y += 60;
            // Dependencies Section
            var lblDependencies = new Label() { Text = "Detected Mod Dependencies:", Left = labelX, Top = y, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            tab.Controls.Add(lblDependencies);

            y += 25;
            _dependencyList = new ListBox() { Left = labelX, Top = y, Width = 400, Height = 120 };
            _dependencyToolTip = new ToolTip() { AutoPopDelay = 10000, InitialDelay = 300 };
            _dependencyList.MouseMove += DependencyList_MouseMove;
            tab.Controls.Add(_dependencyList);

            var btnScanDependencies = new Button() { Text = "Scan Now", Left = labelX + 410, Top = y, Width = 100, Height = 30 };
            btnScanDependencies.Click += (s, e) => ScanDependencies();
            tab.Controls.Add(btnScanDependencies);

            _autoScanDependencies = new CheckBox() { Text = "Auto-scan on Load/Save/Export", Left = labelX + 410, Top = y + 38, AutoSize = true, Checked = true };
            tab.Controls.Add(_autoScanDependencies);

            var lblDepHint = new Label()
            {
                Text = "Dependencies are added to SubModule.xml when exporting.\n\nUse 'Scan Now' to manually detect mods or enable auto-scan.",
                Left = labelX + 410,
                Top = y + 65,
                Width = 300,
                Height = 100,
                ForeColor = Color.Gray
            };
            tab.Controls.Add(lblDepHint);
        }

        private void SetupWandererEditorTab(TabPage tab)
        {
            // SplitContainer: Left = List, Right = Details
            var split = new SplitContainer() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, IsSplitterFixed = false };
            split.Width = 1000; // Prevent clamping of SplitterDistance
            split.SplitterDistance = 400;

            // LEFT: List
            var listPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(5) };

            // Buttons at Bottom
            var btnPanel = new Panel() { Dock = DockStyle.Bottom, Height = 40 };
            _addWandererBtn = new Button() { Text = "Add New", Left = 0, Top = 5, Width = 90 };
            _addWandererBtn.Click += (s, e) => AddWanderer();

            _removeWandererBtn = new Button() { Text = "Delete", Left = 100, Top = 5, Width = 90 };
            _removeWandererBtn.Click += (s, e) => RemoveWanderer();

            btnPanel.Controls.Add(_addWandererBtn);
            btnPanel.Controls.Add(_removeWandererBtn);

            // Label at Top
            var lblList = new Label() { Text = "Wanderers:", Dock = DockStyle.Top, Height = 20, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };

            // List Fills Center
            _wandererList = new ListBox() { Dock = DockStyle.Fill };
            _wandererList.SelectedIndexChanged += (s, e) => SelectWanderer(_wandererList.SelectedItem as WandererDefinition);

            listPanel.Controls.Add(_wandererList);
            listPanel.Controls.Add(lblList);
            listPanel.Controls.Add(btnPanel);


            split.Panel1.Controls.Add(listPanel);

            // RIGHT: Nested Tab Control
            _wandererDetailTabControl = new TabControl() { Dock = DockStyle.Fill };

            // Nested Tab 1: General
            var subTabGeneral = new TabPage("General & Appearance");
            SetupGeneralTab(subTabGeneral);
            _wandererDetailTabControl.TabPages.Add(subTabGeneral);

            // Nested Tab 2: Equipment
            var subTabEquip = new TabPage("Equipments");
            SetupWandererEquipmentTab(subTabEquip);
            _wandererDetailTabControl.TabPages.Add(subTabEquip);

            // Nested Tab 3: Dialogs
            var subTabDialogs = new TabPage("Dialogs");
            SetupDialogsTab(subTabDialogs);
            _wandererDetailTabControl.TabPages.Add(subTabDialogs);

            split.Panel2.Controls.Add(_wandererDetailTabControl);
            tab.Controls.Add(split);
        }


        private void SetupGeneralTab(TabPage tab)
        {
            int labelX = 20;
            int inputX = 150;
            int y = 20;

            // ID Field
            var lblId = new Label() { Text = "ID:", Left = labelX, Top = y, AutoSize = true };
            _idBox = new TextBox() { Left = inputX, Top = y, Width = 250 };
            _idBox.TextChanged += (s, e) => { if (SelectedWanderer != null) SelectedWanderer.Id = _idBox.Text; };
            tab.Controls.Add(lblId); tab.Controls.Add(_idBox);

            y += 40;
            var lblName = new Label() { Text = "Name:", Left = labelX, Top = y, AutoSize = true };
            _nameBox = new TextBox() { Left = inputX, Top = y, Width = 250 };
            _nameBox.TextChanged += (s, e) =>
            {
                if (SelectedWanderer != null)
                {
                    SelectedWanderer.Name = _nameBox.Text;
                    // Force listbox refresh by reassigning DataSource
                    int selectedIndex = _wandererList.SelectedIndex;
                    _wandererList.DataSource = null;
                    _wandererList.DataSource = Project?.Wanderers;
                    _wandererList.DisplayMember = "Name";
                    if (selectedIndex >= 0 && selectedIndex < _wandererList.Items.Count)
                        _wandererList.SelectedIndex = selectedIndex;
                }
            };
            tab.Controls.Add(lblName); tab.Controls.Add(_nameBox);

            y += 40;
            var lblCulture = new Label() { Text = "Culture:", Left = labelX, Top = y, AutoSize = true };
            _cultureBox = new ComboBox() { Left = inputX, Top = y, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            // Dynamic cultures will be populated later via AvailableCultures
            // Fall back to hardcoded list if dynamic is empty
            if (AvailableCultures != null && AvailableCultures.Count > 0)
            {
                _cultureBox.Items.AddRange(AvailableCultures.ToArray());
            }
            else
            {
                _cultureBox.Items.AddRange(new object[] { "empire", "sturgia", "aserai", "vlandia", "battania", "khuzait" });
            }
            _cultureBox.SelectedIndexChanged += (s, e) => { if (SelectedWanderer != null && _cultureBox.SelectedItem != null) SelectedWanderer.Culture = _cultureBox.SelectedItem.ToString(); };
            tab.Controls.Add(lblCulture); tab.Controls.Add(_cultureBox);

            y += 40;
            _isFemaleBox = new CheckBox() { Text = "Is Female", Left = inputX, Top = y, AutoSize = true };
            _isFemaleBox.CheckedChanged += (s, e) => { if (SelectedWanderer != null) SelectedWanderer.IsFemale = _isFemaleBox.Checked; };
            tab.Controls.Add(_isFemaleBox);



            y += 40;
            var lblAge = new Label() { Text = "Age:", Left = labelX, Top = y, AutoSize = true };
            _ageBox = new TextBox() { Left = inputX, Top = y, Width = 100, Text = "18" };
            _ageBox.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
            _ageBox.TextChanged += (s, e) => { if (SelectedWanderer != null && int.TryParse(_ageBox.Text, out int age)) SelectedWanderer.Age = age; };
            tab.Controls.Add(lblAge); tab.Controls.Add(_ageBox);

            y += 40;
            var lblGroup = new Label() { Text = "Default Group:", Left = labelX, Top = y, AutoSize = true };
            _defaultGroupBox = new ComboBox() { Left = inputX, Top = y, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            _defaultGroupBox.Items.AddRange(new object[] { "Infantry", "Ranged", "Cavalry", "HorseArcher" });
            _defaultGroupBox.SelectedIndexChanged += (s, e) => { if (SelectedWanderer != null && _defaultGroupBox.SelectedItem != null) SelectedWanderer.DefaultGroup = _defaultGroupBox.SelectedItem.ToString(); };
            tab.Controls.Add(lblGroup); tab.Controls.Add(_defaultGroupBox);

            y += 40;
            var lblSkill = new Label() { Text = "Skill Template:", Left = labelX, Top = y, AutoSize = true };
            _skillTemplateDisplay = new TextBox() { Left = inputX, Top = y, Width = 180, ReadOnly = true };
            var btnSetSkill = new Button() { Text = "Set...", Left = inputX + 190, Top = y - 2, Width = 60, Height = 25 };
            btnSetSkill.Click += (s, e) => PromptSetSkillTemplate();
            tab.Controls.Add(lblSkill); tab.Controls.Add(_skillTemplateDisplay); tab.Controls.Add(btnSetSkill);

            y += 40;
            var lblTrait = new Label() { Text = "Trait Template:", Left = labelX, Top = y, AutoSize = true };
            _traitTemplateDisplay = new TextBox() { Left = inputX, Top = y, Width = 180, ReadOnly = true };
            var btnSetTrait = new Button() { Text = "Set...", Left = inputX + 190, Top = y - 2, Width = 60, Height = 25 };
            btnSetTrait.Click += (s, e) => { if (SelectedWanderer != null) PromptSetTraitTemplate(); };
            tab.Controls.Add(lblTrait); tab.Controls.Add(_traitTemplateDisplay); tab.Controls.Add(btnSetTrait);

            y += 40;
            var lblBodyTemplate = new Label() { Text = "Body Template:", Left = labelX, Top = y, AutoSize = true };
            _bodyTemplateDisplay = new TextBox() { Left = inputX, Top = y, Width = 180, ReadOnly = true };
            var btnSetBody = new Button() { Text = "Set...", Left = inputX + 190, Top = y - 2, Width = 60, Height = 25 };
            btnSetBody.Click += (s, e) => { if (SelectedWanderer != null) PromptSetBodyTemplate(); };
            tab.Controls.Add(lblBodyTemplate); tab.Controls.Add(_bodyTemplateDisplay); tab.Controls.Add(btnSetBody);
        }

        private void PromptSetSkillTemplate()
        {
            if (Project == null || SelectedWanderer == null) return;
            var f = new Form() { Text = "Set Skill Template", Width = 400, Height = 250, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };

            var rbShared = new RadioButton() { Text = "Use Existing Template", Left = 20, Top = 20, AutoSize = true, Checked = true };
            var cbTemplates = new ComboBox() { Left = 40, Top = 45, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            cbTemplates.DataSource = Project.SharedSkillTemplates;
            cbTemplates.DisplayMember = "Name";

            var rbCustom = new RadioButton() { Text = "Create New Template", Left = 20, Top = 80, AutoSize = true };
            var lblId = new Label() { Text = "New ID:", Left = 40, Top = 105, AutoSize = true };
            var txtId = new TextBox() { Text = GenerateTemplateId("Skill", "New" + ((Project?.SharedSkillTemplates.Count ?? 0) + 1)), Left = 110, Top = 100, Width = 230 };

            var rbClear = new RadioButton() { Text = "Clear (No Template)", Left = 20, Top = 135, AutoSize = true };

            var btnOk = new Button() { Text = "OK", Left = 200, Top = 170, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button() { Text = "Cancel", Left = 290, Top = 170, Width = 80, DialogResult = DialogResult.Cancel };

            f.Controls.Add(rbShared); f.Controls.Add(cbTemplates);
            f.Controls.Add(rbCustom); f.Controls.Add(lblId); f.Controls.Add(txtId);
            f.Controls.Add(rbClear);
            f.Controls.Add(btnOk); f.Controls.Add(btnCancel);

            rbShared.CheckedChanged += (s, e) => { cbTemplates.Enabled = rbShared.Checked; txtId.Enabled = !rbShared.Checked; };
            rbCustom.CheckedChanged += (s, e) => { cbTemplates.Enabled = !rbCustom.Checked; txtId.Enabled = rbCustom.Checked; };
            rbClear.CheckedChanged += (s, e) => { if (rbClear.Checked) { cbTemplates.Enabled = false; txtId.Enabled = false; } };

            cbTemplates.Enabled = true; txtId.Enabled = false;

            if (f.ShowDialog(this) == DialogResult.OK)
            {
                if (rbShared.Checked)
                {
                    if (cbTemplates.SelectedItem is SkillTemplate t)
                        SelectedWanderer.SkillTemplate = t.Id;
                }
                else if (rbCustom.Checked)
                {
                    string rawId = txtId.Text;
                    if (!string.IsNullOrWhiteSpace(rawId))
                    {
                        var newT = new SkillTemplate() { Id = rawId, Name = rawId };
                        Project.SharedSkillTemplates.Add(newT);
                        SelectedWanderer.SkillTemplate = newT.Id;
                        RefreshSkillTemplateList();

                        // Auto-open editor
                        PromptEditSkills(newT);
                    }
                }
                else if (rbClear.Checked)
                {
                    SelectedWanderer.SkillTemplate = "";
                }

                // Update Display
                if (_skillTemplateDisplay != null)
                    _skillTemplateDisplay.Text = SelectedWanderer.SkillTemplate;
            }
        }


        private ListBox? _battleTemplateList;
        private ListBox? _civilianTemplateList;
        private ListBox? _skillTemplateList;
        private ListBox? _traitTemplateList;
        private ListBox? _bodyTemplateList;
        private ListBox? _battleSetsList;
        private ListBox? _civSetsList;

        private void AddTemplate()
        {
            if (Project == null) return;
            var t = new EquipmentTemplate() { Name = "New Template " + (Project.SharedTemplates.Count + 1) };
            Project.SharedTemplates.Add(t);
            RefreshTemplateList();
        }

        private void RemoveTemplate()
        {
            var tmpl = GetSelectedEquipmentTemplate();
            if (tmpl != null && Project != null)
            {
                var result = MessageBox.Show(this, $"Are you sure you want to delete the template '{tmpl.Name}'? This cannot be undone.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;

                // Remove all wanderer references to this template
                foreach (var w in Project.Wanderers)
                {
                    w.CivilianTemplateIds.Remove(tmpl.Id);
                    w.BattleTemplateIds.Remove(tmpl.Id);
                }

                Project.SharedTemplates.Remove(tmpl);
                RefreshTemplateList();
            }
        }

        public void RefreshTemplateList()
        {
            if (Project == null) return;

            // Update Battle templates list
            if (_battleTemplateList != null)
            {
                _battleTemplateList.DataSource = null;
                _battleTemplateList.DataSource = Project.SharedTemplates.Where(t => !t.IsCivilian).ToList();
                _battleTemplateList.DisplayMember = "Name";
            }

            // Update Civilian templates list
            if (_civilianTemplateList != null)
            {
                _civilianTemplateList.DataSource = null;
                _civilianTemplateList.DataSource = Project.SharedTemplates.Where(t => t.IsCivilian).ToList();
                _civilianTemplateList.DisplayMember = "Name";
            }

            RefreshWandererEquipmentUI();
        }

        private void RemoveCivilianSet()
        {
            if (_civSetsList == null) return;
            if (SelectedWanderer != null && _civSetsList.SelectedItem is EquipmentTemplate tmpl)
            {
                SelectedWanderer.CivilianTemplateIds.Remove(tmpl.Id);
                RefreshWandererEquipmentUI();
            }
        }

        private void RemoveBattleSet()
        {
            if (_battleSetsList == null) return;
            if (SelectedWanderer != null && _battleSetsList.SelectedItem is EquipmentTemplate tmpl)
            {
                SelectedWanderer.BattleTemplateIds.Remove(tmpl.Id);
                RefreshWandererEquipmentUI();
            }
        }

        public void RefreshWandererEquipmentUI()
        {
            if (_battleSetsList == null || _civSetsList == null) return;

            if (SelectedWanderer == null || Project == null)
            {
                _battleSetsList.DataSource = null;
                _civSetsList.DataSource = null;
                return;
            }

            // Civ List
            var civList = new List<EquipmentTemplate>();
            foreach (var id in SelectedWanderer.CivilianTemplateIds)
            {
                var t = Project.SharedTemplates.FirstOrDefault(x => x.Id == id);
                if (t != null) civList.Add(t);
            }
            _civSetsList.DataSource = null;
            _civSetsList.DataSource = civList;
            _civSetsList.DisplayMember = "Name";

            // Battle List
            var battleList = new List<EquipmentTemplate>();
            foreach (var id in SelectedWanderer.BattleTemplateIds)
            {
                var t = Project.SharedTemplates.FirstOrDefault(x => x.Id == id);
                if (t != null) battleList.Add(t);
            }
            _battleSetsList.DataSource = null;
            _battleSetsList.DataSource = battleList;
            _battleSetsList.DisplayMember = "Name";
        }

        private void SetupWandererEquipmentTab(TabPage tab)
        {
            // Use TableLayoutPanel for simple vertical stacking of two sections
            var layout = new TableLayoutPanel() { Dock = DockStyle.Fill, RowStyles = { new RowStyle(SizeType.Percent, 50), new RowStyle(SizeType.Percent, 50) } };
            layout.RowCount = 2;
            layout.ColumnCount = 1;

            // Civ Section Panel
            var civPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };
            SetupEquipmentSection(civPanel, true);
            layout.Controls.Add(civPanel, 0, 0);

            // Battle Section Panel
            var battlePanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };
            SetupEquipmentSection(battlePanel, false);
            layout.Controls.Add(battlePanel, 0, 1);

            tab.Controls.Add(layout);
        }

        private void SetupEquipmentSection(Panel p, bool isCivilian)
        {
            // Robust Layout: Table (List | Buttons)
            var layout = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));

            // Left: Label + List
            var leftPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(0) };
            var lbl = new Label() { Text = isCivilian ? "Civilian Outfits:" : "Battle Outfits:", Dock = DockStyle.Top, Height = 20, Font = new Font(this.Font, FontStyle.Bold) };
            var list = new ListBox() { Dock = DockStyle.Fill }; // Fill remaining space in left panel

            // Fix Z-Order/Docking: Label Top, then List Fill.
            leftPanel.Controls.Add(list);
            leftPanel.Controls.Add(lbl);

            if (isCivilian) _civSetsList = list; else _battleSetsList = list;

            // Right: Buttons (Flow or Stack)
            var rightPanel = new FlowLayoutPanel() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(0, 25, 0, 0) };

            var btnAdd = new Button() { Text = "Add New Set", Width = 110, Height = 30, Margin = new Padding(0, 0, 0, 5) };
            btnAdd.Click += (s, e) => PromptAddEquipment(isCivilian);

            var btnRemove = new Button() { Text = "Remove", Width = 110, Height = 30, Margin = new Padding(0, 0, 0, 5) };
            btnRemove.Click += (s, e) => { if (isCivilian) RemoveCivilianSet(); else RemoveBattleSet(); };

            var btnEdit = new Button() { Text = "Edit", Width = 110, Height = 30, Margin = new Padding(0, 0, 0, 5) };
            btnEdit.Click += (s, e) =>
            {
                var l = isCivilian ? _civSetsList : _battleSetsList;
                if (l != null && l.SelectedItem is EquipmentTemplate t) OnEditTemplateRequest?.Invoke(t);
                else MessageBox.Show(this, "Select an outfit first.");
            };

            rightPanel.Controls.Add(btnAdd);
            rightPanel.Controls.Add(btnRemove);
            rightPanel.Controls.Add(btnEdit);

            layout.Controls.Add(leftPanel, 0, 0);
            layout.Controls.Add(rightPanel, 1, 0);

            p.Controls.Add(layout);
        }

        private void SetupSharedTemplatesTab(TabPage tab)
        {
            // Layout: Two horizontal sections (Battle | Civilian), with buttons in the middle
            var mainLayout = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f)); // Battle list
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200f)); // Buttons
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f)); // Civilian list

            // Battle Templates Section (Left)
            var battlePanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var battleLayout = new TableLayoutPanel() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            battleLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            battleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lblBattle = new Label() { Text = "Battle Equipment:", Dock = DockStyle.Fill, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
            _battleTemplateList = new ListBox() { Dock = DockStyle.Fill };
            _battleTemplateList.DisplayMember = "Name";
            _battleTemplateList.SelectedIndexChanged += (s, e) => { if (_battleTemplateList.SelectedItem != null && _civilianTemplateList != null) _civilianTemplateList.ClearSelected(); };

            battleLayout.Controls.Add(lblBattle, 0, 0);
            battleLayout.Controls.Add(_battleTemplateList, 0, 1);
            battlePanel.Controls.Add(battleLayout);

            // Civilian Templates Section (Right)
            var civilianPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var civilianLayout = new TableLayoutPanel() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            civilianLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            civilianLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lblCivilian = new Label() { Text = "Civilian Equipment:", Dock = DockStyle.Fill, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
            _civilianTemplateList = new ListBox() { Dock = DockStyle.Fill };
            _civilianTemplateList.DisplayMember = "Name";
            _civilianTemplateList.SelectedIndexChanged += (s, e) => { if (_civilianTemplateList.SelectedItem != null && _battleTemplateList != null) _battleTemplateList.ClearSelected(); };

            civilianLayout.Controls.Add(lblCivilian, 0, 0);
            civilianLayout.Controls.Add(_civilianTemplateList, 0, 1);
            civilianPanel.Controls.Add(civilianLayout);

            // Center: Buttons
            var buttonPanel = new FlowLayoutPanel() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10, 40, 10, 10) };

            var btnCreateBattle = new Button() { Text = "New Battle Template", Width = 170, Height = 35, Margin = new Padding(0, 0, 0, 5) };
            btnCreateBattle.Click += (s, e) =>
            {
                int count = Project?.SharedTemplates.Count(t => !t.IsCivilian) ?? 0;
                string defaultId = GenerateTemplateId("Equip", "Battle" + (count + 1));
                PromptGenericText("New Battle Template ID", defaultId, (id) =>
                {
                    AddTemplate(id, isCivilian: false);
                    var created = Project?.SharedTemplates.FirstOrDefault(t => t.Id == id);
                    if (created != null) OnEditTemplateRequest?.Invoke(created);
                });
            };

            var btnCreateCivilian = new Button() { Text = "New Civilian Template", Width = 170, Height = 35, Margin = new Padding(0, 0, 0, 15) };
            btnCreateCivilian.Click += (s, e) =>
            {
                int count = Project?.SharedTemplates.Count(t => t.IsCivilian) ?? 0;
                string defaultId = GenerateTemplateId("Equip", "Civ" + (count + 1));
                PromptGenericText("New Civilian Template ID", defaultId, (id) =>
                {
                    AddTemplate(id, isCivilian: true);
                    var created = Project?.SharedTemplates.FirstOrDefault(t => t.Id == id);
                    if (created != null) OnEditTemplateRequest?.Invoke(created);
                });
            };

            var btnDelete = new Button() { Text = "Delete Selected", Width = 170, Height = 35, Margin = new Padding(0, 0, 0, 5) };
            btnDelete.Click += (s, e) => RemoveTemplate();

            var btnRename = new Button() { Text = "Rename Selected", Width = 170, Height = 35, Margin = new Padding(0, 0, 0, 5) };
            btnRename.Click += (s, e) =>
            {
                var selected = GetSelectedEquipmentTemplate();
                if (selected != null)
                {
                    PromptGenericText("Rename Template", selected.Name, (newName) =>
                    {
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            selected.Name = newName;
                            RefreshTemplateList();
                        }
                    });
                }
                else
                {
                    MessageBox.Show(this, "Select a template first.");
                }
            };

            var btnEdit = new Button() { Text = "Edit Selected (In-Game)", Width = 170, Height = 35, Margin = new Padding(0, 0, 0, 5) };
            btnEdit.Click += (s, e) =>
            {
                var selected = GetSelectedEquipmentTemplate();
                if (selected != null) OnEditTemplateRequest?.Invoke(selected);
                else MessageBox.Show(this, "Select a template first.");
            };

            buttonPanel.Controls.Add(btnCreateBattle);
            buttonPanel.Controls.Add(btnCreateCivilian);
            buttonPanel.Controls.Add(btnDelete);
            buttonPanel.Controls.Add(btnRename);
            buttonPanel.Controls.Add(btnEdit);

            mainLayout.Controls.Add(battlePanel, 0, 0);
            mainLayout.Controls.Add(buttonPanel, 1, 0);
            mainLayout.Controls.Add(civilianPanel, 2, 0);

            tab.Controls.Add(mainLayout);
        }

        /// <summary>
        /// Gets the currently selected equipment template from either Battle or Civilian list.
        /// </summary>
        private EquipmentTemplate? GetSelectedEquipmentTemplate()
        {
            if (_battleTemplateList?.SelectedItem is EquipmentTemplate bt) return bt;
            if (_civilianTemplateList?.SelectedItem is EquipmentTemplate ct) return ct;
            return null;
        }

        private void SetupSharedSkillTemplatesTab(TabPage tab)
        {
            var split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel1 };
            split.Width = 1000;
            split.SplitterDistance = 400;

            // Left: List
            var leftPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var tlpLeft = new TableLayoutPanel() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lblTitle = new Label() { Text = "Skill Templates:", Dock = DockStyle.Fill, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
            _skillTemplateList = new ListBox() { Dock = DockStyle.Fill };
            _skillTemplateList.DisplayMember = "Name";

            tlpLeft.Controls.Add(lblTitle, 0, 0);
            tlpLeft.Controls.Add(_skillTemplateList, 0, 1);
            leftPanel.Controls.Add(tlpLeft);

            // Right: Buttons
            var rightPanel = new FlowLayoutPanel() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20) };

            var btnCreate = new Button() { Text = "Create New Template", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnCreate.Click += (s, e) =>
            {
                int count = Project?.SharedSkillTemplates.Count ?? 0;
                string defaultId = GenerateTemplateId("Skill", "New" + (count + 1));
                PromptGenericText("New Skill Template ID", defaultId, (id) =>
                {
                    AddSkillTemplate(id);
                    // Open editor for the newly created skill template
                    var created = Project?.SharedSkillTemplates.FirstOrDefault(t => t.Id == id);
                    if (created != null) PromptEditSkills(created);
                });
            };

            var btnDelete = new Button() { Text = "Delete Template", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnDelete.Click += (s, e) => RemoveSkillTemplate();

            var btnEdit = new Button() { Text = "Edit Skills", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnEdit.Click += (s, e) =>
            {
                if (_skillTemplateList != null && _skillTemplateList.SelectedItem is SkillTemplate t) PromptEditSkills(t);
            };

            var btnRename = new Button() { Text = "Rename Template", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnRename.Click += (s, e) =>
            {
                if (_skillTemplateList?.SelectedItem is SkillTemplate tmpl)
                {
                    PromptGenericText("Rename Template", tmpl.Name, (newName) =>
                    {
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            tmpl.Name = newName;
                            RefreshSkillTemplateList();
                        }
                    });
                }
                else
                {
                    MessageBox.Show(this, "Select a template first.");
                }
            };

            rightPanel.Controls.Add(btnCreate);
            rightPanel.Controls.Add(btnDelete);
            rightPanel.Controls.Add(btnRename);
            rightPanel.Controls.Add(btnEdit);

            split.Panel1.Controls.Add(leftPanel);
            split.Panel2.Controls.Add(rightPanel);
            tab.Controls.Add(split);
        }

        private void AddSkillTemplate(string id)
        {
            if (Project == null) return;
            var t = new SkillTemplate() { Id = id, Name = id };
            Project.SharedSkillTemplates.Add(t);
            RefreshSkillTemplateList();
            // Select the new template
            if (_skillTemplateList != null) _skillTemplateList.SelectedItem = t;
        }

        private void AddTraitTemplate(string id)
        {
            if (Project == null) return;
            var t = new TraitTemplate() { Id = id, Name = id };
            Project.SharedTraitTemplates.Add(t);
            RefreshTraitTemplateList();
            if (_traitTemplateList != null)
            {
                _traitTemplateList.SelectedItem = t;
                PromptEditTraitTemplate(t);
            }
        }

        private void RemoveSkillTemplate()
        {
            if (_skillTemplateList != null && _skillTemplateList.SelectedItem is SkillTemplate t && Project != null)
            {
                // Remove all wanderer references to this template
                foreach (var w in Project.Wanderers)
                {
                    if (w.SkillTemplate == t.Id) w.SkillTemplate = "";
                }

                Project.SharedSkillTemplates.Remove(t);
                RefreshSkillTemplateList();
            }
        }

        private void RefreshSkillTemplateList()
        {
            if (_skillTemplateList == null) return;
            _skillTemplateList.DataSource = null;
            if (Project != null)
            {
                _skillTemplateList.DataSource = Project.SharedSkillTemplates;
                _skillTemplateList.DisplayMember = "Name";
            }
        }

        #region Dynamic Game Data Properties

        private List<string> _availableSkills = new List<string>();
        public List<string> AvailableSkills
        {
            get => _availableSkills;
            set => _availableSkills = value ?? new List<string>();
        }

        private List<string> _availableTraits = new List<string>();
        public List<string> AvailableTraits
        {
            get => _availableTraits;
            set => _availableTraits = value ?? new List<string>();
        }

        private List<string> _availableCultures = new List<string>();
        public List<string> AvailableCultures
        {
            get => _availableCultures;
            set
            {
                _availableCultures = value ?? new List<string>();
                // Repopulate culture combobox when cultures are set
                if (_cultureBox != null && _availableCultures.Count > 0)
                {
                    _cultureBox.Items.Clear();
                    _cultureBox.Items.AddRange(_availableCultures.ToArray());
                }
            }
        }

        #endregion


        private void PromptEditSkills(SkillTemplate template)
        {
            var f = new Form() { Text = "Edit Skills: " + template.Name, Width = 500, Height = 600, StartPosition = FormStartPosition.CenterParent };

            var grid = new DataGridView() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

            grid.Columns.Clear();

            // Dropdown for Skills
            var colId = new DataGridViewComboBoxColumn() { Name = "SkillId", HeaderText = "Skill ID", Width = 250 };

            if (AvailableSkills != null && AvailableSkills.Count > 0)
            {
                colId.Items.AddRange(AvailableSkills.ToArray());
            }
            else
            {
                // Fallback
                MessageBox.Show(this, "Warning: Could not fetch skill list from the game.\nUsing hardcoded fallback list. Please let the mod author know about this.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                colId.Items.AddRange(new object[] {
                    "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing",
                    "Riding", "Athletics", "Smithing",
                    "Scouting", "Tactics", "Roguary", "Charm", "Leadership", "Trade", "Steward", "Medicine", "Engineering"
                });
            }

            colId.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            colId.FlatStyle = FlatStyle.Flat;

            var colVal = new DataGridViewTextBoxColumn() { Name = "Level", HeaderText = "Level (1-300)", Width = 100 };
            grid.Columns.Add(colId); grid.Columns.Add(colVal);

            foreach (var kv in template.Skills)
            {
                grid.Rows.Add(kv.Key, kv.Value);
            }

            var pnlBottom = new Panel() { Dock = DockStyle.Bottom, Height = 50 };
            var btnSave = new Button() { Text = "Save", Left = 380, Top = 10, DialogResult = DialogResult.OK };
            pnlBottom.Controls.Add(btnSave);
            f.AcceptButton = btnSave;

            f.Controls.Add(grid);
            f.Controls.Add(pnlBottom);

            if (f.ShowDialog(this) == DialogResult.OK)
            {
                template.Skills.Clear();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    string id = row.Cells[0].Value?.ToString() ?? "";
                    string valStr = row.Cells[1].Value?.ToString() ?? "0";
                    if (!string.IsNullOrWhiteSpace(id) && int.TryParse(valStr, out int val))
                    {
                        template.Skills[id] = val;
                    }
                }
            }
        }

        private void SetupSharedTraitTemplatesTab(TabPage tab)
        {
            var split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel1 };
            split.Width = 1000;
            split.SplitterDistance = 400;

            // Left: List
            var leftPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var tlpLeft = new TableLayoutPanel() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lblTitle = new Label() { Text = "Trait Templates:", Dock = DockStyle.Fill, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
            _traitTemplateList = new ListBox() { Dock = DockStyle.Fill, DisplayMember = "Name" };

            tlpLeft.Controls.Add(lblTitle, 0, 0);
            tlpLeft.Controls.Add(_traitTemplateList, 0, 1);
            leftPanel.Controls.Add(tlpLeft);

            // Right: Buttons
            var rightPanel = new FlowLayoutPanel() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20) };

            var btnCreate = new Button() { Text = "Create New Template", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnCreate.Click += (s, e) =>
            {
                int count = Project?.SharedTraitTemplates.Count ?? 0;
                string defaultId = GenerateTemplateId("Trait", "New" + (count + 1));
                PromptGenericText("New Trait Template ID", defaultId, (id) => AddTraitTemplate(id));
            };

            var btnDelete = new Button() { Text = "Delete Template", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnDelete.Click += (s, e) =>
            {
                if (_traitTemplateList != null && _traitTemplateList.SelectedItem is TraitTemplate t && Project != null)
                {
                    // Remove all wanderer references to this template
                    foreach (var w in Project.Wanderers)
                    {
                        if (w.TraitTemplate == t.Id) w.TraitTemplate = "";
                    }

                    Project.SharedTraitTemplates.Remove(t);
                    ((CurrencyManager)BindingContext[Project.SharedTraitTemplates]).Refresh();
                    RefreshTraitTemplateList();
                }
            };

            var btnEdit = new Button() { Text = "Edit Traits", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnEdit.Click += (s, e) =>
            {
                if (_traitTemplateList != null && _traitTemplateList.SelectedItem is TraitTemplate t) PromptEditTraitTemplate(t);
            };

            var btnRename = new Button() { Text = "Rename Template", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnRename.Click += (s, e) =>
            {
                if (_traitTemplateList?.SelectedItem is TraitTemplate tmpl)
                {
                    PromptGenericText("Rename Template", tmpl.Name, (newName) =>
                    {
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            tmpl.Name = newName;
                            RefreshTraitTemplateList();
                        }
                    });
                }
                else
                {
                    MessageBox.Show(this, "Select a template first.");
                }
            };

            rightPanel.Controls.Add(btnCreate);
            rightPanel.Controls.Add(btnDelete);
            rightPanel.Controls.Add(btnRename);
            rightPanel.Controls.Add(btnEdit);

            split.Panel1.Controls.Add(leftPanel);
            split.Panel2.Controls.Add(rightPanel);
            tab.Controls.Add(split);
        }

        private void SetupSharedBodyTemplatesTab(TabPage tab)
        {
            var split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel1 };
            split.Width = 1000;
            split.SplitterDistance = 300;

            // Left: List
            var leftPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var tlpLeft = new TableLayoutPanel() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lblTitle = new Label() { Text = "Body Templates:", Dock = DockStyle.Fill, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
            _bodyTemplateList = new ListBox() { Dock = DockStyle.Fill, DisplayMember = "DisplayName" };
            _bodyTemplateList.SelectedIndexChanged += (s, e) => UpdateBodyTemplateDetails();

            tlpLeft.Controls.Add(lblTitle, 0, 0);
            tlpLeft.Controls.Add(_bodyTemplateList, 0, 1);
            leftPanel.Controls.Add(tlpLeft);

            // Right: Details panel
            var rightPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
            int y = 10;
            int labelX = 10;
            int inputX = 130;

            // Create/Delete buttons
            var btnCreate = new Button() { Text = "Create New", Left = labelX, Top = y, Width = 100, Height = 30 };
            btnCreate.Click += (s, e) => PromptCreateBodyTemplate();
            rightPanel.Controls.Add(btnCreate);

            var btnDelete = new Button() { Text = "Delete", Left = labelX + 110, Top = y, Width = 80, Height = 30 };
            btnDelete.Click += (s, e) => RemoveBodyTemplate();
            rightPanel.Controls.Add(btnDelete);

            var btnRename = new Button() { Text = "Rename", Left = labelX + 200, Top = y, Width = 80, Height = 30 };
            btnRename.Click += (s, e) =>
            {
                if (_bodyTemplateList?.SelectedItem is BodyPropertiesTemplate tmpl)
                {
                    PromptGenericText("Rename Template", tmpl.Name, (newName) =>
                    {
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            tmpl.Name = newName;
                            RefreshBodyTemplateList();
                        }
                    });
                }
                else
                {
                    MessageBox.Show(this, "Select a template first.");
                }
            };
            rightPanel.Controls.Add(btnRename);

            y += 45;

            // IsFemale checkbox
            var lblFemale = new Label() { Text = "Is Female:", Left = labelX, Top = y + 3, AutoSize = true };
            _bodyTemplateIsFemaleCheckbox = new CheckBox() { Left = inputX, Top = y, AutoSize = true };
            _bodyTemplateIsFemaleCheckbox.CheckedChanged += (s, e) =>
            {
                if (_bodyTemplateList?.SelectedItem is BodyPropertiesTemplate t)
                {
                    t.IsFemale = _bodyTemplateIsFemaleCheckbox.Checked;
                }
            };
            rightPanel.Controls.Add(lblFemale);
            rightPanel.Controls.Add(_bodyTemplateIsFemaleCheckbox);

            y += 35;

            // Body Properties Min
            var lblMin = new Label() { Text = "Body Min:", Left = labelX, Top = y, AutoSize = true };
            _bodyTemplateMinBox = new TextBox() { Left = inputX, Top = y, Width = 300, Height = 60, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
            var btnEditMin = new Button() { Text = "Edit", Left = inputX + 310, Top = y, Width = 60, Height = 30 };
            btnEditMin.Click += (s, e) =>
            {
                if (_bodyTemplateList?.SelectedItem is BodyPropertiesTemplate t)
                    OnEditBodyTemplateRequest?.Invoke(t, false);
                else
                    MessageBox.Show(this, "Select a template first.");
            };
            rightPanel.Controls.Add(lblMin);
            rightPanel.Controls.Add(_bodyTemplateMinBox);
            rightPanel.Controls.Add(btnEditMin);

            y += 70;

            // Body Properties Max
            var lblMax = new Label() { Text = "Body Max:", Left = labelX, Top = y, AutoSize = true };
            _bodyTemplateMaxBox = new TextBox() { Left = inputX, Top = y, Width = 300, Height = 60, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };
            var btnEditMax = new Button() { Text = "Edit", Left = inputX + 310, Top = y, Width = 60, Height = 30 };
            btnEditMax.Click += (s, e) =>
            {
                if (_bodyTemplateList?.SelectedItem is BodyPropertiesTemplate t)
                    OnEditBodyTemplateRequest?.Invoke(t, true);
                else
                    MessageBox.Show(this, "Select a template first.");
            };
            rightPanel.Controls.Add(lblMax);
            rightPanel.Controls.Add(_bodyTemplateMaxBox);
            rightPanel.Controls.Add(btnEditMax);

            split.Panel1.Controls.Add(leftPanel);
            split.Panel2.Controls.Add(rightPanel);
            tab.Controls.Add(split);
        }

        private void UpdateBodyTemplateDetails()
        {
            if (_bodyTemplateList?.SelectedItem is BodyPropertiesTemplate t)
            {
                if (_bodyTemplateIsFemaleCheckbox != null) _bodyTemplateIsFemaleCheckbox.Checked = t.IsFemale;
                if (_bodyTemplateMinBox != null) _bodyTemplateMinBox.Text = t.BodyPropertiesString;
                if (_bodyTemplateMaxBox != null) _bodyTemplateMaxBox.Text = t.BodyPropertiesMaxString;
            }
            else
            {
                if (_bodyTemplateIsFemaleCheckbox != null) _bodyTemplateIsFemaleCheckbox.Checked = false;
                if (_bodyTemplateMinBox != null) _bodyTemplateMinBox.Text = "";
                if (_bodyTemplateMaxBox != null) _bodyTemplateMaxBox.Text = "";
            }
        }

        // Body template detail fields
        private CheckBox? _bodyTemplateIsFemaleCheckbox;
        private TextBox? _bodyTemplateMinBox;
        private TextBox? _bodyTemplateMaxBox;


        private void AddBodyTemplate(string id, bool isFemale)
        {
            if (Project == null) return;
            var t = new BodyPropertiesTemplate() { Id = id, Name = id, IsFemale = isFemale };
            Project.SharedBodyPropertiesTemplates.Add(t);
            RefreshBodyTemplateList();
            // Select the new template
            if (_bodyTemplateList != null) _bodyTemplateList.SelectedItem = t;
            // Request body properties generation from EditorController
            OnCreateBodyTemplateRequest?.Invoke(t);
        }

        private void PromptCreateBodyTemplate()
        {
            var f = new Form() { Text = "Create Body Template", Width = 350, Height = 180, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };

            var lblId = new Label() { Text = "Template ID:", Left = 20, Top = 20, AutoSize = true };
            int count = Project?.SharedBodyPropertiesTemplates.Count ?? 0;
            var txtId = new TextBox() { Text = GenerateTemplateId("Body", "New" + (count + 1)), Left = 120, Top = 17, Width = 200 };

            var lblGender = new Label() { Text = "Gender:", Left = 20, Top = 55, AutoSize = true };
            var cbFemale = new CheckBox() { Text = "Female", Left = 120, Top = 52, AutoSize = true };

            var btnOk = new Button() { Text = "OK", Left = 140, Top = 95, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button() { Text = "Cancel", Left = 230, Top = 95, Width = 80, DialogResult = DialogResult.Cancel };

            f.Controls.Add(lblId); f.Controls.Add(txtId);
            f.Controls.Add(lblGender); f.Controls.Add(cbFemale);
            f.Controls.Add(btnOk); f.Controls.Add(btnCancel);

            if (f.ShowDialog(this) == DialogResult.OK)
            {
                if (!string.IsNullOrWhiteSpace(txtId.Text))
                {
                    AddBodyTemplate(txtId.Text, cbFemale.Checked);
                }
            }
        }

        private void RemoveBodyTemplate()
        {
            if (_bodyTemplateList != null && _bodyTemplateList.SelectedItem is BodyPropertiesTemplate t && Project != null)
            {
                // Remove all wanderer references to this template
                foreach (var w in Project.Wanderers)
                {
                    if (w.BodyPropertiesTemplateId == t.Id) w.BodyPropertiesTemplateId = "";
                }

                Project.SharedBodyPropertiesTemplates.Remove(t);
                RefreshBodyTemplateList();
            }
        }

        public void RefreshBodyTemplateList()
        {
            if (_bodyTemplateList == null) return;

            // Preserve current selection if possible
            var currentSelection = _bodyTemplateList.SelectedItem as BodyPropertiesTemplate;
            string currentId = currentSelection?.Id;

            _bodyTemplateList.DataSource = null;
            if (Project != null)
            {
                _bodyTemplateList.DataSource = Project.SharedBodyPropertiesTemplates;
                _bodyTemplateList.DisplayMember = "Name";

                // Re-select logic
                if (currentId != null)
                {
                    var reSelect = Project.SharedBodyPropertiesTemplates.FirstOrDefault(t => t.Id == currentId);
                    if (reSelect != null)
                    {
                        _bodyTemplateList.SelectedItem = reSelect;
                    }
                }
            }
            UpdateBodyTemplateDetails();
        }

        private void PromptSetBodyTemplate()
        {
            if (Project == null || SelectedWanderer == null) return;
            var f = new Form() { Text = "Set Body Template", Width = 400, Height = 250, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };

            var rbShared = new RadioButton() { Text = "Use Existing Template", Left = 20, Top = 20, AutoSize = true, Checked = true };
            var cbTemplates = new ComboBox() { Left = 40, Top = 45, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            // Filter templates by wanderer's gender
            var filteredTemplates = Project.SharedBodyPropertiesTemplates
                .Where(t => t.IsFemale == SelectedWanderer.IsFemale)
                .ToList();
            cbTemplates.DataSource = filteredTemplates;
            cbTemplates.DisplayMember = "Name";

            var rbCustom = new RadioButton() { Text = "Create New Template", Left = 20, Top = 80, AutoSize = true };
            var lblId = new Label() { Text = "New ID:", Left = 40, Top = 105, AutoSize = true };
            var txtId = new TextBox() { Text = GenerateTemplateId("Body", "New" + ((Project?.SharedBodyPropertiesTemplates.Count ?? 0) + 1)), Left = 110, Top = 100, Width = 230 };

            var rbClear = new RadioButton() { Text = "Clear (No Template)", Left = 20, Top = 135, AutoSize = true };

            var btnOk = new Button() { Text = "OK", Left = 200, Top = 165, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button() { Text = "Cancel", Left = 290, Top = 165, Width = 80, DialogResult = DialogResult.Cancel };

            f.Controls.Add(rbShared); f.Controls.Add(cbTemplates);
            f.Controls.Add(rbCustom); f.Controls.Add(lblId); f.Controls.Add(txtId);
            f.Controls.Add(rbClear);
            f.Controls.Add(btnOk); f.Controls.Add(btnCancel);

            rbShared.CheckedChanged += (s, e) => { cbTemplates.Enabled = rbShared.Checked; txtId.Enabled = false; };
            rbCustom.CheckedChanged += (s, e) => { cbTemplates.Enabled = false; txtId.Enabled = rbCustom.Checked; };
            rbClear.CheckedChanged += (s, e) => { cbTemplates.Enabled = false; txtId.Enabled = false; };

            cbTemplates.Enabled = true; txtId.Enabled = false;

            if (f.ShowDialog(this) == DialogResult.OK)
            {
                if (rbShared.Checked)
                {
                    if (cbTemplates.SelectedItem is BodyPropertiesTemplate t)
                    {
                        SelectedWanderer.BodyPropertiesTemplateId = t.Id;
                    }
                }
                else if (rbCustom.Checked)
                {
                    string rawId = txtId.Text;
                    if (!string.IsNullOrWhiteSpace(rawId))
                    {
                        // Use centralized function to create template (generates body properties)
                        AddBodyTemplate(rawId, SelectedWanderer.IsFemale);
                        SelectedWanderer.BodyPropertiesTemplateId = rawId;

                        // Switch to Body Templates tab and select the new template
                        if (_tabBodyTemplates != null)
                        {
                            _mainTabControl.SelectedTab = _tabBodyTemplates;
                        }
                        // Refresh and select the new template in the list by ID
                        RefreshBodyTemplateList();
                        if (_bodyTemplateList != null)
                        {
                            var createdTemplate = Project.SharedBodyPropertiesTemplates.FirstOrDefault(t => t.Id == rawId);
                            if (createdTemplate != null) _bodyTemplateList.SelectedItem = createdTemplate;
                        }
                    }
                }
                else if (rbClear.Checked)
                {
                    SelectedWanderer.BodyPropertiesTemplateId = "";
                }

                UpdateBodyTemplateDisplay();
            }
        }

        private void UpdateBodyTemplateDisplay()
        {
            if (SelectedWanderer == null || _bodyTemplateDisplay == null) return;

            if (!string.IsNullOrEmpty(SelectedWanderer.BodyPropertiesTemplateId))
            {
                var template = Project?.SharedBodyPropertiesTemplates.FirstOrDefault(t => t.Id == SelectedWanderer.BodyPropertiesTemplateId);
                _bodyTemplateDisplay.Text = template?.Name ?? SelectedWanderer.BodyPropertiesTemplateId;
            }
            else
            {
                _bodyTemplateDisplay.Text = "(None)";
            }
        }

        private void PromptSetTraitTemplate()
        {
            if (Project == null || SelectedWanderer == null) return;
            var f = new Form() { Text = "Set Trait Template", Width = 400, Height = 250, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };

            var rbShared = new RadioButton() { Text = "Use Existing Template", Left = 20, Top = 20, AutoSize = true, Checked = true };
            var cbTemplates = new ComboBox() { Left = 40, Top = 45, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            cbTemplates.DataSource = Project.SharedTraitTemplates;
            cbTemplates.DisplayMember = "Name";

            var rbCustom = new RadioButton() { Text = "Create New Template", Left = 20, Top = 80, AutoSize = true };
            var lblId = new Label() { Text = "New ID:", Left = 40, Top = 105, AutoSize = true };
            var txtId = new TextBox() { Text = GenerateTemplateId("Trait", "New" + ((Project?.SharedTraitTemplates.Count ?? 0) + 1)), Left = 110, Top = 100, Width = 230 };

            var rbClear = new RadioButton() { Text = "Clear (No Template)", Left = 20, Top = 135, AutoSize = true };

            var btnOk = new Button() { Text = "OK", Left = 200, Top = 170, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button() { Text = "Cancel", Left = 290, Top = 170, Width = 80, DialogResult = DialogResult.Cancel };

            f.Controls.Add(rbShared); f.Controls.Add(cbTemplates);
            f.Controls.Add(rbCustom); f.Controls.Add(lblId); f.Controls.Add(txtId);
            f.Controls.Add(rbClear);
            f.Controls.Add(btnOk); f.Controls.Add(btnCancel);
            f.AcceptButton = btnOk; f.CancelButton = btnCancel;

            if (f.ShowDialog(this) == DialogResult.OK)
            {
                if (rbShared.Checked)
                {
                    if (cbTemplates.SelectedItem is TraitTemplate t)
                        SelectedWanderer.TraitTemplate = t.Id;
                }
                else if (rbCustom.Checked)
                {
                    var result = txtId.Text.Trim();
                    if (!string.IsNullOrEmpty(result))
                    {
                        var t = new TraitTemplate() { Id = result, Name = result };
                        Project.SharedTraitTemplates.Add(t);
                        SelectedWanderer.TraitTemplate = t.Id;

                        RefreshTraitTemplateList();
                        PromptEditTraitTemplate(t);
                    }
                }
                else if (rbClear.Checked)
                {
                    SelectedWanderer.TraitTemplate = "";
                }
                // Update text display
                UpdateTraitTemplateDisplay();
            }
        }
        private void UpdateTraitTemplateDisplay()
        {
            if (SelectedWanderer == null || Project == null)
            {
                if (_traitTemplateDisplay != null) _traitTemplateDisplay.Text = "";
                return;
            }
            if (string.IsNullOrEmpty(SelectedWanderer.TraitTemplate))
            {
                if (_traitTemplateDisplay != null) _traitTemplateDisplay.Text = "(None)";
            }
            else
            {
                var t = Project.SharedTraitTemplates.FirstOrDefault(x => x.Id == SelectedWanderer.TraitTemplate);
                if (_traitTemplateDisplay != null)
                    _traitTemplateDisplay.Text = t != null ? t.Name : SelectedWanderer.TraitTemplate;
            }
        }

        private void PromptEditTraitTemplate(TraitTemplate template)
        {
            var f = new Form() { Text = "Edit Template: " + template.Name, Width = 400, Height = 500, StartPosition = FormStartPosition.CenterParent };
            var grid = new DataGridView() { Dock = DockStyle.Fill, AutoGenerateColumns = false };

            var colId = new DataGridViewComboBoxColumn() { Name = "TraitId", HeaderText = "Trait ID", Width = 200 };
            if (AvailableTraits != null && AvailableTraits.Count > 0)
                colId.Items.AddRange(AvailableTraits.ToArray());
            else
                colId.Items.AddRange(new object[] { "Mercy", "Valor", "Honor", "Generosity", "Calculating" });

            colId.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;
            colId.FlatStyle = FlatStyle.Flat;

            var colVal = new DataGridViewTextBoxColumn() { Name = "Value", HeaderText = "Value", Width = 100 };
            grid.Columns.Add(colId); grid.Columns.Add(colVal);

            foreach (var kv in template.Traits)
            {
                grid.Rows.Add(kv.Key, kv.Value);
            }

            var pnlBottom = new Panel() { Dock = DockStyle.Bottom, Height = 50 };
            var btnSave = new Button() { Text = "Save", Left = 280, Top = 10, DialogResult = DialogResult.OK };
            pnlBottom.Controls.Add(btnSave);
            f.AcceptButton = btnSave;
            f.Controls.Add(grid);
            f.Controls.Add(pnlBottom);

            if (f.ShowDialog(this) == DialogResult.OK)
            {
                template.Traits.Clear();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    string id = row.Cells[0].Value?.ToString();
                    string valStr = row.Cells[1].Value?.ToString();
                    if (!string.IsNullOrEmpty(id) && int.TryParse(valStr, out int val))
                    {
                        template.Traits[id] = val;
                    }
                }
            }
        }

        private void SetupDialogsTab(TabPage tab)
        {
            tab.AutoScroll = true; // Enable scrolling for many fields
            int x = 20;
            int y = 20;

            // Helper
            TextBox AddField(string label, string desc, int height, Action<string> onSet)
            {
                var lbl = new Label() { Text = label, Left = x, Top = y, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
                var lblDesc = new Label() { Text = desc, Left = x + 250, Top = y, AutoSize = true, ForeColor = Color.Gray };
                var box = new TextBox() { Left = x, Top = y + 25, Width = 600, Height = height, Multiline = height > 25, ScrollBars = height > 25 ? ScrollBars.Vertical : ScrollBars.None };
                box.TextChanged += (s, e) => { if (SelectedWanderer != null) onSet(box.Text); };
                tab.Controls.Add(lbl); tab.Controls.Add(lblDesc); tab.Controls.Add(box);
                y += height + 40;
                return box;
            }

            _txtIntro = AddField("Intro (PreBackstory):", "First Hello", 60, (v) => SelectedWanderer.Dialogs.Intro = v);
            _txtBackstoryA = AddField("Backstory A (Life Story):", "Main story part", 100, (v) => SelectedWanderer.Dialogs.LifeStory = v);
            _txtBackstoryB = AddField("Backstory B:", "Continuation", 60, (v) => SelectedWanderer.Dialogs.LifeStoryB = v);
            _txtBackstoryC = AddField("Backstory C:", "Continuation", 60, (v) => SelectedWanderer.Dialogs.LifeStoryC = v);

            _txtResponse1 = AddField("Response 1:", "Player Response Option 1", 40, (v) => SelectedWanderer.Dialogs.Response1 = v);
            _txtResponse2 = AddField("Response 2:", "Player Response Option 2", 40, (v) => SelectedWanderer.Dialogs.Response2 = v);

            _txtBackstoryD = AddField("Backstory D (Recruitment):", "The proposal", 60, (v) => SelectedWanderer.Dialogs.Recruitment = v);
            _txtGeneric = AddField("Generic Backstory:", "Rumor / Tavern talk", 60, (v) => SelectedWanderer.Dialogs.GenericBackstory = v);

            var lblCost = new Label() { Text = "Recruitment Cost:", Left = x, Top = y, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), Visible = false };
            _costBox = new TextBox() { Left = x, Top = y + 25, Width = 150, Visible = false };
            _costBox.TextChanged += (s, e) => { if (SelectedWanderer != null) SelectedWanderer.Dialogs.Cost = _costBox.Text; };
            // Note: Cost field is hidden as per plan. UI element kept for data binding but not displayed.
            // tab.Controls.Add(lblCost); tab.Controls.Add(_costBox);
        }

        // ... (Existing Methods: AddTemplate, PromptGenericText, PromptAddEquipment) ...
        // Need to ensure I include them in this Full Write

        // Overloaded AddTemplate to accept ID
        private void AddTemplate(string id, bool isCivilian = false)
        {
            if (Project == null || string.IsNullOrWhiteSpace(id)) return;
            var t = new EquipmentTemplate() { Id = id, Name = id, IsCivilian = isCivilian };
            Project.SharedTemplates.Add(t);
            RefreshTemplateList();
        }

        private void PromptGenericText(string title, string defaultVal, Action<string> onOk)
        {
            var f = new Form() { Text = title, Width = 400, Height = 150, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            var lbl = new Label() { Text = "Value:", Left = 10, Top = 10 };
            var box = new TextBox() { Text = defaultVal, Left = 10, Top = 30, Width = 360 };
            var btnOk = new Button() { Text = "OK", Left = 200, Top = 70, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button() { Text = "Cancel", Left = 290, Top = 70, Width = 80, DialogResult = DialogResult.Cancel };
            f.Controls.Add(lbl); f.Controls.Add(box); f.Controls.Add(btnOk); f.Controls.Add(btnCancel);
            f.AcceptButton = btnOk; f.CancelButton = btnCancel;
            if (f.ShowDialog(this) == DialogResult.OK) onOk(box.Text);
        }

        private void PromptAddEquipment(bool isCivilian)
        {
            if (Project == null || SelectedWanderer == null) return;
            var f = new Form() { Text = "Add Equipment Set", Width = 400, Height = 250, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            var rbShared = new RadioButton() { Text = "Use Existing Template", Left = 20, Top = 20, AutoSize = true, Checked = true };
            var cbTemplates = new ComboBox() { Left = 40, Top = 45, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };

            // Filter templates: must match isCivilian flag AND must not already be added to this wanderer
            var alreadyAddedIds = isCivilian
                ? SelectedWanderer.CivilianTemplateIds
                : SelectedWanderer.BattleTemplateIds;
            var availableTemplates = Project.SharedTemplates
                .Where(t => t.IsCivilian == isCivilian && !alreadyAddedIds.Contains(t.Id))
                .ToList();
            cbTemplates.DataSource = availableTemplates;
            cbTemplates.DisplayMember = "Name";
            var rbCustom = new RadioButton() { Text = "Create New Set", Left = 20, Top = 80, AutoSize = true };
            var lblId = new Label() { Text = "Custom ID:", Left = 40, Top = 105, AutoSize = true };
            var suffix = isCivilian ? "Civ" : "Battle";
            int eqCount = isCivilian
                ? (Project?.SharedTemplates.Count(t => t.IsCivilian) ?? 0)
                : (Project?.SharedTemplates.Count(t => !t.IsCivilian) ?? 0);
            var txtId = new TextBox() { Text = GenerateTemplateId("Equip", suffix + (eqCount + 1)), Left = 110, Top = 100, Width = 230 };
            var btnOk = new Button() { Text = "Add", Left = 200, Top = 160, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button() { Text = "Cancel", Left = 290, Top = 160, Width = 80, DialogResult = DialogResult.Cancel };
            f.Controls.Add(rbShared); f.Controls.Add(cbTemplates);
            f.Controls.Add(rbCustom); f.Controls.Add(lblId); f.Controls.Add(txtId);
            f.Controls.Add(btnOk); f.Controls.Add(btnCancel);
            rbShared.CheckedChanged += (s, e) => { cbTemplates.Enabled = rbShared.Checked; txtId.Enabled = !rbShared.Checked; };
            cbTemplates.Enabled = true; txtId.Enabled = false;
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                if (rbShared.Checked)
                {
                    if (cbTemplates.SelectedItem is EquipmentTemplate t)
                    {
                        if (isCivilian && !SelectedWanderer.CivilianTemplateIds.Contains(t.Id)) SelectedWanderer.CivilianTemplateIds.Add(t.Id);
                        if (!isCivilian && !SelectedWanderer.BattleTemplateIds.Contains(t.Id)) SelectedWanderer.BattleTemplateIds.Add(t.Id);
                    }
                }
                else
                {
                    string rawId = txtId.Text;
                    if (!string.IsNullOrWhiteSpace(rawId))
                    {
                        var newT = new EquipmentTemplate() { Id = rawId, Name = rawId + " (" + (isCivilian ? "Civ" : "Battle") + ")", IsCivilian = isCivilian };
                        Project.SharedTemplates.Add(newT);
                        if (isCivilian) SelectedWanderer.CivilianTemplateIds.Add(newT.Id);
                        else SelectedWanderer.BattleTemplateIds.Add(newT.Id);

                        // Auto-open the inventory editor for the newly created template
                        RefreshWandererEquipmentUI();
                        OnEditTemplateRequest?.Invoke(newT);
                        return; // Exit early since we already refreshed
                    }
                }
                RefreshWandererEquipmentUI();
            }
        }

        // STANDARD METHODS
        private void NewProject()
        {
            if (Project != null)
            {
                var result = MessageBox.Show(this, "You have a project open. Creating a new project will discard any unsaved changes. Continue?", "Confirm New Project", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }
            Project = new WandererProject();
            _currentProjectFilePath = null; // New project has no file path
            UpdateControlsState();
            RefreshList();
        }
        private string GetProjectDirectory() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord", "WandererCreatorProjects"); }
        private void OpenProject()
        {
            if (Project != null)
            {
                var result = MessageBox.Show(this, "You have a project open. Opening another project will discard any unsaved changes. Continue?", "Confirm Open Project", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = GetProjectDirectory();
                openFileDialog.Filter = "Wanderer Project (*.wcproj)|*.wcproj";
                if (openFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string json = File.ReadAllText(openFileDialog.FileName);
                        Project = JsonConvert.DeserializeObject<WandererProject>(json);

                        // Validate and cleanup invalid data
                        var cleanupMessages = ValidateAndCleanupProject();
                        if (cleanupMessages.Count > 0)
                        {
                            string message = "The following data was cleaned up because it no longer exists in the game:\n\n" +
                                string.Join("\n", cleanupMessages.Take(10));
                            if (cleanupMessages.Count > 10)
                                message += $"\n\n... and {cleanupMessages.Count - 10} more items.";
                            MessageBox.Show(this, message, "Project Cleanup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }

                        UpdateControlsState();
                        RefreshList();

                        // Store the file path for use in SaveProject
                        _currentProjectFilePath = openFileDialog.FileName;

                        // Auto-scan dependencies on load if toggle is checked
                        if (_autoScanDependencies?.Checked == true && Project != null)
                        {
                            try
                            {
                                DependencyTracker.UpdateProjectDependencies(Project);
                                RefreshDependencyList();
                            }
                            catch { /* Silently continue if scan fails */ }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Error loading project: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Validates project data against current game state and removes invalid references.
        /// Returns a list of cleanup messages for user notification.
        /// </summary>
        private List<string> ValidateAndCleanupProject()
        {
            var messages = new List<string>();
            if (Project == null) return messages;

            // Validate wanderer cultures
            if (AvailableCultures != null && AvailableCultures.Count > 0)
            {
                foreach (var wanderer in Project.Wanderers)
                {
                    if (!string.IsNullOrEmpty(wanderer.Culture) &&
                        !AvailableCultures.Contains(wanderer.Culture, StringComparer.OrdinalIgnoreCase))
                    {
                        string defaultCulture = AvailableCultures.FirstOrDefault() ?? "empire";
                        messages.Add($"Wanderer '{wanderer.Name}': Culture '{wanderer.Culture}' → '{defaultCulture}'");
                        wanderer.Culture = defaultCulture;
                    }
                }
            }

            // Validate equipment template items
            foreach (var template in Project.SharedTemplates)
            {
                var invalidItems = new List<string>();
                foreach (var kvp in template.Items.ToList())
                {
                    if (!IsValidItemId(kvp.Value))
                    {
                        invalidItems.Add(kvp.Key);
                        messages.Add($"Template '{template.Name}': Removed invalid item '{kvp.Value}' from slot {kvp.Key}");
                    }
                }
                foreach (var slot in invalidItems)
                {
                    template.Items.Remove(slot);
                }
            }

            // Validate wanderer equipment template references
            foreach (var wanderer in Project.Wanderers)
            {
                // Check civilian template IDs
                var invalidCivIds = wanderer.CivilianTemplateIds
                    .Where(id => !Project.SharedTemplates.Any(t => t.Id == id))
                    .ToList();
                foreach (var id in invalidCivIds)
                {
                    wanderer.CivilianTemplateIds.Remove(id);
                    messages.Add($"Wanderer '{wanderer.Name}': Removed missing civilian template '{id}'");
                }

                // Check battle template IDs
                var invalidBattleIds = wanderer.BattleTemplateIds
                    .Where(id => !Project.SharedTemplates.Any(t => t.Id == id))
                    .ToList();
                foreach (var id in invalidBattleIds)
                {
                    wanderer.BattleTemplateIds.Remove(id);
                    messages.Add($"Wanderer '{wanderer.Name}': Removed missing battle template '{id}'");
                }
            }

            return messages;
        }

        /// <summary>
        /// Checks if an item ID exists in the game.
        /// </summary>
        private bool IsValidItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            try
            {
                var item = TaleWorlds.Core.Game.Current?.ObjectManager?.GetObject<TaleWorlds.Core.ItemObject>(itemId);
                return item != null;
            }
            catch
            {
                return false;
            }
        }
        private void SaveProject()
        {
            if (Project == null) return;

            // Auto-scan dependencies if toggle is checked
            if (_autoScanDependencies?.Checked == true)
            {
                try
                {
                    DependencyTracker.UpdateProjectDependencies(Project);
                    RefreshDependencyList();
                }
                catch { /* Silently continue if scan fails */ }
            }

            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.InitialDirectory = GetProjectDirectory();
                saveFileDialog.Filter = "Wanderer Project (*.wcproj)|*.wcproj";

                // Pre-populate with the current file path if available (file was previously opened/saved)
                if (!string.IsNullOrEmpty(_currentProjectFilePath))
                {
                    saveFileDialog.FileName = Path.GetFileName(_currentProjectFilePath);
                    saveFileDialog.InitialDirectory = Path.GetDirectoryName(_currentProjectFilePath) ?? GetProjectDirectory();
                }

                if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string json = JsonConvert.SerializeObject(Project, Formatting.Indented);
                        File.WriteAllText(saveFileDialog.FileName, json);

                        // Update the current file path to the saved location
                        _currentProjectFilePath = saveFileDialog.FileName;

                        MessageBox.Show(this, "Project Saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        OnSaveRequest?.Invoke(Project);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Error saving project: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportMod()
        {
            if (Project == null) return;

            // Auto-scan dependencies if toggle is checked
            if (_autoScanDependencies?.Checked == true)
            {
                try
                {
                    DependencyTracker.UpdateProjectDependencies(Project);
                    RefreshDependencyList();
                }
                catch { /* Silently continue if scan fails */ }
            }

            OnExportRequest?.Invoke(Project);
        }

        private void UpdateControlsState()
        {
            bool hasProject = Project != null;
            _addWandererBtn.Enabled = hasProject;
            _removeWandererBtn.Enabled = hasProject;
            _wandererList.Enabled = hasProject;
            if (_mainTabControl != null) _mainTabControl.Visible = hasProject; // Hide whole UI if no project?
            _saveBtn.Enabled = hasProject;
            _exportBtn.Enabled = hasProject;
            if (!hasProject) { ResetInputs(); _wandererList.DataSource = null; }
        }

        private void AddWanderer()
        {
            if (Project == null) return;
            int count = Project.Wanderers.Count;
            string id = GenerateTemplateId("Wanderer", "New" + (count + 1));
            var w = new WandererDefinition() { Id = id, Name = "{FIRSTNAME} the new Wanderer" };

            // Fix: Default to the first available culture if possible
            if (AvailableCultures != null && AvailableCultures.Count > 0)
            {
                w.Culture = AvailableCultures[0];
            }

            Project.Wanderers.Add(w);
            RefreshList();
            SelectWanderer(w);
        }
        private void RemoveWanderer()
        {
            if (SelectedWanderer == null || Project == null) return;
            var result = MessageBox.Show(this, $"Are you sure you want to delete the wanderer '{SelectedWanderer.Name}'? This cannot be undone.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;
            Project.Wanderers.Remove(SelectedWanderer);
            SelectedWanderer = null;
            RefreshList();
            ResetInputs();
        }
        private void RefreshList()
        {
            _wandererList.DataSource = null;
            RefreshTemplateList();
            RefreshSkillTemplateList();
            RefreshTraitTemplateList();
            RefreshBodyTemplateList();
            RefreshProjectInfo();
            if (Project != null) { _wandererList.DataSource = Project.Wanderers; _wandererList.DisplayMember = "Name"; }
        }
        private void RefreshTraitTemplateList()
        {
            if (_traitTemplateList == null) return;
            _traitTemplateList.DataSource = null;
            if (Project != null) { _traitTemplateList.DataSource = Project.SharedTraitTemplates; _traitTemplateList.DisplayMember = "Name"; }
        }
        private void RefreshProjectInfo()
        {
            if (Project == null)
            {
                if (_projectIdBox != null) _projectIdBox.Text = "";
                if (_projectNameBox != null) _projectNameBox.Text = "";
                if (_projectVersionBox != null) _projectVersionBox.Text = "";
                if (_projectUrlBox != null) _projectUrlBox.Text = "";
                RefreshDependencyList();
                return;
            }
            if (_projectIdBox != null) _projectIdBox.Text = Project.ModuleId;
            if (_projectNameBox != null) _projectNameBox.Text = Project.ProjectName;
            if (_projectVersionBox != null) _projectVersionBox.Text = Project.Version;
            if (_projectUrlBox != null) _projectUrlBox.Text = Project.Url;
            RefreshDependencyList();
        }

        private void ScanDependencies()
        {
            if (Project == null)
            {
                MessageBox.Show(this, "No project loaded.", "Scan Dependencies", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                DependencyTracker.UpdateProjectDependencies(Project);
                RefreshDependencyList();

                if (Project.DetectedDependencies.Count == 0)
                {
                    MessageBox.Show(this, "No third-party mod dependencies detected.\n\nAll equipment items appear to be from core game modules.", "Scan Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, $"Found {Project.DetectedDependencies.Count} mod dependencies.\n\nThese will be added to SubModule.xml when exporting.", "Scan Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error scanning dependencies:\n{ex.Message}", "Scan Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshDependencyList()
        {
            if (_dependencyList == null) return;
            _dependencyList.Items.Clear();

            if (Project?.DetectedDependencies != null)
            {
                foreach (var dep in Project.DetectedDependencies)
                {
                    _dependencyList.Items.Add($"{dep.ModuleName} ({dep.ModuleId}) - {dep.UsedItems.Count} items");
                }
            }
        }

        private void DependencyList_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_dependencyList == null || _dependencyToolTip == null || Project?.DetectedDependencies == null) return;

            int index = _dependencyList.IndexFromPoint(e.Location);
            if (index != _lastDependencyIndex)
            {
                _lastDependencyIndex = index;
                if (index >= 0 && index < Project.DetectedDependencies.Count)
                {
                    var dep = Project.DetectedDependencies[index];
                    string tooltipText = $"Module: {dep.ModuleName}\nID: {dep.ModuleId}\n\nUsed Items:\n" +
                        string.Join("\n", dep.UsedItems.Select(item => $"  • {item}"));
                    _dependencyToolTip.SetToolTip(_dependencyList, tooltipText);
                }
                else
                {
                    _dependencyToolTip.SetToolTip(_dependencyList, "");
                }
            }
        }
        private void ResetInputs()
        {
            if (_nameBox != null) _nameBox.Text = "";
            if (_idBox != null) _idBox.Text = "";
            if (_cultureBox != null) _cultureBox.SelectedIndex = -1;
            if (_isFemaleBox != null) _isFemaleBox.Checked = false;

            if (_ageBox != null) _ageBox.Text = "18";
            if (_defaultGroupBox != null) _defaultGroupBox.SelectedIndex = -1;
            if (_skillTemplateDisplay != null) _skillTemplateDisplay.Text = "";
            if (_bodyTemplateDisplay != null) _bodyTemplateDisplay.Text = "";
            if (_txtIntro != null) _txtIntro.Text = "";

            SetWandererControlsEnabled(false);
        }

        private void SetWandererControlsEnabled(bool enabled)
        {
            if (_nameBox != null) _nameBox.Enabled = enabled;
            if (_idBox != null) _idBox.Enabled = enabled;
            if (_cultureBox != null) _cultureBox.Enabled = enabled;
            if (_isFemaleBox != null) _isFemaleBox.Enabled = enabled;
            if (_ageBox != null) _ageBox.Enabled = enabled;
            if (_defaultGroupBox != null) _defaultGroupBox.Enabled = enabled;
            if (_skillTemplateDisplay != null) _skillTemplateDisplay.Enabled = enabled;
            if (_traitTemplateDisplay != null) _traitTemplateDisplay.Enabled = enabled;
            if (_bodyTemplateDisplay != null) _bodyTemplateDisplay.Enabled = enabled;

            if (_txtIntro != null) _txtIntro.Enabled = enabled;
            if (_txtBackstoryA != null) _txtBackstoryA.Enabled = enabled;
            if (_txtBackstoryB != null) _txtBackstoryB.Enabled = enabled;
            if (_txtBackstoryC != null) _txtBackstoryC.Enabled = enabled;
            if (_txtBackstoryD != null) _txtBackstoryD.Enabled = enabled;
            if (_txtGeneric != null) _txtGeneric.Enabled = enabled;
            if (_txtResponse1 != null) _txtResponse1.Enabled = enabled;
            if (_txtResponse2 != null) _txtResponse2.Enabled = enabled;
        }

        public void SelectWanderer(WandererDefinition? w)
        {
            SelectedWanderer = w;
            if (w == null) { ResetInputs(); return; }

            SetWandererControlsEnabled(true);

            if (_nameBox != null) _nameBox.Text = w.Name;
            if (_idBox != null) _idBox.Text = w.Id;
            if (_cultureBox != null) _cultureBox.SelectedItem = w.Culture;
            if (_isFemaleBox != null) _isFemaleBox.Checked = w.IsFemale;

            if (_ageBox != null) _ageBox.Text = w.Age.ToString();
            if (_defaultGroupBox != null) _defaultGroupBox.SelectedItem = w.DefaultGroup;

            if (_skillTemplateDisplay != null)
            {
                if (string.IsNullOrEmpty(w.SkillTemplate))
                    _skillTemplateDisplay.Text = "(None)";
                else
                {
                    var t = Project?.SharedSkillTemplates.FirstOrDefault(x => x.Id == w.SkillTemplate);
                    _skillTemplateDisplay.Text = t != null ? t.Name : w.SkillTemplate;
                }
            }

            UpdateBodyTemplateDisplay();

            if (_txtIntro != null) _txtIntro.Text = w.Dialogs.Intro;
            if (_txtBackstoryA != null) _txtBackstoryA.Text = w.Dialogs.LifeStory;
            if (_txtBackstoryB != null) _txtBackstoryB.Text = w.Dialogs.LifeStoryB;
            if (_txtBackstoryC != null) _txtBackstoryC.Text = w.Dialogs.LifeStoryC;
            if (_txtBackstoryD != null) _txtBackstoryD.Text = w.Dialogs.Recruitment;
            if (_txtGeneric != null) _txtGeneric.Text = w.Dialogs.GenericBackstory;
            if (_txtResponse1 != null) _txtResponse1.Text = w.Dialogs.Response1;
            if (_txtResponse2 != null) _txtResponse2.Text = w.Dialogs.Response2;
            if (_costBox != null) _costBox.Text = w.Dialogs.Cost;

            RefreshWandererEquipmentUI();
            UpdateTraitTemplateDisplay();
        }
    }
}

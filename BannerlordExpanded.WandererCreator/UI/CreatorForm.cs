using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using BannerlordExpanded.WandererCreator.Models;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace BannerlordExpanded.WandererCreator.UI
{
    public class CreatorForm : Form
    {
        private MenuStrip _menuStrip;
        private ListBox _wandererList;
        private TextBox _nameBox;
        private ComboBox _cultureBox;
        private CheckBox _isFemaleBox;
        private Button _editAppearanceBtn;
        private Button _saveBtn;
        private Button _exportBtn;
        private Button _addWandererBtn;
        private Button _removeWandererBtn;

        // Nested Tab Controls
        private TabControl? _mainTabControl;
        private TabControl? _wandererDetailTabControl;

        public WandererProject Project { get; private set; }
        public WandererDefinition SelectedWanderer { get; private set; }

        public event Action<WandererDefinition>? OnEditAppearanceRequest;
        public event Action<WandererDefinition, bool>? OnEditEquipmentRequest; // Legacy
        public event Action<EquipmentTemplate>? OnEditTemplateRequest;
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

        private RadioButton? _rbBattle;
        private RadioButton? _rbCivilian;

        public CreatorForm()
        {
            InitializeComponent();
            Project = null;
            UpdateControlsState();
        }

        private void InitializeComponent()
        {
            this.Text = "Wanderer Creator (Bannerlord)";
            this.Size = new Size(1100, 800); // Increased size

            // Menu
            _menuStrip = new MenuStrip() { Dock = DockStyle.Top };
            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("New Project", null, (s, e) => NewProject()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Open Project", null, (s, e) => OpenProject()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save Project", null, (s, e) => SaveProject()));
            _menuStrip.Items.Add(fileMenu);
            this.MainMenuStrip = _menuStrip;
            this.Controls.Add(_menuStrip);

            // MAIN TAB CONTROL (Root Layer)
            _mainTabControl = new TabControl() { Dock = DockStyle.Fill, Padding = new Point(10, 5) };

            // 1. WANDERER EDITOR TAB
            var tabWandererEditor = new TabPage("Wanderer Editor");
            SetupWandererEditorTab(tabWandererEditor);
            _mainTabControl.TabPages.Add(tabWandererEditor);

            // 2. SHARED LIBRARY TAB
            var tabShared = new TabPage("Shared Equipment Templates");
            SetupSharedTemplatesTab(tabShared);
            _mainTabControl.TabPages.Add(tabShared);

            // Panel for Main Tabs to separate from Menu and Bottom Buttons
            // Bottom Actions (Added first so it docks to Bottom correctly)
            var bottomPanel = new Panel() { Height = 50, Dock = DockStyle.Bottom };
            _saveBtn = new Button() { Text = "Save Project", Left = 10, Top = 10, Width = 100 };
            _saveBtn.Click += (s, e) => SaveProject();

            _exportBtn = new Button() { Text = "Export Mod", Left = 120, Top = 10, Width = 100 };
            _exportBtn.Click += (s, e) => { if (Project != null) OnExportRequest?.Invoke(Project); };

            bottomPanel.Controls.Add(_saveBtn);
            bottomPanel.Controls.Add(_exportBtn);
            this.Controls.Add(bottomPanel);

            // Main Panel (Fills remaining space)
            var mainPanel = new Panel() { Dock = DockStyle.Fill };
            mainPanel.Controls.Add(_mainTabControl);
            this.Controls.Add(mainPanel);
            mainPanel.BringToFront();
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

            // Add to Panel (Order Correctness for Docking: Buttons first (Bottom), then Label (Top), then List (Fill))
            // actually Dock priority: first added takes precedence for Top/Bottom/Left/Right? 
            // Standard: Add Dock.Fill LAST.
            // Add Dock.Bottom (Buttons).
            // Add Dock.Top (Label).
            // Add Dock.Fill (List).
            listPanel.Controls.Add(_wandererList);
            listPanel.Controls.Add(lblList);
            listPanel.Controls.Add(btnPanel);
            // wait, if I add List (Fill) last, it is Index 0. It docks Last. Fills remaining. Correct.
            // But Buttons (Bottom) added first (Index 2). Docks First?
            // "Controls at the bottom of the Z-order are docked first."
            // Index N (Bottom of Z) = First Added.
            // So First Added -> Docks First.
            // 1. Add(ButtonPanel) -> Index N. Docks Bottom.
            // 2. Add(Label) -> Index N-1. Docks Top (of remaining).
            // 3. Add(List) -> Index 0. Docks Fill (of remaining).
            // PERFECT.

            // Correction in code block below:
            // I need to reverse the Add calls in my implementation plan above? 
            // Code:
            // listPanel.Controls.Add(btnPanel); // Added first. Index high. Docks First (Bottom).
            // listPanel.Controls.Add(lblList); // Docks Top.
            // listPanel.Controls.Add(_wandererList); // Docks Fill.

            // Wait, `Controls.Add` ADDS TO INDEX 0.
            // So `btnPanel` becomes Index 0.
            // Then `lblList` becomes Index 0. `btnPanel` becomes Index 1.
            // Then `_wandererList` becomes Index 0.
            // So Z-Order: List(0), Label(1), Btn(2).
            // Docking Order (Bottom of Z to Top): Btn(2), Label(1), List(0).
            // Btn(2) [Bottom] -> Takes bottom slice.
            // Label(1) [Top] -> Takes top slice of remaining.
            // List(0) [Fill] -> Takes center.
            // CORRECT. 
            // So valid order is: Add(Panel), Add(Label), Add(List).

            // DO NOT change the ReplacementContent logic. 
            // The ReplacementContent below:
            // listPanel.Controls.Add(btnPanel);
            // listPanel.Controls.Add(lblList);
            // listPanel.Controls.Add(_wandererList);
            // This is correct.


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
            int y = 40;

            var lblName = new Label() { Text = "Name:", Left = labelX, Top = y, AutoSize = true };
            _nameBox = new TextBox() { Left = inputX, Top = y, Width = 250 };
            _nameBox.TextChanged += (s, e) => { if (SelectedWanderer != null) { SelectedWanderer.Name = _nameBox.Text; _wandererList.Refresh(); } };
            tab.Controls.Add(lblName); tab.Controls.Add(_nameBox);

            y += 40;
            var lblCulture = new Label() { Text = "Culture:", Left = labelX, Top = y, AutoSize = true };
            _cultureBox = new ComboBox() { Left = inputX, Top = y, Width = 250 };
            _cultureBox.Items.AddRange(new object[] { "Empire", "Sturgia", "Aserai", "Vlandia", "Battania", "Khuzait" });
            _cultureBox.SelectedIndexChanged += (s, e) => { if (SelectedWanderer != null && _cultureBox.SelectedItem != null) SelectedWanderer.Culture = _cultureBox.SelectedItem.ToString(); };
            tab.Controls.Add(lblCulture); tab.Controls.Add(_cultureBox);

            y += 40;
            _isFemaleBox = new CheckBox() { Text = "Is Female", Left = inputX, Top = y, AutoSize = true };
            _isFemaleBox.CheckedChanged += (s, e) => { if (SelectedWanderer != null) SelectedWanderer.IsFemale = _isFemaleBox.Checked; };
            tab.Controls.Add(_isFemaleBox);

            y += 80;
            _editAppearanceBtn = new Button() { Text = "Edit Appearance (In-Game)", Left = inputX, Top = y, Width = 250, Height = 50 };
            _editAppearanceBtn.Click += (s, e) => { if (SelectedWanderer != null) OnEditAppearanceRequest?.Invoke(SelectedWanderer); };
            tab.Controls.Add(_editAppearanceBtn);
        }

        private ListBox? _templateList;
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
            if (_templateList == null) return;
            var tmpl = _templateList.SelectedItem as EquipmentTemplate;
            if (tmpl != null && Project != null)
            {
                Project.SharedTemplates.Remove(tmpl);
                RefreshTemplateList();
            }
        }

        public void RefreshTemplateList()
        {
            if (_templateList == null) return;
            _templateList.DataSource = null;
            if (Project != null)
            {
                _templateList.DataSource = Project.SharedTemplates;
                _templateList.DisplayMember = "Name";
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

            var btnAdd = new Button() { Text = "Add...", Width = 110, Height = 30, Margin = new Padding(0, 0, 0, 5) };
            btnAdd.Click += (s, e) => PromptAddEquipment(isCivilian);

            var btnRemove = new Button() { Text = "Remove", Width = 110, Height = 30, Margin = new Padding(0, 0, 0, 5) };
            btnRemove.Click += (s, e) => { if (isCivilian) RemoveCivilianSet(); else RemoveBattleSet(); };

            var btnEdit = new Button() { Text = "Edit", Width = 110, Height = 30, Margin = new Padding(0, 0, 0, 5) };
            btnEdit.Click += (s, e) =>
            {
                var l = isCivilian ? _civSetsList : _battleSetsList;
                if (l != null && l.SelectedItem is EquipmentTemplate t) OnEditTemplateRequest?.Invoke(t);
                else MessageBox.Show("Select an outfit first.");
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
            // Layout: List on Left (Fixed Width), Actions on Right
            var split = new SplitContainer() { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel1 };
            split.Width = 1000;
            split.SplitterDistance = 400;

            // Left: List
            var leftPanel = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(10) };

            // Use Table to prevent overlap
            var tlpLeft = new TableLayoutPanel() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var lblTitle = new Label() { Text = "Project Library:", Dock = DockStyle.Fill, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
            _templateList = new ListBox() { Dock = DockStyle.Fill };

            tlpLeft.Controls.Add(lblTitle, 0, 0);
            tlpLeft.Controls.Add(_templateList, 0, 1);

            leftPanel.Controls.Add(tlpLeft);

            // Right: Buttons
            var rightPanel = new FlowLayoutPanel() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20) };

            var btnCreate = new Button() { Text = "Create New Template", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnCreate.Click += (s, e) => PromptGenericText("New Template ID", "equipment_template_" + Guid.NewGuid().ToString("N").Substring(0, 8), (id) => AddTemplate(id));

            var btnDelete = new Button() { Text = "Delete Template", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnDelete.Click += (s, e) => RemoveTemplate();

            var btnEdit = new Button() { Text = "Edit Selected (In-Game)", Width = 180, Height = 40, Margin = new Padding(0, 0, 0, 10) };
            btnEdit.Click += (s, e) =>
            {
                if (_templateList == null) return;
                var tmpl = _templateList.SelectedItem as EquipmentTemplate;
                if (tmpl != null) OnEditTemplateRequest?.Invoke(tmpl);
            };

            rightPanel.Controls.Add(btnCreate);
            rightPanel.Controls.Add(btnDelete);
            rightPanel.Controls.Add(btnEdit);

            split.Panel1.Controls.Add(leftPanel);
            split.Panel2.Controls.Add(rightPanel);

            tab.Controls.Add(split);
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
            _txtBackstoryD = AddField("Backstory D (Recruitment):", "The proposal", 60, (v) => SelectedWanderer.Dialogs.Recruitment = v);

            _txtGeneric = AddField("Generic Backstory:", "Rumor / Tavern talk", 60, (v) => SelectedWanderer.Dialogs.GenericBackstory = v);

            _txtResponse1 = AddField("Response 1:", "Player: 'Tell me more'", 40, (v) => SelectedWanderer.Dialogs.Response1 = v);
            _txtResponse2 = AddField("Response 2:", "Player: 'Not interested'", 40, (v) => SelectedWanderer.Dialogs.Response2 = v);

            var lblCost = new Label() { Text = "Recruitment Cost:", Left = x, Top = y, AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            _costBox = new TextBox() { Left = x, Top = y + 25, Width = 150 };
            _costBox.TextChanged += (s, e) => { if (SelectedWanderer != null) SelectedWanderer.Dialogs.Cost = _costBox.Text; };
            tab.Controls.Add(lblCost); tab.Controls.Add(_costBox);
        }

        // ... (Existing Methods: AddTemplate, PromptGenericText, PromptAddEquipment) ...
        // Need to ensure I include them in this Full Write

        // Overloaded AddTemplate to accept ID
        private void AddTemplate(string id)
        {
            if (Project == null || string.IsNullOrWhiteSpace(id)) return;
            var t = new EquipmentTemplate() { Id = id, Name = id, IsCivilian = false };
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
            if (f.ShowDialog() == DialogResult.OK) onOk(box.Text);
        }

        private void PromptAddEquipment(bool isCivilian)
        {
            if (Project == null || SelectedWanderer == null) return;
            var f = new Form() { Text = "Add Equipment Set", Width = 400, Height = 250, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };
            var rbShared = new RadioButton() { Text = "Use Shared Template", Left = 20, Top = 20, AutoSize = true, Checked = true };
            var cbTemplates = new ComboBox() { Left = 40, Top = 45, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
            cbTemplates.DataSource = Project.SharedTemplates;
            cbTemplates.DisplayMember = "Name";
            var rbCustom = new RadioButton() { Text = "Create Custom Set", Left = 20, Top = 80, AutoSize = true };
            var lblId = new Label() { Text = "Custom ID:", Left = 40, Top = 105, AutoSize = true };
            var suffix = isCivilian ? "civ" : "battle";
            var txtId = new TextBox() { Text = $"eq_{suffix}_custom_{SelectedWanderer.Id}_{Guid.NewGuid().ToString("N").Substring(0, 4)}", Left = 110, Top = 100, Width = 230 };
            var btnOk = new Button() { Text = "Add", Left = 200, Top = 160, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button() { Text = "Cancel", Left = 290, Top = 160, Width = 80, DialogResult = DialogResult.Cancel };
            f.Controls.Add(rbShared); f.Controls.Add(cbTemplates);
            f.Controls.Add(rbCustom); f.Controls.Add(lblId); f.Controls.Add(txtId);
            f.Controls.Add(btnOk); f.Controls.Add(btnCancel);
            rbShared.CheckedChanged += (s, e) => { cbTemplates.Enabled = rbShared.Checked; txtId.Enabled = !rbShared.Checked; };
            cbTemplates.Enabled = true; txtId.Enabled = false;
            if (f.ShowDialog() == DialogResult.OK)
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
                    }
                }
                RefreshWandererEquipmentUI();
            }
        }

        // STANDARD METHODS
        private void NewProject() { Project = new WandererProject(); UpdateControlsState(); RefreshList(); }
        private string GetProjectDirectory() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mount and Blade II Bannerlord", "WandererCreatorProjects"); }
        private void OpenProject() { /* ... */ using (var openFileDialog = new OpenFileDialog()) { openFileDialog.InitialDirectory = GetProjectDirectory(); openFileDialog.Filter = "Wanderer Project (*.json)|*.json"; if (openFileDialog.ShowDialog() == DialogResult.OK) { try { string json = File.ReadAllText(openFileDialog.FileName); Project = JsonConvert.DeserializeObject<WandererProject>(json); UpdateControlsState(); RefreshList(); } catch (Exception ex) { MessageBox.Show("Error " + ex.Message); } } } }
        private void SaveProject() { /* ... */ if (Project == null) return; using (var saveFileDialog = new SaveFileDialog()) { saveFileDialog.InitialDirectory = GetProjectDirectory(); saveFileDialog.Filter = "Wanderer Project (*.json)|*.json"; if (saveFileDialog.ShowDialog() == DialogResult.OK) { try { string json = JsonConvert.SerializeObject(Project, Formatting.Indented); File.WriteAllText(saveFileDialog.FileName, json); MessageBox.Show("Project Saved."); OnSaveRequest?.Invoke(Project); } catch (Exception ex) { MessageBox.Show("Error " + ex.Message); } } } }

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

        private void AddWanderer() { if (Project == null) return; var w = new WandererDefinition() { Name = "New Wanderer" }; Project.Wanderers.Add(w); RefreshList(); SelectWanderer(w); }
        private void RemoveWanderer() { if (SelectedWanderer != null && Project != null) { Project.Wanderers.Remove(SelectedWanderer); SelectedWanderer = null; RefreshList(); ResetInputs(); } }
        private void RefreshList() { _wandererList.DataSource = null; if (Project != null) { _wandererList.DataSource = Project.Wanderers; _wandererList.DisplayMember = "Name"; } }
        private void ResetInputs() { if (_nameBox != null) _nameBox.Text = ""; if (_cultureBox != null) _cultureBox.SelectedIndex = -1; if (_isFemaleBox != null) _isFemaleBox.Checked = false; if (_txtIntro != null) _txtIntro.Text = ""; }

        public void SelectWanderer(WandererDefinition? w)
        {
            SelectedWanderer = w;
            if (w == null) { ResetInputs(); return; }
            if (_nameBox != null) _nameBox.Text = w.Name;
            if (_cultureBox != null) _cultureBox.SelectedItem = w.Culture;
            if (_isFemaleBox != null) _isFemaleBox.Checked = w.IsFemale;

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
        }
    }
}

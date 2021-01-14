using System;
using System.Collections.Generic;
using System.Linq;
using UtilityBelt.Lib;
using System.Text.RegularExpressions;
using Decal.Adapter.Wrappers;
using Decal.Adapter;
using System.Runtime.InteropServices;
using UBLoader.Lib.Settings;
using VirindiViewService.Controls;
using System.Collections.ObjectModel;
using System.IO;
using UtilityBelt.Lib.Actions;
using UtilityBelt.Views;
using Newtonsoft.Json;
using System.ComponentModel;

namespace UtilityBelt.Tools {
    public class UBAlias {
        [JsonIgnore]
        public Regex AliasRegex { get; private set; }

        private string _alias = "";

        public string Alias {
            get => _alias;
            set {
                _alias = value;
                AliasRegex = new Regex(_alias, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
        }
        public BaseAction Action { get; set; }
        public bool Eat { get; set; }

        public UBAlias() {

        }

        public UBAlias(string alias, BaseAction action, bool eat) {
            Alias = alias;
            Action = action;
            Eat = eat;
        }

        public void Run(string args) {
            Action.Run();
        }
    }

    [Name("Aliases")]
    [Summary("Allows you to define aliases that run actions when text is typed into the chatbox that matches the specified regular expression.")]
    [FullDescription(@"
Allows you to define aliases that run actions when text is typed into the chatbox that matches the specified regular expression. These function in similar fashion to how VTank meta chat captures function.  You can capture named groups in your regular expressions to expression variables. Captured regex groups will be set to expression variables named `capturegroup_<name>`, see examples below.

Multiple aliases can match the same input text, so you can add an alias that runs multiple actions.  Aliases cannot override commands that are defined by plugins.

### Examples

#### Add an alias command `/ubpd` that shortcuts to `/ub propertydump`
* **Alias:** `^/ubpd$`
* **Action:** ChatCommand
* **Command:** `/ub propertydump`

#### Add an alias command `/lsr` that casts lifestone recall
* **Alias:** `^/lsr$`
* **Action:** Expression
* **Expression:** `actiontrycastbyid[1635]`

#### Add an `/tloc <name>` command that sends a tell to `<name>` with your current location
* **Alias:** `^/tloc (?<name>.*)$`
* **Action:** ChatExpression
* **Expression:** `\/tell +getvar[capturegroup_name]+\, I am at: +getplayercoordinates[]`

#### Replace $LOC in typed chat with your current location
* **Alias:** `^(?<start>.*)(?<loc>\$LOC)(?<end>.*)$`
* **Action:** ChatExpression
* **Expression:** `$capturegroup_start + getplayercoordinates[] + $capturegroup_end`
    ")]
    public class Aliases : ToolBase {

        /// <summary>
        /// The default character aliases profile path
        /// </summary>
        public string CharacterAliasesFile { get => Path.Combine(Util.GetCharacterDirectory(), AliasesProfileExtension); }

        /// <summary>
        /// The file path to the currently loaded aliases profile
        /// </summary>
        public string AliasesProfilePath {
            get {
                if (Profile == "[character]")
                    return CharacterAliasesFile;
                else
                    return Path.Combine(Util.GetProfilesDirectory(), $"{Profile}.{AliasesProfileExtension}");
            }
        }

        public static readonly string AliasesProfileExtension = "aliases.json";

        #region Config
        [Summary("Enabled")]
        public Setting<bool> Enabled = new Setting<bool>(true);

        [Summary("Aliases profile. Set to `[character]` to use a private set of aliases for only this character.")]
        public readonly CharacterState<string> Profile = new CharacterState<string>("[character]");

        [Summary("Defined aliases")]
        public readonly Alias<ObservableCollection<UBAlias>> DefinedAliases = new Alias<ObservableCollection<UBAlias>>(new ObservableCollection<UBAlias>());
        #endregion // Config

        HudList UIAliasesList;
        HudTextBox UIAliasAlias;
        HudButton UIAliasUpdate;
        HudButton UIAliasCancel;
        HudCheckBox UIAliasEat;
        HudCombo UIAliasActionType;
        HudFixedLayout UIAliasesActionFormLayout;
        HudButton UIAliasAliasPreview;

        private bool started = false;
        private UBAlias editingAlias;
        private BaseAction action;
        private LongStringEditor longStringEditor;

        public Aliases(UtilityBeltPlugin ub, string name) : base(ub, name) {

        }

        public override void Init() {
            base.Init();

            UIAliasesList = (HudList)UB.MainView.view["AliasesList"];
            UIAliasAlias = (HudTextBox)UB.MainView.view["AliasAlias"];
            UIAliasUpdate = (HudButton)UB.MainView.view["AliasUpdate"];
            UIAliasCancel = (HudButton)UB.MainView.view["AliasCancel"];
            UIAliasEat = (HudCheckBox)UB.MainView.view["AliasEat"];
            UIAliasActionType = (HudCombo)UB.MainView.view["AliasActionType"];
            UIAliasesActionFormLayout = (HudFixedLayout)UB.MainView.view["AliasesActionFormLayout"];
            UIAliasAliasPreview = (HudButton)UB.MainView.view["AliasAliasPreview"];

            foreach (var item in Enum.GetValues(typeof(BaseAction.ActionType)).Cast<BaseAction.ActionType>())
                UIAliasActionType.AddItem(item.ToString(), item.ToString());

            UIAliasActionType.Change += UIAliasActionType_Change;
            UIAliasUpdate.Hit += UIAliasUpdate_Hit;
            UIAliasCancel.Hit += UIAliasCancel_Hit;
            UIAliasAliasPreview.Hit += UIAliasAliasPreview_Hit;
            UIAliasesList.Click += UIAliasesList_Click;

            UIAliasCancel.Visible = false;
            UIAliasEat.Visible = false;

            TryEnable();
            Enabled.Changed += Enabled_Changed;
            DefinedAliases.Changed += DefinedAliases_Changed;

            UpdateList();
            DrawActionForm();
        }

        #region UI Event Handlers
        private void UIAliasesList_Click(object sender, int row, int col) {
            switch (col) {
                case 0: // alias
                case 1: // action
                    Edit(DefinedAliases.Value[row]);
                    break;
                case 2: // delete
                    DefinedAliases.Value.RemoveAt(row);
                    break;
            }
            if (col != 2) // 2 is delete icon
                return;
        }

        private void UIAliasAliasPreview_Hit(object sender, EventArgs e) {
            if (longStringEditor == null || longStringEditor.IsDisposed) {
                longStringEditor = new LongStringEditor(UB.MainView.view, UIAliasAlias);
            }
        }

        private void UIAliasActionType_Change(object sender, EventArgs e) {
            DrawActionForm();
        }

        private void UIAliasUpdate_Hit(object sender, EventArgs e) {
            if (string.IsNullOrEmpty(UIAliasAlias.Text)) {
                LogError($"Alias cannot be empty.");
                return;
            }
            try {
                var re = new Regex(UIAliasAlias.Text);
            }
            catch (Exception ex) {
                LogError($"Invalid Alias Regex. {ex.Message}");
                return;
            }

            if (editingAlias != null) {
                var index = DefinedAliases.Value.IndexOf(editingAlias);
                editingAlias.Action = action;
                editingAlias.Alias = UIAliasAlias.Text;
                editingAlias.Eat = UIAliasEat.Checked;
                DefinedAliases.Value.RemoveAt(index);
                DefinedAliases.Value.Insert(index, editingAlias);
            }
            else {
                var alias = new UBAlias(UIAliasAlias.Text, action, UIAliasEat.Checked);
                DefinedAliases.Value.Add(alias);
            }
            ClearForm();
        }

        private void UIAliasCancel_Hit(object sender, EventArgs e) {
            ClearForm();
        }
        #endregion UI Event Handlers

        private void UpdateList() {
            UIAliasesList.ClearRows();
            foreach (var alias in DefinedAliases.Value) {
                var row = UIAliasesList.AddRow();

                ((HudStaticText)row[0]).Text = alias.Alias;
                ((HudStaticText)row[1]).Text = alias.Action.ToString();
                ((HudPictureBox)row[2]).Image = 0x060011F8; // delete
            }
        }

        private void DrawActionForm() {
            action?.ClearForm(UIAliasesActionFormLayout);
            var type = ((HudStaticText)UIAliasActionType[UIAliasActionType.Current]).Text;
            action = BaseAction.FromType((BaseAction.ActionType)Enum.Parse(typeof(BaseAction.ActionType), type));
            action.DrawForm(UIAliasesActionFormLayout);
        }

        private void ClearForm() {
            editingAlias = null;
            action?.ClearForm(UIAliasesActionFormLayout);
            UIAliasCancel.Visible = false;
            UIAliasUpdate.Text = "Add";
            UIAliasAlias.Text = "";
            UIAliasActionType.Current = 0;
            UIAliasEat.Checked = true;
            action?.ClearForm(UIAliasesActionFormLayout);
            action = BaseAction.FromType(0);
            action.DrawForm(UIAliasesActionFormLayout);
        }

        private void Edit(UBAlias alias) {
            ClearForm();
            action.ClearForm(UIAliasesActionFormLayout);
            editingAlias = alias;
            UIAliasActionType.Current = (int)editingAlias.Action.Type;
            action = editingAlias.Action.Clone();
            action.DrawForm(UIAliasesActionFormLayout);
            UIAliasAlias.Text = editingAlias.Alias;
            UIAliasEat.Checked = editingAlias.Eat;
            UIAliasUpdate.Text = "Update";
            UIAliasCancel.Visible = true;
        }

        private void TryEnable() {
            if (Enabled && !started) {
                UB.Core.CommandLineText += Core_CommandLineText;
                started = true;
            }
            else if (!Enabled)
                Disable();
        }

        private void Disable() {
            if (!started)
                return;
            UB.Core.CommandLineText -= Core_CommandLineText;
            started = false;
        }

        private void Enabled_Changed(object sender, SettingChangedEventArgs e) {
            TryEnable();
        }

        private void DefinedAliases_Changed(object sender, SettingChangedEventArgs e) {
            UpdateList();
        }

        private void Core_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            var text = e.Text.Trim(' ');
            foreach (var alias in DefinedAliases.Value) {
                if (alias.AliasRegex.IsMatch(text)) {
                    if (alias.Eat)
                        e.Eat = true;
                    Match match = alias.AliasRegex.Match(text);
                    foreach (var groupName in alias.AliasRegex.GetGroupNames()) {
                        UB.VTank.Setvar($"capturegroup_{groupName}", match.Groups[groupName].Value);
                    }
                    alias.Run(text);
                }
            }
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            DefinedAliases.Changed += DefinedAliases_Changed;
            Enabled.Changed -= Enabled_Changed;
            Disable();
        }
    }
}

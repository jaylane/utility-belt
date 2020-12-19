using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirindiViewService.Controls;
using System.ComponentModel;

namespace UtilityBelt.Views {
    public class KeyValueTreeView : HudList {
        public class TreeItem : INotifyPropertyChanged, IDisposable {
            public event PropertyChangedEventHandler PropertyChanged;
            public event EventHandler Clicked;

            private string _key = "";
            public string Key {
                get => _key;
                set {
                    _key = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Key"));
                }
            }

            private string _value = "";
            public string Value {
                get => _value;
                set {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
                }
            }

            private bool _expanded = false;
            public bool Expanded {
                get => _expanded;
                set {
                    _expanded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Expanded"));
                }
            }

            private bool _hasChanges = false;
            public bool HasChanges {
                get {
                    return HasChildren ? Children.Any(t => t.HasChanges) : _hasChanges;
                }
                set {
                    _hasChanges = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HasChanges"));
                }
            }

            public bool Visible {
                get {
                    var p = Parent;
                    while (p != null) {
                        if (p.Expanded == false)
                            return false;
                        p = p.Parent;
                    }
                    return true;
                }
            }

            public TreeItem Parent { get; private set; }
            public List<TreeItem> Children { get; } = new List<TreeItem>();
            public bool HasChildren { get => Children.Count > 0; }
            public bool IsLast { get => Parent == null ? true : Parent.Children.Last() == this; }
            public int Depth { get; private set; }
            public string Description { get; set; } = "";

            public TreeItem(string key, string value, TreeItem parent=null, string description="") {
                Key = key;
                Value = value;
                Parent = parent;
                Depth = 0;
                Description = string.IsNullOrEmpty(description) ? Key : description;

                var p = parent;
                while (p != null) {
                    Depth++;
                    p = p.Parent;
                }
            }

            private void Child_PropertyChanged(object sender, PropertyChangedEventArgs e) {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Children"));
            }

            public void Click() {
                if (HasChildren)
                    Expanded = !Expanded;
                Clicked?.Invoke(this, EventArgs.Empty);
            }

            public void AddChildren(IEnumerable<TreeItem> items) {
                Children.AddRange(items);

                foreach (var child in items) {
                    child.PropertyChanged += Child_PropertyChanged;
                }
            }

            public void AddChild(TreeItem child) {
                Children.Add(child);
                child.PropertyChanged += Child_PropertyChanged;
            }

            public void RemoveChild(TreeItem child) {
                child.PropertyChanged -= Child_PropertyChanged;
                Children.Remove(child);
            }

            public void Dispose() {
                foreach (var child in Children) {
                    child.PropertyChanged -= Child_PropertyChanged;
                    child.Dispose();
                }
            }
        }

        private string _filterText = "";
        public string FilterText {
            get => _filterText;
            set {
                _filterText = value;
                Redraw();
            }
        }

        public bool HasFilter { get => FilterText.Length > 0; }

        public List<TreeItem> Nodes = new List<TreeItem>();
        public List<TreeItem> NodeRowCache = new List<TreeItem>();

        public TreeItem SelectedNode { get; private set; } = null;

        public KeyValueTreeView() : base() {
            AddColumn(typeof(HudStaticText), 220, "Key");
            AddColumn(typeof(HudStaticText), 999, "Value");

            Click += KeyValueTreeView_Click;
        }

        private void KeyValueTreeView_Click(object sender, int row, int col) {
            var node = NodeFromRow(row);
            node.Click();

            if (SelectedNode != null) {
                var index = RowFromNode(SelectedNode);
                if (index >= 0) {
                    var selectedRow = this[index];
                    ((HudStaticText)this[index][0]).TextColor = Theme.GetColor("ListText");
                    ((HudStaticText)this[index][1]).TextColor = Theme.GetColor("ListText");
                }
                SelectedNode = null;
            }

            if (node.HasChildren)
                return;

            SelectedNode = node;
            ((HudStaticText)this[row][0]).TextColor = System.Drawing.Color.Red;
            ((HudStaticText)this[row][1]).TextColor = System.Drawing.Color.Red;
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            Redraw();
        }

        private TreeItem NodeFromRow(int row) {
            return NodeRowCache[row];
        }

        private int RowFromNode(TreeItem node) {
            return NodeRowCache.FindIndex(n => n.Equals(node));
        }

        public void AddTreeItems(IEnumerable<TreeItem> items) {
            Nodes.AddRange(items);
            foreach (var item in items) {
                item.PropertyChanged += Item_PropertyChanged;
            }
            Redraw();
        }

        public void AddTreeItem(TreeItem item) {
            Nodes.Add(item);
            item.PropertyChanged += Item_PropertyChanged;
            Redraw();
        }

        public void RemoveTreeItem(TreeItem item) {
            item.PropertyChanged -= Item_PropertyChanged;
            Nodes.Remove(item);
            Redraw();
        }

        private void Redraw() {
            var scrollPosition = ScrollPosition;
            ClearRows();
            foreach (var node in Nodes) {
                DrawNode(node);
            }
            ScrollPosition = scrollPosition;
            UpdateNodeRowCache();
        }

        private void UpdateNodeRowCache(TreeItem parentNode=null) {
            var nodes = parentNode == null ? Nodes : parentNode.Children;

            if (parentNode == null)
                NodeRowCache.Clear();

            foreach (var node in nodes) {
                if ((!HasFilter && node.Visible) || (HasFilter && NodeMatchesFilter(node))) {
                    NodeRowCache.Add(node);
                    UpdateNodeRowCache(node);
                }
            }
        }

        private void DrawNode(TreeItem node) {
            if (HasFilter && !NodeMatchesFilter(node))
                return;

            var expanded = HasFilter || node.Expanded;
            var row = AddRow();
            var text = GetPadding(node.Depth);
            text += ((node.Depth <= 1 || node.HasChildren) ? "" : " ") + " ";
            text += (node.HasChildren ? (node.Depth == 0 ? "" : " ") + (expanded ? "[-]" : "[+]") : "") + (node.Depth > 0 ? " " : "");
            text += node.Key;
            text += (node.HasChanges) ? "*" : "";
            text += (node.HasChildren ? "" : " " + (new String('.', 100)));

            ((HudStaticText)row[0]).Text = text;
            ((HudStaticText)row[1]).Text = node.HasChildren ? (!expanded ? "(click to expand)" : "") : node.Value;

            if (SelectedNode == node) {
                ((HudStaticText)row[0]).TextColor = System.Drawing.Color.Red;
                ((HudStaticText)row[1]).TextColor = System.Drawing.Color.Red;
            }

            if (!expanded)
                return;

            foreach (var child in node.Children) {
                DrawNode(child);
            }
        }

        private bool NodeMatchesFilter(TreeItem node) {
            if (node.HasChildren) {
                foreach (var child in node.Children) {
                    if (NodeMatchesFilter(child))
                        return true;
                }
            }
            else if (node.Description.ToLower().Contains(FilterText.ToLower()))
                return true;

            return false;
        }

        internal void ExpandAll(bool expand=true, TreeItem parent = null) {
            var children = parent == null ? Nodes : parent.Children;
            foreach (var child in children) {
                child.Expanded = expand;
                ExpandAll(expand, child);
            }
        }

        private string GetPadding(int depth, bool hasLastParent = false) {
            return new string(' ', depth * 2);
        }

        public override void Dispose() {
            Click -= KeyValueTreeView_Click;
            foreach (var node in Nodes) {
                node.PropertyChanged -= Item_PropertyChanged;
                node.Dispose();
            }

            base.Dispose();
        }
    }
}

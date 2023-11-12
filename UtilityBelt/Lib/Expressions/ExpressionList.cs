using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace UtilityBelt.Lib.Expressions {
    public class ExpressionList : ExpressionObjectBase {
        public ObservableCollection<object> Items { get; set; } = new ObservableCollection<object>();

        public ExpressionList(IEnumerable<object> items = null) {
            IsSerializable = true;
            Items.CollectionChanged += Items_CollectionChanged;

            if (items != null) {
                AddRange(items);
            }
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            InvokeChange();
        }

        public void Clear() {
            Items.Clear();
        }

        public void AddRange(IEnumerable<object> items) {
            foreach (var item in items)
                Items.Add(item);
        }

        public override string ToString() {
            return $"[{string.Join(",", Items.Select(o => o.ToString()).ToArray())}]";
        }
    }
}

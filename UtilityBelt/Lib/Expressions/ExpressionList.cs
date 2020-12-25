using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace UtilityBelt.Lib.Expressions {
    public class ExpressionList : ExpressionObjectBase {
        public ObservableCollection<object> Items { get; set; } = new ObservableCollection<object>();

        public ExpressionList() {
            IsSerializable = true;
            Items.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            InvokeChange();
        }

        public override string ToString() {
            return $"[{string.Join(",", Items.Select(o => o.ToString()).ToArray())}]";
        }
    }
}

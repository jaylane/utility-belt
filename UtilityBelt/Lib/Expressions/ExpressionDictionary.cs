using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace UtilityBelt.Lib.Expressions {
    public class ExpressionDictionary : ExpressionObjectBase {

        //this class is currently supplied by Exceptionless. Will need to add the package https://www.nuget.org/packages/MSFT.ParallelExtensionsExtras/ once Exceptionless is removed.
        public ObservableConcurrentDictionary<string, object> Items { get; set; } = new ObservableConcurrentDictionary<string, object>();

        public ExpressionDictionary() {
            IsSerializable = true;
            Items.CollectionChanged += Items_CollectionChanged;
        }

        public void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            InvokeChange();
        }

        public override string ToString() {
            var kvp = Items.Keys.Select(key => $"{key}=>{Items[key]}").ToArray();
            return $"[{string.Join(",", kvp)}]";
        }
    }
}

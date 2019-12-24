using LiteDB;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Models;

namespace UtilityBelt.Lib {
    public class Database : IDisposable {
        internal LiteDatabase ldb;
        internal LiteCollection<Landblock> Landblocks;
        internal LiteCollection<Weenie> Weenies;
        public static string DBPath { get { return Path.Combine(Util.GetPluginDirectory(), "utilitybelt.db"); } }

        public Database() {
            ldb = new LiteDatabase(DBPath);
            Weenies = ldb.GetCollection<Weenie>("weenies");
            Landblocks = ldb.GetCollection<Landblock>("landblocks");
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (ldb != null) {
                        ldb.Dispose();
                    }
                }
                disposedValue = true;
            }
        }
        public void Dispose() {
            Dispose(true);
        }
        #endregion
    }
}

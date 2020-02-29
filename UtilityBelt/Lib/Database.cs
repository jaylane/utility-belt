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
        private LiteCollection<Landblock> landblocks;
        internal LiteCollection<Landblock> Landblocks {
            get {
                if (landblocks == null)
                    Init();
                return landblocks;
            }
        }
        private LiteCollection<Weenie> weenies;
        internal LiteCollection<Weenie> Weenies {
            get {
                if (weenies == null)
                    Init();
                return weenies;
            }
        }
        private LiteCollection<PersistentVariable> persistentVariables;
        internal LiteCollection<PersistentVariable> PersistentVariables {
            get {
                if (persistentVariables == null)
                    Init();
                return persistentVariables;
            }
        }
        private LiteCollection<GlobalVariable> globalVariables;
        internal LiteCollection<GlobalVariable> GlobalVariables {
            get {
                if (globalVariables == null)
                    Init();
                return globalVariables;
            }
        }
        public static string DBPath { get; private set; }

        public Database(string dbPath) {
            DBPath = dbPath;
        }

        private void Init() {
            if (ldb != null)
                return;

            ldb = new LiteDatabase(DBPath);
            weenies = ldb.GetCollection<Weenie>("weenies");
            landblocks = ldb.GetCollection<Landblock>("landblocks");
            persistentVariables = ldb.GetCollection<PersistentVariable>("persistent_variables");
            globalVariables = ldb.GetCollection<GlobalVariable>("global_variables");

            persistentVariables.EnsureIndex("Server");
            persistentVariables.EnsureIndex("Character");
            persistentVariables.EnsureIndex("Name");

            globalVariables.EnsureIndex("Server");
            globalVariables.EnsureIndex("Name");
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

        internal void Shrink() {
            Init();
            ldb.Shrink();
        }
        #endregion
    }
}

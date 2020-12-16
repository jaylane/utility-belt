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
        private bool didInitRetry = false;
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
        private LiteCollection<QuestFlag> questFlags;
        internal LiteCollection<QuestFlag> QuestFlags {
            get {
                if (questFlags == null)
                    Init();
                return questFlags;
            }
        }
        public static string DBPath { get; private set; }

        public Database(string dbPath) {
            DBPath = dbPath;
        }

        private void Init() {
            try {
                if (ldb != null)
                    ldb.Dispose();

                ldb = new LiteDatabase(DBPath);
                weenies = ldb.GetCollection<Weenie>("weenies");
                landblocks = ldb.GetCollection<Landblock>("landblocks");
                persistentVariables = ldb.GetCollection<PersistentVariable>("persistent_variables");
                globalVariables = ldb.GetCollection<GlobalVariable>("global_variables");
                questFlags = ldb.GetCollection<QuestFlag>("quest_flags");

                persistentVariables.EnsureIndex("Server");
                persistentVariables.EnsureIndex("Character");
                persistentVariables.EnsureIndex("Name");

                globalVariables.EnsureIndex("Server");
                globalVariables.EnsureIndex("Name");

                questFlags.EnsureIndex("Server");
                questFlags.EnsureIndex("Character");
                questFlags.EnsureIndex("Key");
            }
            catch (Exception ex) {
                Logger.LogException(ex);
                if (!didInitRetry) {
                    Logger.LogException($"Deleting corrupt database file: {DBPath}");
                    try {
                        File.Move(DBPath, $"{DBPath}.corruptBackup");
                    }
                    catch (Exception innerEx) {
                        Logger.LogException(innerEx);
                    }
                    didInitRetry = true;
                    Init();
                }
            }
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

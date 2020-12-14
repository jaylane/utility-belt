using Microsoft.DirectX;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Text.RegularExpressions;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Dungeon;
using UtilityBelt.Lib.Models;
using UtilityBelt.Lib.Settings;

namespace UtilityBelt.Tools {
    public class DataUpdatedEventArgs : EventArgs {
        public uint Landblock;
        public DataUpdatedEventArgs(uint landblock) {
            Landblock = landblock;
        }
    }

    [Name("Lifestoned")]
    public class LSD : ToolBase {
        private WebClient landblockClient;
        private WebClient weenieClient;

        private Queue<uint> landblocksNeedingDownload = new Queue<uint>();
        private Queue<int> weeniesNeedingDownload = new Queue<int>();

        private Dictionary<uint, List<int>> landblockWeenies = new Dictionary<uint, List<int>>();

        private uint currentLandblockDownload = 0;
        private uint currentWeenieLandblockDownload = 0;
        private Landblock currentLandblock;

        internal int DownloadQueueCount { get { return landblocksNeedingDownload.Count + weeniesNeedingDownload.Count; } }

        internal event EventHandler<DataUpdatedEventArgs> DataUpdated;

        #region Config
        [Summary("Enabled")]
        public readonly Setting<bool> Enabled = new Setting<bool>(false);

        [Summary("Lifestoned web root")]
        public readonly Setting<string> LSDWebRoot = new Setting<string>("https://lifestoned.org/");

        [Summary("How long Lifestoned results are cached for before redownloading (in seconds)")]
        public readonly Setting<int> LSDCacheSeconds = new Setting<int>(60 * 60 * 24 * 180);
        #endregion

        #region Commands
        #region /ub lsdlb
        [Summary("Downloads and caches landblock spawns from Lifestoned.")]
        [Usage("/ub lsdlb <landblock>")]
        [Example("/ub lsdlb 00070000", "Downloads and caches Town Network landblock spawns")]
        [CommandPattern("lsdlb", @"^(?<landblock>[a-z0-9]{8})$")]
        public void ldbdb(string verb, Match args) {
            var t = args.Groups["landblock"].Value.Replace("0x", "");
            if (!uint.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out uint landblock)) {
                LogError($"Unable to parse landblock as hex: {args.Groups["landblock"].Value}");
                return;
            }
            var spawnsReady = EnsureLandblockSpawnsReady(landblock & 0xFFFF0000);
            WriteToChat($"{(landblock & 0xFFFF0000):X8} has spawns ready? {spawnsReady}");
        }
        #endregion
        #region /ub lsdclearcache
        [Summary("Clears all lifestoned cached data")]
        [Usage("/ub lsdclearcache")]
        [Example("/ublsdclearcache", "Clears all lifestoned cached data")]
        [CommandPattern("lsdclearcache", @"^$")]
        public void lsdclearcache(string verb, Match args) {
            var lbCount = UB.Database.Landblocks.Count();
            var weenieCount = UB.Database.Weenies.Count();
            UB.Database.Landblocks.Delete(x => true);
            UB.Database.Weenies.Delete(x => true);
            UB.Database.Shrink();
            WriteToChat($"Cleared {lbCount} landblocks and {weenieCount} weenies from the database");
        }
        #endregion
        #endregion

        public LSD(UtilityBeltPlugin ub, string name) : base(ub, name) {
        }

        public override void Init() {
            base.Init();

            landblockClient = new WebClient();
            landblockClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate);
            landblockClient.DownloadStringCompleted += LbClient_DownloadStringCompleted;

            weenieClient = new WebClient();
            weenieClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate);
            weenieClient.UploadStringCompleted += WeenieClient_UploadStringCompleted;
        }

        internal bool EnsureLandblockSpawnsReady(uint landblock) {
            var col = UB.Database.Landblocks;
            var lb = col.Include(new string[] { "$.Weenies[*].Weenie" }).FindById((int)landblock);
            if (lb == null || DateTime.UtcNow - lb.LastCheck > TimeSpan.FromSeconds(LSDCacheSeconds)) {
                if (!landblocksNeedingDownload.Contains(landblock)) {
                    landblocksNeedingDownload.Enqueue(landblock);
                }
                CheckDownloads();
                return false;
            }

            if (lb != null) {
                var weeniesToCheck = new List<int>();
                foreach (var spawn in lb.Weenies) {
                    if (spawn.Weenie == null) {
                        var weenie = UB.Database.Weenies.FindById(spawn.Wcid);
                        if (weenie == null)
                            weeniesToCheck.Add(spawn.Wcid);
                        else
                            spawn.Weenie = weenie;
                    }
                }
                //TODO: request missing weenies
                UB.Database.Landblocks.Upsert(lb);
            }

            return true;
        }

        private void CheckDownloads() {
            if (weenieClient != null && !weenieClient.IsBusy && landblockClient != null && !landblockClient.IsBusy && landblocksNeedingDownload.Count > 0) {
                var lbToDownload = landblocksNeedingDownload.Dequeue();
                DownloadLandblockSpawns(lbToDownload);
            }
        }

        private void DownloadLandblockSpawns(uint lbToDownload) {
            currentLandblockDownload = (lbToDownload & 0xFFFF0000);
            LogDebug($"Downloading {lbToDownload:X8} from {LSDWebRoot}Spawn/Download/{lbToDownload}");
            landblockClient.DownloadStringAsync(new Uri($"{LSDWebRoot}Spawn/Download/{lbToDownload}"));
        }

        private void WeenieClient_UploadStringCompleted(object sender, UploadStringCompletedEventArgs e) {
            try {
                weenieClient.CancelAsync();
                var lb = currentWeenieLandblockDownload;
                currentWeenieLandblockDownload = 0;

                if (e.Cancelled) {
                    LogError($"WeenieClient e.Cancelled");
                    return;
                }
                if (e.Error != null) {
                    LogError($"WeenieClient: {e.Error}");
                    Logger.LogException(e.Error);
                    return;
                }

                var weenieData = JArray.Parse(e.Result.ToString());
                foreach (var weenie in weenieData) {
                    if (weenie.Type == JTokenType.Object) {
                        var w = UB.Database.Weenies.FindById(weenie["wcid"].ToObject<int>());
                        if (w == null) {
                            w = new Weenie() {
                                Id = weenie["wcid"].ToObject<int>(),
                                Type = weenie["weenieType"].ToObject<int>(),
                                LastCheck = DateTime.UtcNow
                            };
                        }

                        if (weenie.SelectToken("intStats") != null) {
                            foreach (var intStat in weenie["intStats"]) {
                                if (w.IntStats.ContainsKey(intStat["key"].ToObject<int>()))
                                    w.IntStats[intStat["key"].ToObject<int>()] = intStat["value"].ToObject<int>();
                                else
                                    w.IntStats.Add(intStat["key"].ToObject<int>(), intStat["value"].ToObject<int>());
                            }
                        }
                        if (weenie.SelectToken("stringStats") != null) {
                            foreach (var stringStat in weenie["stringStats"]) {
                                if (w.StringStats.ContainsKey(stringStat["key"].ToObject<int>()))
                                    w.StringStats[stringStat["key"].ToObject<int>()] = stringStat["value"].ToString();
                                else
                                    w.StringStats.Add(stringStat["key"].ToObject<int>(), stringStat["value"].ToString());
                            }
                        }
                        if (weenie.SelectToken("didStats") != null) {
                            foreach (var didStat in weenie["didStats"]) {
                                if (w.DIDStats.ContainsKey(didStat["key"].ToObject<int>()))
                                    w.DIDStats[didStat["key"].ToObject<int>()] = didStat["value"].ToObject<int>();
                                else
                                    w.DIDStats.Add(didStat["key"].ToObject<int>(), didStat["value"].ToObject<int>());
                            }
                        }
                        if (weenie.SelectToken("floatStats") != null) {
                            foreach (var floatStat in weenie["floatStats"]) {
                                if (w.FloatStats.ContainsKey(floatStat["key"].ToObject<int>()))
                                    w.FloatStats[floatStat["key"].ToObject<int>()] = floatStat["value"].ToObject<float>();
                                else
                                    w.FloatStats.Add(floatStat["key"].ToObject<int>(), floatStat["value"].ToObject<float>());
                            }
                        }
                        if (weenie.SelectToken("boolStats") != null) {
                            foreach (var boolStat in weenie["boolStats"]) {
                                if (w.BoolStats.ContainsKey(boolStat["key"].ToObject<int>()))
                                    w.BoolStats[boolStat["key"].ToObject<int>()] = boolStat["value"].ToObject<bool>();
                                else
                                    w.BoolStats.Add(boolStat["key"].ToObject<int>(), boolStat["value"].ToObject<bool>());
                            }
                        }

                        UB.Database.Weenies.Upsert(w);

                        if (currentLandblock != null) {
                            var spawns = currentLandblock.Weenies.FindAll(x => x.Wcid == w.Id);
                            foreach (var spawn in spawns) {
                                spawn.Weenie = w;
                            }
                            UB.Database.Landblocks.Upsert(currentLandblock);
                        }
                    }
                }
                DataUpdated?.Invoke(this, new DataUpdatedEventArgs(lb));
                currentLandblock = null;
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void LbClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e) {
            try {
                landblockClient.CancelAsync();
                var lb = currentLandblockDownload;
                currentLandblockDownload = 0;
                currentWeenieLandblockDownload = lb;

                if (e.Cancelled) return;
                if (e.Error != null) {
                    var landblock = UB.Database.Landblocks.FindById((int)lb);
                    if (landblock == null) {
                        landblock = new Landblock() {
                            Id = (int)lb,
                            CheckFailCount = 0
                        };
                    }
                    landblock.CheckFailCount++;
                    landblock.LastCheck = DateTime.UtcNow;
                    UB.Database.Landblocks.Upsert(landblock);
                    return;
                }

                try {
                    currentLandblock = new Landblock() {
                        Id = (int)lb,
                        CheckFailCount = 0,
                        LastCheck = DateTime.UtcNow
                    };

                    try {
                        var lbData = JObject.Parse(e.Result.ToString());
                        foreach (var link in lbData["value"]["links"]) {
                            var l = new Link() {
                                Source = link["source"].ToObject<int>(),
                                Target = link["target"].ToObject<int>()
                            };

                            if (link.SelectToken("desc") != null)
                                l.Description = link["desc"].ToString();

                            currentLandblock.Links.Add(l);
                        }
                        foreach (var spawnData in lbData["value"]["weenies"]) {
                            var spawn = new WeenieSpawn() {
                                Id = spawnData["id"].ToObject<int>(),
                                Wcid = spawnData["wcid"].ToObject<int>(),
                                Position = new Position() {
                                    Landcell = spawnData["pos"]["objcell_id"].ToObject<uint>(),
                                    Frame = new Frame() {
                                        Origin = new Origin() {
                                            X = spawnData["pos"]["frame"]["origin"]["x"].ToObject<float>(),
                                            Y = spawnData["pos"]["frame"]["origin"]["y"].ToObject<float>(),
                                            Z = spawnData["pos"]["frame"]["origin"]["z"].ToObject<float>()
                                        },
                                        Angles = new Angles() {
                                            X = spawnData["pos"]["frame"]["angles"]["x"].ToObject<float>(),
                                            Y = spawnData["pos"]["frame"]["angles"]["y"].ToObject<float>(),
                                            Z = spawnData["pos"]["frame"]["angles"]["z"].ToObject<float>(),
                                            W = spawnData["pos"]["frame"]["angles"]["w"].ToObject<float>()
                                        }
                                    }
                                }
                            };
                            
                            currentLandblock.Weenies.Add(spawn);
                        }
                    }
                    catch (Exception ex) {
                        Logger.LogException(ex);
                        currentLandblock.CheckFailCount++;
                    }
                    UB.Database.Landblocks.Delete(currentLandblock.Id);
                    UB.Database.Landblocks.Insert(currentLandblock);

                    RequestWeenies(currentLandblock.Weenies.Select((weenie, index) => weenie.Wcid));
                }
                catch (Exception ex) {
                    Logger.LogException(ex);
                    LogError($"Unable to parse downloaded landblock: {(lb >> 16):X4} {ex.Message}");
                    return;
                }
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void RequestWeenies(IEnumerable<int> weenies) {
            var neededWeenies = new List<int>();

            foreach (var id in weenies) {
                var weenie = UB.Database.Weenies.FindById(id);
                if (weenie == null || DateTime.UtcNow - weenie.LastCheck > TimeSpan.FromSeconds(LSDCacheSeconds)) {
                    if (!neededWeenies.Contains(id))
                        neededWeenies.Add(id);
                }

                if (weenie != null) {
                    var spawns = currentLandblock.Weenies.FindAll(x => x.Wcid == weenie.Id);
                    foreach (var spawn in spawns) {
                        if (spawn.Weenie == null)
                            spawn.Weenie = UB.Database.Weenies.FindById(spawn.Wcid);
                    }
                }
            }
            UB.Database.Landblocks.Upsert(currentLandblock);

            if (neededWeenies.Count == 0) {
                currentLandblock = null;
                CheckDownloads();
                return;
            }

            weenieClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            var urlencoded = "props=wcid&props=weenieType&props=intStats&props=stringStats&props=didStats&props=floatStats&props=boolStats";
            var ids = "";
            foreach (var wcid in neededWeenies) {
                urlencoded += "&ids=" + wcid.ToString();
                ids += wcid + ",";
            }

            LogDebug($"Downloading {neededWeenies.Count} weenies for lb {currentWeenieLandblockDownload:X8} {ids}");

            weenieClient.UploadStringAsync(new Uri($"{LSDWebRoot}Weenie/DownloadCustom/"), "POST", urlencoded);
        }
    }
}

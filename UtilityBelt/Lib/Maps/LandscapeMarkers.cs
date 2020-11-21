using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace UtilityBelt.Lib.Maps {
    class LandscapeMarkers {
        public enum MarkerType {
            Unknown = 0,
            Town,
            Lifestone,
            Vendor,
            NPC,
            Portal,
            Outpost,
            Dungeon
        }

        internal class MarkerDisplayOptions {
            public double MinLabelZoomLevel = 0.0;
            public double MaxLabelZoomLevel = 1.0;
            public double MinMarkerZoomLevel = 0.0;
            public double MaxMarkerZoomLevel = 1.0;
            public int Icon;
        }

        public readonly Dictionary<MarkerType, MarkerDisplayOptions> DisplayOptions = new Dictionary<MarkerType, MarkerDisplayOptions>();

        public class Marker {
            public int Id { get; private set; }
            public string Name { get; set; }
            public MarkerType Type { get; set; }
            public double NS { get; set; }
            public double EW { get; set; }
            public double DestNS { get; set; }
            public double DestEW { get; set; }
            public uint DestLandcell { get; set; }
            public Marker(int id) {
                Id = id;
            }
        }

        public Dictionary<int, Marker> Markers = new Dictionary<int, Marker>();
        private int _id = 0;

        public LandscapeMarkers() {
            try {
                SetupDisplayOptions();

                LoadPortalData();
                LoadCSVMapData("npcs", MarkerType.NPC);
                LoadCSVMapData("vendors", MarkerType.Vendor);
                LoadCSVMapData("lifestones", MarkerType.Lifestone);

                LoadTowns();
            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private void SetupDisplayOptions() {
            DisplayOptions.Add(MarkerType.Unknown, new MarkerDisplayOptions() {
                MinMarkerZoomLevel = 0,
                MinLabelZoomLevel = 0,
                Icon = 100667504
            });
            DisplayOptions.Add(MarkerType.Town, new MarkerDisplayOptions() {
                MinMarkerZoomLevel = 0,
                MinLabelZoomLevel = 0,
                Icon = 100667518
            });
            DisplayOptions.Add(MarkerType.Outpost, new MarkerDisplayOptions() {
                MinMarkerZoomLevel = 0.1,
                MinLabelZoomLevel = 0.25,
                Icon = 100671984
            });
            DisplayOptions.Add(MarkerType.Portal, new MarkerDisplayOptions() {
                MinMarkerZoomLevel = 0.1,
                MinLabelZoomLevel = 0.32,
                Icon = 100667499
            });
            DisplayOptions.Add(MarkerType.Dungeon, new MarkerDisplayOptions() {
                MinMarkerZoomLevel = 0.1,
                MinLabelZoomLevel = 0.32,
                Icon = 0x06005B57
            });
            DisplayOptions.Add(MarkerType.Lifestone, new MarkerDisplayOptions() {
                MinMarkerZoomLevel = 0.24,
                MinLabelZoomLevel = 0.4,
                Icon = 0x060024E1
            });
            DisplayOptions.Add(MarkerType.NPC, new MarkerDisplayOptions() {
                MinMarkerZoomLevel = 0.4,
                MinLabelZoomLevel = 0.8,
                Icon = 100667446
            });
            DisplayOptions.Add(MarkerType.Vendor, new MarkerDisplayOptions() {
                MinMarkerZoomLevel = 0.4,
                MinLabelZoomLevel = 0.8,
                Icon = 100672159
            });
        }

        public void LoadPortalData() {
            var markers = LoadPortalsCSV("UtilityBelt.Resources.mapdata.portals.csv");
            foreach (var marker in markers) {
                var dungeon = Dungeon.Dungeon.GetCached((int)marker.DestLandcell);
                if (dungeon.IsDungeon()) {
                    marker.Type = MarkerType.Dungeon;
                    marker.Name = dungeon.Name;
                }
                else {
                    marker.Type = MarkerType.Portal;
                }
                Markers.Add(marker.Id, marker);
            }
        }

        public void LoadCSVMapData(string name, MarkerType type) {
            var markers = LoadCSV("UtilityBelt.Resources.mapdata." + name + ".csv");
            markers.ForEach(m => {
                m.Type = type;
                Markers.Add(m.Id, m);
            });
        }

        public List<Marker> LoadCSV(string resourcePath) {
            var markers = new List<Marker>();
            using (Stream manifestResourceStream = typeof(LandscapeMarkers).Assembly.GetManifestResourceStream(resourcePath)) {
                using (StreamReader reader = new StreamReader(manifestResourceStream)) {
                    string line;
                    reader.ReadLine(); // columns header
                    var lineRe = new Regex(@"^""(?<classId>\d+)"",""(?<name>.*)"",""(?<landcell>[0-9A-F]{8})"",""(?<x>\-?[\d\.]+)"",""(?<y>\-?[\d\.]+)"",""(?<z>\-?[\d\.]+)""$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    while ((line = reader.ReadLine()) != null) {
                        if (!lineRe.IsMatch(line))
                            continue;

                        var matches = lineRe.Match(line);
                        uint landcell = (uint)int.Parse(matches.Groups["landcell"].Value, System.Globalization.NumberStyles.HexNumber);
                        var x = float.Parse(matches.Groups["x"].Value);
                        var y = float.Parse(matches.Groups["y"].Value);

                        markers.Add(new Marker(++_id) {
                            Name = matches.Groups["name"].Value,
                            NS = Geometry.LandblockToNS(landcell, y),
                            EW = Geometry.LandblockToEW(landcell, x)
                        });
                    }
                }
            }
            return markers;
        }

        public List<Marker> LoadPortalsCSV(string resourcePath) {
            var markers = new List<Marker>();
            using (Stream manifestResourceStream = typeof(LandscapeMarkers).Assembly.GetManifestResourceStream(resourcePath)) {
                using (StreamReader reader = new StreamReader(manifestResourceStream)) {
                    string line;
                    reader.ReadLine(); // columns header
                    var lineRe = new Regex(@"^""(?<classId>\d+)"",""(?<name>.*)"",""(?<landcell>[0-9A-F]{8})"",""(?<x>\-?[\d\.]+)"",""(?<y>\-?[\d\.]+)"",""(?<z>\-?[\d\.]+)"",""(?<destCell>[0-9A-F]{8})"",""(?<dx>\-?[\d\.]+)"",""(?<dy>\-?[\d\.]+)"",""(?<dz>\-?[\d\.]+)""$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    while ((line = reader.ReadLine()) != null) {
                        if (!lineRe.IsMatch(line))
                            continue;

                        var matches = lineRe.Match(line);
                        uint landcell = uint.Parse(matches.Groups["landcell"].Value, System.Globalization.NumberStyles.HexNumber);
                        uint dlandcell = uint.Parse(matches.Groups["destCell"].Value, System.Globalization.NumberStyles.HexNumber);
                        var x = float.Parse(matches.Groups["x"].Value);
                        var y = float.Parse(matches.Groups["y"].Value);
                        var dx = float.Parse(matches.Groups["dx"].Value);
                        var dy = float.Parse(matches.Groups["dy"].Value);

                        markers.Add(new Marker(++_id) {
                            Name = matches.Groups["name"].Value,
                            NS = Geometry.LandblockToNS(landcell, y),
                            EW = Geometry.LandblockToEW(landcell, x),
                            DestEW = Geometry.LandblockToEW(dlandcell, dx),
                            DestNS = Geometry.LandblockToNS(dlandcell, dy),
                            DestLandcell = dlandcell
                        });
                    }
                }
            }
            return markers;
        }

        public void LoadTowns() {
            using (Stream manifestResourceStream = typeof(LandscapeMarkers).Assembly.GetManifestResourceStream("UtilityBelt.Resources.mapdata.towns.csv")) {
                using (StreamReader reader = new StreamReader(manifestResourceStream)) {
                    string line;
                    reader.ReadLine(); // columns header
                    var lineRe = new Regex(@"^(?<name>.*),(?<type>.*),(?<ns>\-?[\d\.]+),(?<ew>\-?[\d\.]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    while ((line = reader.ReadLine()) != null) {
                        if (!lineRe.IsMatch(line))
                            continue;

                        var matches = lineRe.Match(line);

                        var marker = new Marker(++_id) {
                            Name = matches.Groups["name"].Value,
                            NS = float.Parse(matches.Groups["ns"].Value),
                            EW = float.Parse(matches.Groups["ew"].Value),
                            Type = (MarkerType)Enum.Parse(typeof(MarkerType), matches.Groups["type"].Value)
                        };
                        Markers.Add(marker.Id, marker);
                    }
                }
            }
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;

namespace UtilityBelt.Lib {
    public class GitLabTagData {
        public string name = "";
        public string tag_name = "";
        public DateTime created_at = DateTime.MinValue;
        public DateTime released_at = DateTime.MinValue;
        public string description = "";

    }

    public static class UpdateChecker {
        public static void CheckForUpdate(bool loud=false) {
            if (loud) Util.WriteToChat("Checking for update");

            new Thread(() => {
                Thread.CurrentThread.IsBackground = true;
                var json = FetchGitlabData();
                if (!string.IsNullOrEmpty(json)) {
                    OnGitlabFetchComplete(json, loud);
                }
            }).Start();
        }

        public static string FetchGitlabData() {
            // no tls 1.2 in dotnet 3.5???
            try {
                var url = string.Format(@"http://http.haxit.org/ubupdatecheck.php");

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 30000;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) {
                    using (Stream stream = response.GetResponseStream()) {
                        using (StreamReader reader = new StreamReader(stream)) {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch {}

            return null;
        }

        private static void OnGitlabFetchComplete(string json, bool loud) {
            try {
                if (!string.IsNullOrEmpty(json)) {
                    try {
                        var UB = UtilityBeltPlugin.Instance;
                        var tags = JsonConvert.DeserializeObject<GitLabTagData[]>(json);
                        Version version = System.Reflection.Assembly.GetAssembly(typeof(UtilityBeltPlugin)).GetName().Version;
                        bool foundUpdate = false;

                        foreach (var tag in tags) {
                            try {
                                Version releaseVersion = new Version(tag.tag_name.Replace("release-", "") + ".0");

                                if (releaseVersion.CompareTo(version) >= 1 || (releaseVersion.CompareTo(version) == 0 && !Util.IsReleaseVersion())) {
                                    var lines = new List<string>(tag.description.Split('\r'));
                                    lines.RemoveAt(0);
                                    lines = lines.Where(s => !string.IsNullOrEmpty(s.Trim())).Distinct().ToList();

                                    var description = string.Join("", lines.ToArray());
                                    UB.Host.Actions.AddChatText($"[UB] Version {releaseVersion.ToString()} is now available! {description}", 3);
                                    UB.Host.Actions.AddChatText($"Get it here: <Tell:IIDString:{Util.GetChatId()}:openurl|https://utilitybelt.gitlab.io/>https://utilitybelt.gitlab.io/</Tell>", 3);
                                    foundUpdate = true;
                                    break;
                                }
                            }
                            catch (Exception ex) { Logger.LogException(ex); }
                        }

                        if (!foundUpdate) {
                            Util.WriteToChat("Plugin is up to date: " + Util.GetVersion(true));
                        }

                        json = "";
                    }
                    catch (Exception ex) { Logger.LogException(ex); }
                }
            }
            catch (Exception ex) {
                Logger.LogException(ex);
            }
        }
    }
}

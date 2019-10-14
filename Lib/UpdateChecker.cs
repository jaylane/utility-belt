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
        private static string json = "";

        public static void CheckForUpdate() {
            new Thread(() => {
                Thread.CurrentThread.IsBackground = true;
                FetchGitlabData();
                OnGitlabFetchComplete();
            }).Start();
        }

        public static void FetchGitlabData() {
            // no tls 1.2 in dotnet 3.5???
            try {
                var url = string.Format(@"http://http.haxit.org/ubupdatecheck.php");

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 30000;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse()) {
                    using (Stream stream = response.GetResponseStream()) {
                        using (StreamReader reader = new StreamReader(stream)) {
                            json = reader.ReadToEnd();
                        }
                    }
                }
            }
            catch {}
        }

        private static void OnGitlabFetchComplete() {
            try {
                if (!string.IsNullOrEmpty(json)) {
                    try {
                        var tags = JsonConvert.DeserializeObject<GitLabTagData[]>(json);

                        Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                        Version version = new Version(fvi.FileVersion);

                        foreach (var tag in tags) {
                            try {
                                Version releaseVersion = new Version(tag.tag_name.Replace("release-", ""));
                                if (releaseVersion.CompareTo(version) >= 1) {
                                    var lines = new List<string>(tag.description.Split('\r'));
                                    lines.RemoveAt(0);
                                    lines = lines.Where(s => !string.IsNullOrEmpty(s.Trim())).Distinct().ToList();

                                    var description = string.Join("", lines.ToArray());
                                    Globals.Host.Actions.AddChatText($"[{Globals.PluginName}] Version {releaseVersion.ToString()} is now available! {description}", 3);
                                    Globals.Host.Actions.AddChatText($"Get it here: <Tell:IIDString:{Util.GetChatId()}:openurl|https://gitlab.com/trevis/utilitybelt>https://gitlab.com/trevis/utilitybelt</Tell>", 3);
                                    break;
                                }
                            }
                            catch (Exception ex) { Logger.LogException(ex); }
                        }
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

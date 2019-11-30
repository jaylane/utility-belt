using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using UtilityBelt.Lib;

namespace UBDocsGen {
    #region UB Attribute data
    class SettingInfo {
        public string Name { get; }
        public object DefaultValue { get; }
        public string Summary { get; }

        public SettingInfo(string name, object defaultValue, string summary) {
            Name = name;
            DefaultValue = defaultValue;
            Summary = summary;
        }
    }
    class CommandInfo {
        public string Verb { get; }
        public string ArgsPattern { get; }
        public string Summary { get; }
        public string Usage { get; }
        public Dictionary<string, string> Examples { get; }

        public CommandInfo(string verb, string argsPattern, string summary, string usage, Dictionary<string, string> examples) {
            Verb = verb;
            ArgsPattern = argsPattern;
            Summary = summary;
            Usage = usage;
            Examples = examples;
        }
    }

    class ToolInfo {
        public string Name { get; }
        public string Summary { get; }
        public string Description { get; }
        public List<CommandInfo> Commands { get; }
        public List<SettingInfo> Settings { get; }

        public ToolInfo(string name, string summary, string description, List<CommandInfo> commands, List<SettingInfo> settings) {
            Name = name;
            Summary = summary;
            Description = description;
            Commands = commands;
            Settings = settings;
            Console.WriteLine($"Added tool {Name} with {Commands.Count} commands and {Settings.Count} settings.");
        }
    }
    #endregion

    class Program {
        static Dictionary<string, ToolInfo> AvailableTools = new Dictionary<string, ToolInfo>();
        static string RepoURL = "https://gitlab.com/utilitybelt/utilitybelt.gitlab.io";

        static void Main(string[] args) {
            try {
                var UB = new UtilityBelt.UtilityBeltPlugin();
                InitToolInfo(UB);

                // TODO: fix these paths to be absolute
                GenerateToolSummaries("./Site/layouts/partials/tool-summaries.html");
                GenerateReleases("./Site/content/releases/", "./Site/layouts/partials/");
                GenerateToolPages("./Site/content/docs/tools/");
                GenerateSettingsPage("./Site/content/docs/plugin-settings.md");
                GenerateCommandLinePage("./Site/content/docs/command-line.md");
            }
            catch (Exception ex) {
                LogException(ex);
                Environment.Exit(1);
            }
        }

        private static void InitToolInfo(UtilityBelt.UtilityBeltPlugin UB) {
            var toolProps = UB.GetToolProps();

            foreach (var toolProp in toolProps) {
                var nameAttrs = toolProp.PropertyType.GetCustomAttributes(typeof(NameAttribute), true);

                if (nameAttrs.Length == 1) {
                    var name = ((NameAttribute)nameAttrs[0]).Name;
                    var summary = GetSummary(toolProp);
                    var description = GetFullDescription(toolProp);
                    var commands = GetCommands(toolProp);
                    var settings = GetSettings(toolProp);

                    var info = new ToolInfo(name, summary, description, commands, settings);

                    AvailableTools.Add(name, info);
                }
            }
        }

        #region Attribute getters
        private static string GetSummary(PropertyInfo prop) {
            var summary = "";
            var attrs = prop.PropertyType.GetCustomAttributes(typeof(SummaryAttribute), true);

            if (attrs.Length == 1) {
                summary = ((SummaryAttribute)attrs[0]).Summary;
            }

            return summary;
        }

        private static string GetSummary(MethodInfo methodInfo) {
            var summary = "";
            var attrs = methodInfo.GetCustomAttributes(typeof(SummaryAttribute), true);

            if (attrs.Length == 1) {
                summary = ((SummaryAttribute)attrs[0]).Summary;
            }

            return summary;
        }

        private static string GetUsage(MethodInfo methodInfo) {
            var usage = "";
            var attrs = methodInfo.GetCustomAttributes(typeof(UsageAttribute), true);

            if (attrs.Length == 1) {
                usage = ((UsageAttribute)attrs[0]).Usage;
            }

            return usage;
        }

        private static Dictionary<string, string> GetExamples(MethodInfo methodInfo) {
            var examples = new Dictionary<string, string>();
            var attrs = methodInfo.GetCustomAttributes(typeof(ExampleAttribute), true);

            foreach (var attr in attrs) {
                examples.Add(((ExampleAttribute)attr).Command, ((ExampleAttribute)attr).Description);
            }

            return examples;
        }

        private static string GetFullDescription(PropertyInfo toolProp) {
            var description = "";
            var attrs = toolProp.PropertyType.GetCustomAttributes(typeof(FullDescriptionAttribute), true);

            if (attrs.Length == 1) {
                description = ((FullDescriptionAttribute)attrs[0]).Description;
            }

            return description;
        }
        #endregion

        #region Tool Reflectors
        private static List<CommandInfo> GetCommands(PropertyInfo toolProp) {
            var commandInfos = new List<CommandInfo>();

            foreach (var method in toolProp.PropertyType.GetMethods()) {
                var commandPatternAttrs = method.GetCustomAttributes(typeof(CommandPatternAttribute), true);

                foreach (var attr in commandPatternAttrs) {
                    var verb = ((CommandPatternAttribute)attr).Verb;
                    var argsPattern = ((CommandPatternAttribute)attr).ArgumentsPattern;
                    var summary = GetSummary(method);
                    var usage = GetUsage(method);
                    var examples = GetExamples(method);

                    var commandInfo = new CommandInfo(verb, argsPattern, summary, usage, examples);

                    commandInfos.Add(commandInfo);
                }
            }

            return commandInfos;
        }

        private static List<SettingInfo> GetSettings(PropertyInfo toolProp) {
            var settings = new List<SettingInfo>();

            foreach (var prop in toolProp.PropertyType.GetProperties()) {
                var defaultValueAttrs = prop.GetCustomAttributes(typeof(DefaultValueAttribute), true);

                if (defaultValueAttrs.Length == 1) {
                    var defaultValue = ((DefaultValueAttribute)defaultValueAttrs[0]).Value;
                    var summaryAttrs = prop.GetCustomAttributes(typeof(SummaryAttribute), true);
                    var summary = "";
                    if (summaryAttrs.Length == 1 && defaultValue != null) {
                        summary = ((SummaryAttribute)summaryAttrs[0]).Summary;

                        var settingInfo = new SettingInfo($"{toolProp.Name}.{prop.Name}", defaultValue, summary);

                        settings.Add(settingInfo);
                    }
                }
            }

            return settings;
        }
        #endregion

        #region Template Generators
        private static void GenerateToolSummaries(string v) {
            StringWriter stringWriter = new StringWriter();

            using (HtmlTextWriter writer = new HtmlTextWriter(stringWriter)) {
                foreach (var kp in AvailableTools) {
                    // skip tools without a summary defined
                    if (string.IsNullOrEmpty(kp.Value.Summary)) continue;

                    writer.RenderBeginTag(HtmlTextWriterTag.Div); // Begin div

                    writer.RenderBeginTag(HtmlTextWriterTag.H3); // Begin h3

                    writer.AddAttribute(HtmlTextWriterAttribute.Href, $"docs/tools/{kp.Key.ToLower()}/");
                    writer.RenderBeginTag(HtmlTextWriterTag.A); // Begin 1
                    writer.Write(kp.Key);
                    writer.RenderEndTag(); // End 1

                    writer.RenderEndTag(); // End h3

                    writer.RenderBeginTag(HtmlTextWriterTag.Blockquote); // Begin Blockquote
                    writer.Write(kp.Value.Summary);
                    writer.RenderEndTag(); // End Blockquote

                    writer.RenderEndTag(); // End div
                    writer.Write("\n");
                }
            }

            File.WriteAllText(v, stringWriter.ToString());
        }

        private static Regex changelogTitleRe = new Regex(@"^## (?<version>\d+\.\d+\.\d+) ?\(?(?<releaseDate>[^)]+)\)? ?(?<downloadMarkdown>\[(?<downloadText>[^]]+)\])?(\((?<downloadLink>[^\)]+)\))?");
        private static void GenerateReleases(string releasePagesPath, string partialsPath) {
            var changelog = File.ReadAllLines("./Changelog.md");

            var stringWriter = new StringWriter();
            var title = "";
            var releaseDate = "";
            var downloadLink = "";
            var installerPath = "";
            bool didWriteLatest = false;

            var i = 23;
            foreach (var line in changelog) {
                if (changelogTitleRe.IsMatch(line)) {
                    var match = changelogTitleRe.Match(line);
                    if (stringWriter.ToString().Length > 0) {
                        var path = Path.Combine(releasePagesPath, $"{title}.md");
                        stringWriter.WriteLine($"<div class='download-installer'><a href='{installerPath}' class='btn btn-success btn-lg get-started-btn'>Download UtilityBelt {title} Installer</a></div>");

                        Console.WriteLine($"Writing Release: {title} to {path}");
                        File.WriteAllText(path, stringWriter.ToString());

                        if (releaseDate != match.Groups["releaseDate"].Value) i = 23;

                        if (!didWriteLatest) {
                            didWriteLatest = true;
                            File.WriteAllText(Path.Combine(partialsPath, "download-latest.html"), $"<a href='{installerPath}' class='btn btn-success btn-lg get-started-btn'>Download UtilityBelt {title} Installer</a>");
                        }

                        i--;

                        // how do you clear a stringwriter?
                        stringWriter = new StringWriter();
                        title = "";
                        releaseDate = "";
                    }

                    releaseDate = match.Groups["releaseDate"].Value;
                    downloadLink = match.Groups["downloadLink"].Value;
                    title = $"v{match.Groups["version"].Value}";


                    if (releaseDate == "TBD") {
                        // we want this to throw an exception if it fails, so the build will fail
                        using (StreamReader file = File.OpenText(@"./bin/installer.json"))
                        using (JsonTextReader reader = new JsonTextReader(file)) {
                            JObject o2 = (JObject)JToken.ReadFrom(reader);
                            installerPath = RepoURL + o2["url"];
                        }

                        releaseDate = DateTime.Now.ToString("yyyy-MM-dd");
                    }
                    else {
                        // todo: read from changelog header
                        installerPath = match.Groups["downloadLink"].Value;
                    }

                    // TZ is for hacky ordering of releases on the same day, since thats all we track...
                    WritePageHeader(stringWriter, title, $"{releaseDate}T00:00:{i:D2}Z", "");
                }
                else {
                    if (line.StartsWith("- ")) {
                        stringWriter.WriteLine("* " + line.Substring(2));
                    }
                    else {
                        stringWriter.WriteLine(line);
                    }
                }

                stringWriter.Flush();
            }
        }

        private static void GenerateToolPages(string toolDocsPath) {
            foreach (var kp in AvailableTools) {
                if (string.IsNullOrEmpty(kp.Value.Summary)) continue;

                var outFile = Path.Combine(toolDocsPath, kp.Key + ".md");
                var stringWriter = new StringWriter();

                WritePageHeader(stringWriter, kp.Key, "", "tool: true");
                WriteToolPage(stringWriter, kp);

                Console.WriteLine($"Writing Tool Docs: {outFile}");
                File.WriteAllText(outFile, stringWriter.ToString());
            }
        }

        private static void GenerateSettingsPage(string v) {
            var stringWriter = new StringWriter();

            WritePageHeader(stringWriter, "Settings", "", "");

            stringWriter.WriteLine(@"
All settings take immediate effect on the plugin, and will save to your [character settings](/docs/configfiles/#settings-json) file. You can manipulate settings from the command line using the command below.
            ");

            WriteCommand(stringWriter, AvailableTools["Plugin"].Commands.Find(t => t.Verb == "opt"));

            foreach (var tool in AvailableTools) {
                foreach (var setting in tool.Value.Settings) {
                    WriteSetting(stringWriter, setting);
                }
            }

            File.WriteAllText(v, stringWriter.ToString());
        }

        private static void GenerateCommandLinePage(string v) {
            var stringWriter = new StringWriter();

            WritePageHeader(stringWriter, "Command Line", "", "");

            foreach (var tool in AvailableTools) {
                foreach (var command in tool.Value.Commands) {
                    WriteCommand(stringWriter, command);
                }
            }

            File.WriteAllText(v, stringWriter.ToString());
        }
        #endregion

        #region Template Writers
        private static void WriteSetting(StringWriter stringWriter, SettingInfo setting) {
            var type = setting.DefaultValue.GetType().ToString().Replace("System.", "");

            stringWriter.WriteLine($"#### {setting.Name}");
            stringWriter.WriteLine("> " + setting.Summary + "<br />");
            stringWriter.WriteLine("> **Type:** <span style=\"color:black\">" + type + "</span><br />");
            stringWriter.WriteLine("> **Default Value:** <span style=\"color:black\">" + setting.DefaultValue.ToString() + "</span>");

            stringWriter.WriteLine("");
        }

        private static void WriteCommand(StringWriter stringWriter, CommandInfo command) {
            stringWriter.WriteLine($"#### {HttpUtility.HtmlEncode(command.Usage)}");
            stringWriter.WriteLine("> ");
            stringWriter.WriteLine("> " + command.Summary);

            if (command.Examples.Count > 0) {
                stringWriter.WriteLine("> ");
                stringWriter.WriteLine("> **Examples:**<br />");
                stringWriter.WriteLine("> ");

                foreach (var example in command.Examples) {
                    stringWriter.WriteLine($"> * `{example.Key}` - {example.Value}");
                }
            }

            stringWriter.WriteLine("");
        }

        private static void WritePageHeader(StringWriter stringWriter, string title, string date, string extra) {
            stringWriter.WriteLine("---");
            stringWriter.WriteLine($"title: {title}");
            if (!string.IsNullOrEmpty(date)) {
                stringWriter.WriteLine($"date: {date}");
            }
            if (!string.IsNullOrEmpty(extra)) {
                stringWriter.WriteLine(extra);
            }
            stringWriter.WriteLine("---");
        }

        private static void WriteToolPage(StringWriter stringWriter, KeyValuePair<string, ToolInfo> kp) {
            stringWriter.WriteLine("### Overview");
            stringWriter.WriteLine(kp.Value.Description);
            stringWriter.WriteLine("");
            stringWriter.WriteLine("");

            if (kp.Value.Commands.Count > 0) {
                stringWriter.WriteLine("### Commands");
                stringWriter.WriteLine();
                foreach (var command in kp.Value.Commands) {
                    WriteCommand(stringWriter, command);
                }
            }

            if (kp.Value.Settings.Count > 0) {
                stringWriter.WriteLine("### Settings");
                stringWriter.WriteLine();
                foreach (var setting in kp.Value.Settings) {
                    stringWriter.WriteLine($"#### {setting.Name}");
                    stringWriter.WriteLine("> **Default Value:** " + setting.DefaultValue.ToString() + "<br />");
                    stringWriter.WriteLine("> " + setting.Summary);

                    stringWriter.WriteLine("");
                }
            }
        }
        #endregion

        public static void LogException(Exception ex) {
            try {
                Console.WriteLine("============================================================================");
                Console.WriteLine(DateTime.Now.ToString());
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine("Source: " + ex.Source);
                Console.WriteLine("Stack: " + ex.StackTrace);
                if (ex.InnerException != null) {
                    Console.WriteLine("Inner: " + ex.InnerException.Message);
                    Console.WriteLine("Inner Stack: " + ex.InnerException.StackTrace);
                }
                Console.WriteLine("============================================================================");
                Console.WriteLine("");
            }
            catch {
            }
        }
    }
}

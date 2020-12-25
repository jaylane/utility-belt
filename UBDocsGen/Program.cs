using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using UtilityBelt;
using UtilityBelt.Lib;
using UtilityBelt.Lib.Settings;
using UBLoader.Lib.Settings;

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

    class ExpressionParameterInfo {
        public string Name { get; }
        public Type Type { get; }
        public string Description { get; }
        public object DefaultValue { get; }

        public string FriendlyType {
            get {
                return UtilityBelt.Lib.Expressions.ExpressionVisitor.GetFriendlyType(Type);
            }
        }

        public ExpressionParameterInfo(string name, Type type, string description, object defaultValue) {
            Name = name;
            Type = type;
            Description = description;
            DefaultValue = defaultValue;
        }
    }

    class ExpressionReturnInfo {
        public Type Type { get; }
        public string Description { get; }
        public string FriendlyType {
            get {
                return UtilityBelt.Lib.Expressions.ExpressionVisitor.GetFriendlyType(Type);
            }
        }

        public ExpressionReturnInfo(Type type, string description) {
            Type = type;
            Description = description;
        }
    }

    class ExpressionInfo {
        public string MethodName { get; }
        public List<ExpressionParameterInfo> Parameters { get; }
        public string Summary { get; }
        public Dictionary<string, string> Examples { get; }
        public ExpressionReturnInfo ReturnInfo { get; }

        public ExpressionInfo(string methodName, string summary, List<ExpressionParameterInfo> parameters, Dictionary<string, string> examples, ExpressionReturnInfo returnInfo) {
            MethodName = methodName;
            Parameters = parameters;
            Summary = summary;
            Examples = examples;
            ReturnInfo = returnInfo;
        }
    }

    class ToolInfo {
        public string Name { get; }
        public string Summary { get; }
        public string Description { get; }
        public List<CommandInfo> Commands { get; }
        public List<SettingInfo> Settings { get; }
        public List<ExpressionInfo> ExpressionMethods { get; }

        public ToolInfo(string name, string summary, string description, List<CommandInfo> commands, List<SettingInfo> settings, List<ExpressionInfo> expressions) {
            Name = name;
            Summary = summary;
            Description = description;
            Commands = commands;
            Settings = settings;
            ExpressionMethods = expressions;
            Console.WriteLine($"Added tool {Name} with {Commands.Count} commands, {ExpressionMethods.Count} expression methods, and {Settings.Count} settings.");
        }
    }
    #endregion

    class Program {
        static Dictionary<string, ToolInfo> AvailableTools = new Dictionary<string, ToolInfo>();
        static string RepoURL = "https://gitlab.com/utilitybelt/utilitybelt.gitlab.io";

        public static string AssemblyPath { get; private set; }
        public static string ProjectRoot { get; private set; }
        public static UtilityBeltPlugin UB { get; private set; }
        public static UBLoader.FilterCore Loader { get; private set; }
        public static List<SettingInfo> GlobalSettingInfos { get; private set; }

        static void Main(string[] args) {
            try {
                AssemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                ProjectRoot = Path.GetFullPath(Path.Combine(AssemblyPath, Path.Combine("..", "..")));
                Console.WriteLine($"Project Root: {ProjectRoot}");
                Init();
            }
            catch (Exception ex) {
                LogException(ex);
                Environment.Exit(1);
            }
        }

        private static void Init() {
            Loader = new UBLoader.FilterCore();
            UBLoader.FilterCore.Settings = new Settings(Loader, "");
            UB = new UtilityBeltPlugin();
            UB.Settings = new Settings(UB, "");
            UB.LoadTools();

            InitToolInfo(UB);
            GlobalSettingInfos = GetSettings(Loader, "Global");

            GenerateToolSummaries(Path.Combine(ProjectRoot, "Site/layouts/partials/tool-summaries.html"));
            GenerateReleases(Path.Combine(ProjectRoot, "Site/content/releases/"), Path.Combine(ProjectRoot, "Site/layouts/partials/"));
            GenerateToolPages(Path.Combine(ProjectRoot, "Site/content/docs/tools/"));
            GenerateSettingsPage(Path.Combine(ProjectRoot, "Site/content/docs/plugin-settings.md"));
            GenerateCommandLinePage(Path.Combine(ProjectRoot, "Site/content/docs/command-line.md"));
            GenerateExpressionsPage(Path.Combine(ProjectRoot, "Site/content/docs/expressions.md"));

            MakeFooter();
        }

        private static void MakeFooter() {
            var productVersion = FileVersionInfo.GetVersionInfo(Path.Combine(AssemblyPath, "UtilityBelt.dll")).ProductVersion;
            //0.1.7.bin-cleanup.3bcea54 (2020-11-25 00:13:21)
            var versionRe = new Regex(@"(?<version>\d+\.\d+\.\d+)\.(?<branch>[^\.]+)\.(?<commit>\S+) (?<releaseDate>.*)");
            var matches = versionRe.Match(productVersion);

            StringWriter stringWriter = new StringWriter();
            using (HtmlTextWriter writer = new HtmlTextWriter(stringWriter)) {
                writer.Write(matches.Groups["version"].Value);

                writer.Write(".");

                writer.AddAttribute(HtmlTextWriterAttribute.Href, $"{RepoURL}/-/tree/{matches.Groups["branch"].Value}");
                writer.RenderBeginTag(HtmlTextWriterTag.A); // Begin a
                writer.Write(matches.Groups["branch"].Value);
                writer.RenderEndTag(); // End a

                writer.Write(".");

                writer.AddAttribute(HtmlTextWriterAttribute.Href, $"{RepoURL}/-/commit/{matches.Groups["commit"].Value}");
                writer.RenderBeginTag(HtmlTextWriterTag.A); // Begin a
                writer.Write(matches.Groups["commit"].Value);
                writer.RenderEndTag(); // End a

                writer.Write($" {matches.Groups["releaseDate"].Value}");
            }

            File.WriteAllText(Path.Combine(ProjectRoot, "Site/layouts/partials/footer_custom.html"), stringWriter.ToString());
        }

        private static void InitToolInfo(UtilityBelt.UtilityBeltPlugin UB) {
            var toolProps = UB.GetToolInfos();

            foreach (var toolProp in toolProps) {
                var nameAttrs = toolProp.FieldType.GetCustomAttributes(typeof(NameAttribute), true);

                if (nameAttrs.Length == 1) {
                    var name = ((NameAttribute)nameAttrs[0]).Name;
                    var summary = GetSummary(toolProp);
                    var description = GetFullDescription(toolProp);
                    var commands = GetCommands(toolProp);
                    var settings = GetSettings(toolProp.GetValue(UB), toolProp.Name);
                    var expressions = GetExpressions(toolProp);

                    var info = new ToolInfo(name, summary, description, commands, settings, expressions);

                    AvailableTools.Add(name, info);
                }
            }
        }

        #region Attribute getters
        private static string GetSummary(FieldInfo prop) {
            var summary = "";
            var attrs = prop.FieldType.GetCustomAttributes(typeof(SummaryAttribute), true);

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

        private static ExpressionReturnInfo GetReturnInfo(MethodInfo methodInfo) {
            var attrs = methodInfo.GetCustomAttributes(typeof(ExpressionReturnAttribute), true);

            if (attrs.Length == 1) {
                return new ExpressionReturnInfo(((ExpressionReturnAttribute)attrs[0]).Type, ((ExpressionReturnAttribute)attrs[0]).Description);
            }

            return null;
        }

        private static Dictionary<string, string> GetExamples(MethodInfo methodInfo) {
            var examples = new Dictionary<string, string>();
            var attrs = methodInfo.GetCustomAttributes(typeof(ExampleAttribute), true);

            foreach (var attr in attrs) {
                examples.Add(((ExampleAttribute)attr).Command, ((ExampleAttribute)attr).Description);
            }

            return examples;
        }

        private static List<ExpressionParameterInfo> GetParameters(MethodInfo methodInfo) {
            var parameters = new List<ExpressionParameterInfo>();
            var attrs = methodInfo.GetCustomAttributes(typeof(ExpressionParameterAttribute), true);

            foreach (var attr in attrs) {
                var name = ((ExpressionParameterAttribute)attr).Name;
                var type = ((ExpressionParameterAttribute)attr).Type;
                var description = ((ExpressionParameterAttribute)attr).Description;
                var defaultValue = ((ExpressionParameterAttribute)attr).DefaultValue;
                parameters.Add(new ExpressionParameterInfo(name, type, description, defaultValue));
            }

            return parameters;
        }

        private static string GetFullDescription(FieldInfo toolProp) {
            var description = "";
            var attrs = toolProp.FieldType.GetCustomAttributes(typeof(FullDescriptionAttribute), true);

            if (attrs.Length == 1) {
                description = ((FullDescriptionAttribute)attrs[0]).Description;
            }

            return description;
        }
        #endregion

        #region Tool Reflectors
        private static List<CommandInfo> GetCommands(FieldInfo toolProp) {
            var commandInfos = new List<CommandInfo>();

            foreach (var method in toolProp.FieldType.GetMethods()) {
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

        private static List<SettingInfo> GetSettings(object parent, string history="") {
            var settings = new List<SettingInfo>();
            var children = parent.GetType().GetFields(Settings.BindingFlags)
                .Where(f => typeof(ISetting).IsAssignableFrom(f.FieldType));
            foreach (var field in children) {
                var settingInfo = RegisterSetting(field, parent, history);
                if (settingInfo != null)
                    settings.Add(settingInfo);
            }
            return settings;
        }

        private static SettingInfo RegisterSetting(FieldInfo settingField, object parent, string history="") {
            var summaryAttrs = settingField.GetCustomAttributes(typeof(SummaryAttribute), true);
            var summary = "";
            var setting = ((ISetting)settingField.GetValue(parent));
            var defaultValue = setting.GetDefaultValue();
            var name = string.IsNullOrEmpty(history) ? settingField.Name : $"{history}.{settingField.Name}";

            // these default paths get set to the environment variable on the build machine... so we need to override
            if (name == "Global.PluginStorageDirectory") {
                defaultValue = @"`[Environment.SpecialFolder.Personal]`\Decal Plugins\UtilityBelt";
            }
            else if (name == "Global.DatabaseFile") {
                defaultValue = @"`[Environment.SpecialFolder.Personal]`\Decal Plugins\UtilityBelt\utilitybelt.db";
            }

            if (summaryAttrs.Length == 1 && defaultValue != null && !typeof(ISetting).IsAssignableFrom(defaultValue.GetType())) {
                summary = ((SummaryAttribute)summaryAttrs[0]).Summary;

                var settingInfo = new SettingInfo(name, defaultValue, summary);
                return settingInfo;
            }
            return null;
        }

        private static List<ExpressionInfo> GetExpressions(FieldInfo toolProp) {
            var expresionInfos = new List<ExpressionInfo>();

            foreach (var method in toolProp.FieldType.GetMethods()) {
                var expressionMethodAttrs = method.GetCustomAttributes(typeof(ExpressionMethodAttribute), true);

                foreach (var attr in expressionMethodAttrs) {
                    var name = ((ExpressionMethodAttribute)attr).Name;
                    var parameters = GetParameters(method);
                    var summary = GetSummary(method);
                    var usage = GetUsage(method);
                    var examples = GetExamples(method);
                    var returnInfo = GetReturnInfo(method);

                    var expressionInfo = new ExpressionInfo(name, summary, parameters, examples, returnInfo);
                    expresionInfos.Add(expressionInfo);
                }
            }

            return expresionInfos;
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
            var changelog = File.ReadAllLines(Path.Combine(ProjectRoot, "./Changelog.md"));

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
                        //*
                        using (StreamReader file = File.OpenText(Path.Combine(ProjectRoot, @"bin/installer.json")))
                        using (JsonTextReader reader = new JsonTextReader(file)) {
                            JObject o2 = (JObject)JToken.ReadFrom(reader);
                            installerPath = RepoURL + o2["url"];
                        }
                        //*/
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

            foreach (var setting in GlobalSettingInfos) {
                WriteSetting(stringWriter, setting);
            }

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

        private static void GenerateExpressionsPage(string v) {
            var stringWriter = new StringWriter();

            WritePageHeader(stringWriter, "Expressions", "", "");

            stringWriter.WriteLine(File.ReadAllText(Path.Combine(ProjectRoot, @"Site/content/docs/_expressions_summary.md")));

            foreach (var tool in AvailableTools) {
                foreach (var method in tool.Value.ExpressionMethods) {
                    WriteExpressionMethod(stringWriter, method);
                }
            }

            File.WriteAllText(v, stringWriter.ToString());
        }
        #endregion

        #region Template Writers
        private static void WriteSetting(StringWriter stringWriter, SettingInfo setting) {
            var type = setting.DefaultValue.GetType().ToString().Replace("System.", "");
            var defaultValue = setting.DefaultValue.ToString();

            if (typeof(IList).IsAssignableFrom(setting.DefaultValue.GetType())) {
                type = "List";
                defaultValue = ((IList)setting.DefaultValue).Count == 0 ? "Empty List" : "";
                foreach (var item in ((IList)setting.DefaultValue)) {
                    defaultValue += $"{item}, ";
                }
            }

            stringWriter.WriteLine($"#### {setting.Name}");
            stringWriter.WriteLine("> " + setting.Summary + "<br />");
            stringWriter.WriteLine("> **Type:** <span style=\"color:black\">" + type + "</span><br />");
            stringWriter.WriteLine("> **Default Value:** <span style=\"color:black\">" + defaultValue + "</span>");

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
                    stringWriter.WriteLine($">* &nbsp;```{example.Key}``` - {example.Value}");
                }
                stringWriter.WriteLine("> ");
            }

            stringWriter.WriteLine("");
        }

        private static void WriteExpressionMethod(StringWriter stringWriter, ExpressionInfo method) {
            var parameters = new List<string>();
            foreach (var p in method.Parameters) {
                parameters.Add($"<span style=\"color:#27436F\">{p.FriendlyType} {p.Name}{(p.DefaultValue != null ? $"={p.DefaultValue}" : "")}</span>");
            }

            stringWriter.WriteLine($"#### {HttpUtility.HtmlEncode(method.MethodName)}[{string.Join(", ", parameters.ToArray())}]");
            stringWriter.WriteLine("> ");
            stringWriter.WriteLine("> " + method.Summary);

            if (method.Parameters.Count > 0) {
                stringWriter.WriteLine("> ");
                stringWriter.WriteLine("> **Parameters:**<br />");
                stringWriter.WriteLine("> ");

                var i = 0;
                foreach (var p in method.Parameters) {
                    stringWriter.WriteLine($"> * Param #{i++}: {p.Name} ({p.FriendlyType}) {(p.DefaultValue != null ? $"(Default: {p.DefaultValue})" : "")} - {p.Description}");
                }
            }

            stringWriter.WriteLine("> ");
            stringWriter.WriteLine($"> **Returns:** {method.ReturnInfo.FriendlyType} - {method.ReturnInfo.Description}");
            stringWriter.WriteLine("> ");

            if (method.Examples.Count > 0) {
                stringWriter.WriteLine("> ");
                stringWriter.WriteLine("> **Examples:**<br />");
                stringWriter.WriteLine("> ");

                foreach (var example in method.Examples) {
                    stringWriter.WriteLine($"> * &nbsp;```{example.Key}``` - {example.Value}");
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

            if (kp.Value.ExpressionMethods.Count > 0) {
                stringWriter.WriteLine("### Expression Methods");
                stringWriter.WriteLine();
                foreach (var expressionMethod in kp.Value.ExpressionMethods) {
                    WriteExpressionMethod(stringWriter, expressionMethod);
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

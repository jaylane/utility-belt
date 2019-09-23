using UtilityBelt;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Xml;
using System.Reflection;

// https://github.com/Mag-nus/Mag-Plugins/blob/master/Shared/Settings/SettingsFile.cs

namespace Mag.Shared.Settings {
    static class SettingsFile {
        internal static XmlDocument XmlDocument = new XmlDocument();

        static string _documentPath;

        static string _rootNodeName = "Settings";

        static SettingsFile() {
            ReloadXmlDocument();
        }

        public static void Init(string filePath, string rootNode = "Settings") {
            XmlDocument = new XmlDocument();
            _documentPath = filePath;

            _rootNodeName = rootNode;

            ReloadXmlDocument();
        }

        public static void ReloadXmlDocument() {
            try {

                if (!String.IsNullOrEmpty(_documentPath) && File.Exists(_documentPath))
                    XmlDocument.Load(_documentPath);
                else if (XmlDocument.InnerText.Length > 0)
                    return;
                else
                    XmlDocument.LoadXml("<" + _rootNodeName + "></" + _rootNodeName + ">");
            }
            catch (Exception ex) {
                Logger.LogException(ex);

                XmlDocument.LoadXml("<" + _rootNodeName + "></" + _rootNodeName + ">");
            }
        }

        public static void SaveXmlDocument() {
            Util.WriteToDebugLog("Saved config.xml");
            XmlDocument.Save(_documentPath);
        }

        public static T GetSetting<T>(string xPath, T defaultValue = default(T), string description = "") {
            try {
                if (typeof(T) == typeof(List<string>)) {
                    return GetSetting<T>(xPath, defaultValue as List<string>, description);
                }

                if (typeof(T) == typeof(List<int>)) {
                    return GetSetting<T>(xPath, defaultValue as List<int>, description);
                }

                XmlNode xmlNode = XmlDocument.SelectSingleNode(_rootNodeName + "/" + xPath);

                if (xmlNode != null) {
                    TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));

                    if (converter.CanConvertFrom(typeof(string)))
                        return (T)converter.ConvertFromString(xmlNode.InnerText);
                }

                Util.WriteToDebugLog(String.Format("Creating {0} because it doesn't exist", xPath));

                // save default setting to xml if it doesn't exist
                PutSetting<T>(xPath, defaultValue, description, false);

                return defaultValue;
            }
            catch (Exception e) { Logger.LogException(e); return defaultValue; }
        }

        public static T GetSetting<T>(string xPath, List<string> defaultValue, string description = "") {
            try {
                var xpathParts = new List<string>(xPath.Split('/'));
                XmlNode xmlNode = XmlDocument.SelectSingleNode(_rootNodeName + "/" + String.Join("/", xpathParts.GetRange(0, xpathParts.Count - 1).ToArray()));
                var lastPart = xpathParts[xpathParts.Count - 1];

                List<string> returnVals = new List<string>();

                if (xmlNode != null) {
                    foreach (XmlNode childNode in xmlNode.ChildNodes) {
                        returnVals.Add(childNode.InnerText);
                    }

                    return (T)Convert.ChangeType(returnVals, typeof(T));
                }

                Util.WriteToDebugLog(String.Format("Creating {0} because it doesn't exist", xPath));

                // save default setting to xml if it doesn't exist
                PutSetting(xPath, defaultValue, description, false);

                return (T)Convert.ChangeType(returnVals, typeof(T));
            }
            catch (Exception e) { Logger.LogException(e); return (T)Convert.ChangeType(defaultValue, typeof(T)); }
        }

        public static T GetSetting<T>(string xPath, List<int> defaultValue, string description = "") {
            try {
                var xpathParts = new List<string>(xPath.Split('/'));
                Util.WriteToDebugLog(_rootNodeName + "/" + String.Join("/", xpathParts.GetRange(0, xpathParts.Count - 1).ToArray()));
                XmlNode xmlNode = XmlDocument.SelectSingleNode(_rootNodeName + "/" + String.Join("/", xpathParts.GetRange(0, xpathParts.Count - 1).ToArray()));
                var lastPart = xpathParts[xpathParts.Count - 1];

                List<int> returnVals = new List<int>();

                if (xmlNode != null) {
                    foreach (XmlNode childNode in xmlNode.ChildNodes) {
                        if (int.TryParse(childNode.InnerText, out int value)) {
                            returnVals.Add(value);
                        }
                        else {
                            Util.WriteToChat("can't parse item: " + childNode.InnerText);
                        }
                    }

                    return (T)Convert.ChangeType(returnVals, typeof(T));
                }

                Util.WriteToDebugLog(String.Format("Creating {0} because it doesn't exist", xPath));

                // save default setting to xml if it doesn't exist
                PutSetting(xPath, defaultValue, description, false);

                return (T)Convert.ChangeType(returnVals, typeof(T));
            }
            catch (Exception e) { Logger.LogException(e); return (T)Convert.ChangeType(defaultValue, typeof(T)); }
        }

        public static void PutSetting(string xPath, List<int> values, string helpText, bool doSave) {
            try {
                // Before we save a setting, we reload the document to make sure we don't overwrite settings saved from another session.
                ReloadXmlDocument();

                var xpathParts = new List<string>(xPath.Split('/'));
                XmlNode xmlNode = XmlDocument.SelectSingleNode(_rootNodeName + "/" + String.Join("/", xpathParts.GetRange(0, xpathParts.Count - 1).ToArray()));

                if (xmlNode == null) {
                    xmlNode = createMissingNode(_rootNodeName + "/" + String.Join("/", xpathParts.GetRange(0, xpathParts.Count - 1).ToArray()));

                    xmlNode.ParentNode.InsertBefore(XmlDocument.CreateComment(" " + helpText + " "), xmlNode);
                }

                xmlNode.InnerText = "";

                foreach (var value in values) {
                        var child = XmlDocument.CreateElement(xpathParts[xpathParts.Count - 1]);
                        child.InnerText = value.ToString();

                        xmlNode.AppendChild(child);
                }

                if (doSave) {
                    SaveXmlDocument();
                }
            }
            catch (Exception e) { Logger.LogException(e); }
        }

        public static void PutSetting(string xPath, List<string> values, string helpText, bool doSave) {
            try {
                // Before we save a setting, we reload the document to make sure we don't overwrite settings saved from another session.
                ReloadXmlDocument();

                var xpathParts = new List<string>(xPath.Split('/'));

                XmlNode xmlNode = XmlDocument.SelectSingleNode(_rootNodeName + "/" + String.Join("/", xpathParts.GetRange(0, xpathParts.Count - 1).ToArray()));

                if (xmlNode == null) {
                    xmlNode = createMissingNode(_rootNodeName + "/" + String.Join("/", xpathParts.GetRange(0, xpathParts.Count - 1).ToArray()));

                    xmlNode.ParentNode.InsertBefore(XmlDocument.CreateComment(" " + helpText + " "), xmlNode);
                }

                xmlNode.InnerText = "";
                foreach (var value in values) {
                            if (value != null) {
                                var child = XmlDocument.CreateElement(xpathParts[xpathParts.Count - 1]);
                                child.InnerText = value;

                                xmlNode.AppendChild(child);
                            }
                }

                if (doSave) {
                    SaveXmlDocument();
                }
            }
            catch (Exception e) { Logger.LogException(e); }
        }

        public static void PutSetting<T>(string xPath, T value, string helpText, bool doSave) {
            try {
                if (typeof(T) == typeof(List<string>)) {
                    PutSetting(xPath, value as List<string>, helpText, doSave);
                    return;
                }
                if (typeof(T) == typeof(List<int>)) {
                    PutSetting(xPath, value as List<int>, helpText, doSave);
                    return;
                }

                // Before we save a setting, we reload the document to make sure we don't overwrite settings saved from another session.
                ReloadXmlDocument();

                XmlNode xmlNode = XmlDocument.SelectSingleNode(_rootNodeName + "/" + xPath);

                if (xmlNode == null) {
                    xmlNode = createMissingNode(_rootNodeName + "/" + xPath);

                    xmlNode.ParentNode.InsertBefore(XmlDocument.CreateComment(" " + helpText + " "), xmlNode);
                }

                TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));

                if (converter.CanConvertTo(typeof(string))) {
                    string result = converter.ConvertToString(value);

                    if (result != null) {
                        xmlNode.InnerText = result;

                        if (doSave) {
                            SaveXmlDocument();
                        }
                    }
                }
            }
            catch (Exception e) { Logger.LogException(e); }
        }

        static XmlNode createMissingNode(string xPath) {
            string[] xPathSections = xPath.Split('/');

            string currentXPath = "";

            XmlNode currentNode = XmlDocument.SelectSingleNode(_rootNodeName);

            foreach (string xPathSection in xPathSections) {
                currentXPath += xPathSection;

                XmlNode testNode = XmlDocument.SelectSingleNode(currentXPath);

                if (testNode == null) {
                    if (currentNode != null)
                        currentNode.InnerXml += "<" + xPathSection + "></" + xPathSection + ">";
                }

                currentNode = XmlDocument.SelectSingleNode(currentXPath);

                currentXPath += "/";
            }

            return currentNode;
        }

        public static IList<string> GetChilderenInnerTexts(string xPath) {
            XmlNode xmlNode = XmlDocument.SelectSingleNode(_rootNodeName + "/" + xPath);

            Collection<string> collection = new Collection<string>();

            if (xmlNode != null) {
                foreach (XmlNode childNode in xmlNode.ChildNodes)
                    collection.Add(childNode.InnerText);
            }

            return collection;
        }

        public static void SetNodeChilderen(string xPath, string childNodeName, IList<string> innerTexts) {
            // Before we save a setting, we reload the document to make sure we don't overwrite settings saved from another session.
            ReloadXmlDocument();

            XmlNode parentNode = XmlDocument.SelectSingleNode(_rootNodeName + "/" + xPath);

            if (parentNode == null) {
                if (innerTexts.Count == 0)
                    return;

                parentNode = createMissingNode(_rootNodeName + "/" + xPath);
            }

            parentNode.RemoveAll();

            if (innerTexts.Count == 0) {
                XmlDocument.Save(_documentPath);
                return;
            }

            foreach (string innerText in innerTexts) {
                XmlNode childNode = parentNode.AppendChild(XmlDocument.CreateElement(childNodeName));

                childNode.InnerText = innerText;
            }

            XmlDocument.Save(_documentPath);
        }

        public static XmlNode GetNode(string xPath, bool createIfNull = false) {
            var node = XmlDocument.SelectSingleNode(_rootNodeName + "/" + xPath);

            if (node == null && createIfNull)
                node = createMissingNode(_rootNodeName + "/" + xPath);

            return node;
        }

        public static void SetNodeChilderen(string xPath, string childNodeName, Collection<Dictionary<string, string>> childNodeAttributes) {
            // Before we save a setting, we reload the document to make sure we don't overwrite settings saved from another session.
            ReloadXmlDocument();

            XmlNode parentNode = XmlDocument.SelectSingleNode(_rootNodeName + "/" + xPath);

            if (parentNode == null)
                parentNode = createMissingNode(_rootNodeName + "/" + xPath);

            if (parentNode.HasChildNodes)
                parentNode.RemoveAll();

            foreach (Dictionary<string, string> dictionary in childNodeAttributes) {
                XmlNode childNode = parentNode.AppendChild(XmlDocument.CreateElement(childNodeName));

                foreach (KeyValuePair<string, string> pair in dictionary) {
                    XmlAttribute attribute = XmlDocument.CreateAttribute(pair.Key);
                    attribute.Value = pair.Value;

                    if (childNode.Attributes != null)
                        childNode.Attributes.Append(attribute);
                }
            }

            XmlDocument.Save(_documentPath);
        }
    }
}

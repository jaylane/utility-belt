using System;
using Decal.Interop.Core;
using System.Runtime.InteropServices;
using System.IO;
using UBService.Views;
using UBService.Lib;
using Decal.Adapter;
using UBService.Lib.Settings;
using UBService.Views.SettingsEditor;
using System.Reflection;
using System.Diagnostics;

namespace UBService {
    /// <summary>
    /// UB Service
    /// </summary>
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("8adc5729-db1a-4e28-9475-c4eafae1e6e7")]
    [ProgId("UBService")]
    [ComVisible(true)]
    [ComDefaultInterface(typeof(IDecalService))]
    public sealed class UBService : MarshalByRefObject, IDecalService, IDecalRender, IDecalWindowsMessageSink {
        internal static DecalCore iDecal;
        internal static bool didInit = false;
        internal static Settings Settings { get; set; }
        internal static Settings ViewsSettings { get; set; }
        internal static Settings CharacterSettings { get; set; }
        internal static string AssemblyDirectory => Path.GetDirectoryName(Assembly.GetAssembly(typeof(UBService)).Location);

        public static bool IsInGame { get; internal set; }

        #region Service Settings
        public class ServiceSettings : ISetting {
            [Summary("Enable service debug logs")]
            public Setting<bool> Debug = new Setting<bool>(false);
        }
        public static ServiceSettings Service = new ServiceSettings();
        #endregion // Service Settings

        public static HudManager Huds = null;

        unsafe void IDecalService.Initialize(DecalCore pDecal) {
            WriteLog($"IDecalService.Initialize");
            try {
                iDecal = pDecal;
                Huds = new HudManager();

                Settings = new Settings(this, System.IO.Path.Combine(AssemblyDirectory, "ubservice.settings.json"), (t) => {
                    return (t.SettingType == SettingType.Profile);
                });
                Settings.Load();

                if (!Directory.Exists(Huds.profilesDir))
                    Directory.CreateDirectory(Huds.profilesDir);

                ViewsSettings = new Settings(this, Huds.CurrentProfilePath, (t) => {
                    return (t.SettingType == SettingType.Views);
                });
                ViewsSettings.Load();

                Huds.Profile.Changed += Profile_Changed;
            }
            catch (Exception ex) { LogException(ex); }
        }

        private void Profile_Changed(object sender, SettingChangedEventArgs e) {
            ViewsSettings.SettingsPath = Huds.CurrentProfilePath;
        }

        /// <summary>
        /// Handle window messages
        /// </summary>
        /// <param name="HWND"></param>
        /// <param name="uMsg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        unsafe public bool WindowMessage(int HWND, short uMsg, int wParam, int lParam) {
            var eat = false;
            try {
                eat = Huds.WindowMessage(HWND, uMsg, wParam, lParam);
            }
            catch (Exception ex) { LogException(ex); }
            return eat;
        }

        void IDecalService.BeforePlugins() {
            WriteLog($"BeforePlugins: {Huds.CurrentProfilePath}");
            CoreManager.Current.CharacterFilter.Login += CharacterFilter_Login;
        }

        private void CharacterFilter_Login(object sender, Decal.Adapter.Wrappers.LoginEventArgs e) {
            IsInGame = true;
            WriteLog($"CharacterFilter_Login: {Huds.CurrentProfilePath}");

            var charSettingsPath = Path.Combine(Huds.profilesDir, $"__{CoreManager.Current.CharacterFilter.AccountName}_{CoreManager.Current.CharacterFilter.Name}.json");
            CharacterSettings = new Settings(this, charSettingsPath, (t) => {
                return (t.SettingType == SettingType.CharacterSettings);
            });
            CharacterSettings.Load();

            ViewsSettings.SettingsPath = Huds.CurrentProfilePath;
            CoreManager.Current.CharacterFilter.Login -= CharacterFilter_Login;
        }

        void IDecalService.AfterPlugins() {
            IsInGame = false;
            WriteLog($"AfterPlugins: {Huds.CurrentProfilePath}");
            ViewsSettings.SettingsPath = Huds.CurrentProfilePath;
            if (CharacterSettings != null && CharacterSettings.NeedsSave)
                CharacterSettings.Save();
            CharacterSettings = null;
        }

        void IDecalService.Terminate() {
            Huds.Profile.Changed -= Profile_Changed;
            if (Settings != null && Settings.NeedsSave)
                Settings.Save();
            if (ViewsSettings != null && ViewsSettings.NeedsSave)
                ViewsSettings.Save();
            WriteLog($"Terminate");
        }


#pragma warning disable 1591
        public unsafe void ChangeDirectX() {
            WriteLog($"ChangeDirectX");
            try {
                if (!didInit) {
                    Huds.Init();
                }
            }
            catch (Exception ex) {
                LogException(ex);
            }
        }

#pragma warning disable 1591
        public void ChangeHWND() {
            WriteLog($"ChangeHWND");
        }

#pragma warning disable 1591
        public void PostReset() {
            WriteLog($"PostReset");
            try {
                Huds.PostReset();
            }
            catch (Exception ex) {
                LogException(ex);
            }
        }

#pragma warning disable 1591
        public void PreReset() {
            WriteLog($"PreReset");
            try {
                Huds.PreReset();
            }
            catch (Exception ex) {
                LogException(ex);
            }
        }

#pragma warning disable 1591
        public void Render2D() {
            try {
                Huds.DoRender();
            }
            catch (Exception ex) {
                LogException(ex);
            }
        }

#pragma warning disable 1591
        public void Render3D() {
            var timers = Timer.RunningTimers.ToArray();
            foreach (var timer in timers) {
                timer.TryTick();
            }
        }

        internal static void LogException(Exception ex) {
            WriteLog($"Exception : {ex}");
        }

        internal static void WriteLog(string text) {
            if (UBService.Service.Debug)
                File.AppendAllText(@"ubservice.exceptions.txt", text + "\n");
        }

        internal static void LogError(string v) {
            WriteLog(v);
        }
    }
}

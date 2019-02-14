using System;

using Decal.Adapter;
using Decal.Adapter.Wrappers;
using MyClasses.MetaViewWrappers;
using UtilityBelt.Tools;
using UtilityBelt.Views;

namespace UtilityBelt
{

    //Attaches events from core
	[WireUpBaseEvents]
    
	[FriendlyName("UtilityBelt")]
	public class PluginCore : PluginBase {
        private AutoVendor autoVendor;
        private AutoSalvage autoSalvage;
        private DateTime lastThought = DateTime.MinValue;

        /// <summary>
        /// This is called when the plugin is started up. This happens only once.
        /// </summary>
        protected override void Startup() {
			try {
				Globals.Init("UtilityBelt", Host, Core);
            }
			catch (Exception ex) { Util.LogException(ex); }
		}

		/// <summary>
		/// This is called when the plugin is shut down. This happens only once.
		/// </summary>
		protected override void Shutdown() {
			try {

			}
			catch (Exception ex) { Util.LogException(ex); }
		}

		[BaseEvent("LoginComplete", "CharacterFilter")]
		private void CharacterFilter_LoginComplete(object sender, EventArgs e)
		{
			try {
                string configFilePath = Util.GetCharacterDirectory() + "config.xml";

                Mag.Shared.Settings.SettingsFile.Init(configFilePath, Globals.PluginName);

                Util.CreateDataDirectories();

                Globals.Config = new Config();
                Globals.View = new MainView();

                autoVendor = new AutoVendor();
                autoSalvage = new AutoSalvage();

                Globals.Core.RenderFrame += Core_RenderFrame;
            }
			catch (Exception ex) { Util.LogException(ex); }
		}

        private void Core_RenderFrame(object sender, EventArgs e) {
            try {
                //if (DateTime.UtcNow - lastThought >= TimeSpan.FromMilliseconds(1000/60)) {
                    lastThought = DateTime.UtcNow;

                    if (autoSalvage != null) autoSalvage.Think();
                    if (autoVendor != null) autoVendor.Think();
                //}
            }
            catch (Exception ex) { Util.LogException(ex); }
        }

        [BaseEvent("Logoff", "CharacterFilter")]
		private void CharacterFilter_Logoff(object sender, Decal.Adapter.Wrappers.LogoffEventArgs e)
		{
			try {
                Globals.Core.RenderFrame -= Core_RenderFrame;

                if (autoVendor != null) autoVendor.Dispose();
                if (autoSalvage != null) autoSalvage.Dispose();
                if (Globals.View != null) Globals.View.Dispose();
                if (Globals.Config != null) Globals.Config.Dispose();
            }
			catch (Exception ex) { Util.LogException(ex); }
		}
	}
}

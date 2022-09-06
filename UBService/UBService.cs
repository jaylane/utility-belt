using System;
using Decal.Interop.Core;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.DirectX.Direct3D;
using Microsoft.DirectX;
using System.Drawing;
using System.Collections.Generic;
using Microsoft.DirectX.PrivateImplementationDetails;
using System.Linq;
using Decal.Adapter;
using static System.Net.Mime.MediaTypeNames;
using System.Threading;

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
        internal static bool DEBUG = false;

        unsafe void IDecalService.Initialize(DecalCore pDecal) {
            //WriteLog($"IDecalService.Initialize");
            try {
                iDecal = pDecal;
                iDecal.InitializeComplete += iDecal_InitializeComplete;
            }
            catch (Exception ex) { LogException(ex); }
        }

        unsafe private void iDecal_InitializeComplete(eDecalComponentType type) {
            //WriteLog($"iDecal_InitializeComplete: {type} {(CoreManager.Current == null ? "null" : "not null")}");
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
                eat = HudManager.WindowMessage(HWND, uMsg, wParam, lParam);
            }
            catch (Exception ex) { LogException(ex); }
            return eat;
        }

        void IDecalService.AfterPlugins() {
            WriteLog($"AfterPlugins");
        }

        void IDecalService.BeforePlugins() {
            WriteLog($"BeforePlugins");
        }

        void IDecalService.Terminate() {
            WriteLog($"Terminate");
        }


#pragma warning disable 1591
        public unsafe void ChangeDirectX() {
            WriteLog($"ChangeDirectX");
            try {
                HudManager.ChangeDirectX();
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
                HudManager.PostReset();
            }
            catch (Exception ex) {
                LogException(ex);
            }
        }

#pragma warning disable 1591
        public void PreReset() {
            WriteLog($"PreReset");
            try {
                HudManager.PreReset();
            }
            catch (Exception ex) {
                LogException(ex);
            }
        }

#pragma warning disable 1591
        public void Render2D() {
            try {
                HudManager.DoRender();
            }
            catch (Exception ex) {
                LogException(ex);
            }
        }

#pragma warning disable 1591
        public void Render3D() {

        }

        internal static void LogException(Exception ex) {
            WriteLog($"Exception : {ex}");
        }

        internal static void WriteLog(string text) {
            //File.AppendAllText(@"ubservice.exceptions.txt", text + "\n");
            if (DEBUG)
                File.AppendAllText(@"C:\Users\trevis\Documents\Decal Plugins\UtilityBelt\exceptions.txt", text + "\n");
        }
    }
}

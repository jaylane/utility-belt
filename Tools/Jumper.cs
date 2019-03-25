using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityBelt.MagTools.Shared;

namespace UtilityBelt.Tools {
    class Jumper : IDisposable {
        private bool disposed = false;

        public Jumper() {
        Globals.Core.CommandLineText += Current_CommandLineText;
            }

        private static byte ScanCode(char Char) {
            switch (char.ToLower(Char)) {
                case 'a': return 0x1E;
                case 'b': return 0x30;
                case 'c': return 0x2E;
                case 'd': return 0x20;
                case 'e': return 0x12;
                case 'f': return 0x21;
                case 'g': return 0x22;
                case 'h': return 0x23;
                case 'i': return 0x17;
                case 'j': return 0x24;
                case 'k': return 0x25;
                case 'l': return 0x26;
                case 'm': return 0x32;
                case 'n': return 0x31;
                case 'o': return 0x18;
                case 'p': return 0x19;
                case 'q': return 0x10;
                case 'r': return 0x13;
                case 's': return 0x1F;
                case 't': return 0x14;
                case 'u': return 0x16;
                case 'v': return 0x2F;
                case 'w': return 0x11;
                case 'x': return 0x2D;
                case 'y': return 0x15;
                case 'z': return 0x2C;
                case '/': return 0x35;
                case ' ': return 0x39;
            }
            return 0;
        }

        private static byte CharCode(char Char) {
            switch (char.ToLower(Char)) {
                case 'a': return 0x41;
                case 'b': return 0x42;
                case 'c': return 0x43;
                case 'd': return 0x44;
                case 'e': return 0x45;
                case 'f': return 0x46;
                case 'g': return 0x47;
                case 'h': return 0x48;
                case 'i': return 0x49;
                case 'j': return 0x4A;
                case 'k': return 0x4B;
                case 'l': return 0x4C;
                case 'm': return 0x4D;
                case 'n': return 0x4E;
                case 'o': return 0x4F;
                case 'p': return 0x50;
                case 'q': return 0x51;
                case 'r': return 0x52;
                case 's': return 0x53;
                case 't': return 0x54;
                case 'u': return 0x55;
                case 'v': return 0x56;
                case 'w': return 0x57;
                case 'x': return 0x58;
                case 'y': return 0x59;
                case 'z': return 0x5A;
                case '/': return 0xBF;
                case ' ': return 0x20;
            }
            return 0x20;
        }


        void Current_CommandLineText(object sender, ChatParserInterceptEventArgs e) {
            try {
                if (e.Text.StartsWith("/mt face ")) {
                    if (e.Text.Length > 9) {
                        int heading;
                        if (!int.TryParse(e.Text.Substring(9, e.Text.Length - 9), out heading))
                            return;
                        
                        CoreManager.Current.Actions.FaceHeading(heading, true);
                    }

                    return;
                }

                if (e.Text.StartsWith("/ub jump") || e.Text.StartsWith("/ub sjump")) {
                    int msToHoldDown = 0;
                    bool addShift = e.Text.Contains("sjump");
                    bool addW = e.Text.Contains("jumpw");
                    bool addZ = e.Text.Contains("jumpz");
                    bool addX = e.Text.Contains("jumpx");
                    bool addC = e.Text.Contains("jumpc");
                    e.Eat = true;

                    string[] split = e.Text.Split(' ');
                    if (split.Length == 3)
                        int.TryParse(split[2], out msToHoldDown);

                    PostMessageTools.SendSpace(msToHoldDown, addShift, addW, addZ, addX, addC);

                    return;
                }
                if (e.Text.StartsWith("/ub testjump")) {
                    e.Eat = true;
                    if (!CoreManager.Current.Actions.ChatState) {
                        //User32.PostMessage(CoreManager.Current.Decal.Hwnd, User32.WM_KEYDOWN, (IntPtr)CharCode((char)Globals.Host.GetKeyboardMapping("MovementForward")), (UIntPtr)(0xC0000001 + ScanCode((char)Globals.Host.GetKeyboardMapping("1")) * 0x10000));
                        User32.PostMessage(CoreManager.Current.Decal.Hwnd, User32.WM_KEYDOWN, (IntPtr)(CharCode((char)Globals.Host.GetKeyboardMapping("MovementForward"))), (UIntPtr)(0x00000001 + ScanCode((char)Globals.Host.GetKeyboardMapping("MovementForward")) * 0x10000));
                        //PostMessageTools.SendMovement((char)Globals.Host.GetKeyboardMapping("MovementWalkMode"), 500);
                        //PostMessageTools.SendMovement((char)Globals.Host.GetKeyboardMapping("MovementJump"), 500);
                        //PostMessageTools.SendMovement((char)Globals.Host.GetKeyboardMapping("MovementForward"), 500);
                    }
                    //User32.PostMessage(CoreManager.Current.Decal.Hwnd, User32.WM_KEYDOWN, (IntPtr)CharCode((char)Globals.Host.GetKeyboardMapping("MovementTurnLeft")), (UIntPtr)(0xC0000001 + ScanCode((char)Globals.Host.GetKeyboardMapping("1")) * 0x10000));
                    //User32.PostMessage(CoreManager.Current.Decal.Hwnd, User32.WM_KEYUP, (IntPtr)CharCode((char)Globals.Host.GetKeyboardMapping("MovementTurnLeft")), (UIntPtr)(0xC0000001 + ScanCode((char)Globals.Host.GetKeyboardMapping("1")) * 0x10000));
                    Util.WriteToChat("this is a test - turning left");
                }
            } catch (Exception ex) { Logger.LogException(ex); }
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    Globals.Core.CommandLineText -= Current_CommandLineText;
                }
                disposed = true;
            }
        }
    }
}

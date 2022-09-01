using Decal.Adapter;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace UtilityBelt.Lib {
    class UBHudManager : IDisposable {
        public List<UBHud> Huds = new List<UBHud>();
        public Point LastMousePos = Point.Empty;
        public bool IsHoldingControl = false;
        public unsafe double IsHoldingControlTS = *AcClient.Timer.cur_time;

        public bool IsLMouse = false;
        public unsafe double IsLMouseTS = *AcClient.Timer.cur_time;
        public Point IsLMousePos = Point.Empty;

        private bool isHandlingEvents = false;

        public Dictionary<UBHud.WinKeys, double> Keys = new Dictionary<UBHud.WinKeys, double>();

        public UBHud CreateHud(int x, int y, int width, int height) {
            try {
                var hud = new UBHud(x, y, width, height, this);
                Huds.Add(hud);
                Huds.Sort((h1, h2) => {
                    return h1.ZPriority.CompareTo(h2.ZPriority);
                });
                EnsureEventHandlers();
                return hud;

            }
            catch (Exception ex) { Logger.LogException(ex); }
            return null;
        }
        public void DestroyHud(UBHud hud) {
            if (Huds.Contains(hud)) {
                Huds.Remove(hud);
            }
            if (MouseOverHud == hud) {
                MouseOverHud = null;
                MouseOverElement = null;
            }

        }

        private UBHud MouseOverHud = null;
        private UBHud.Element MouseOverElement = null;
        private unsafe void FindElementAt(Point pos) {
            if (MouseOverHud == null || !MouseOverHud.IsMouseOver(pos)) {
                // previous hud is no longer valid
                if (MouseOverHud != null) { MouseOverHud.MouseBlur(); MouseOverHud = null; }
                foreach (var h in Huds) {
                    if (h != null && h.IsMouseOver(pos)) {
                        MouseOverHud = h;
                        //Logger.WriteToChat($"Found Hud at {MouseOverHud.BBox}");
                        MouseOverHud.Render();
                        MouseOverHud.MouseFocus();
                        break;
                    }
                }
            }
            if (MouseOverElement == null || !MouseOverElement.IsMouseOver(pos)) {
                if (MouseOverElement != null) { MouseOverElement.MouseBlur(); MouseOverElement = null; }
                if (MouseOverHud != null) {
                    foreach (var h in MouseOverHud.Elements) {
                        if (h != null && h.IsMouseOver(pos)) {
                            MouseOverElement = h;
                            //Logger.WriteToChat($"Found Element at {MouseOverElement.BBox} idx {i}");
                            MouseOverElement.MouseFocus();
                            break;
                        }
                    }
                }
            }
        }
        private unsafe void EnsureEventHandlers() {
            if (isHandlingEvents)
                return;

            isHandlingEvents = true;

            foreach (UBHud.WinKeys i in Enum.GetValues(typeof(UBHud.WinKeys))) {
                Keys.Add(i, 0);
            }

            CoreManager.Current.WindowMessage += Current_WindowMessage;
            CoreManager.Current.RegionChange3D += Current_RegionChange3D;
        }

        private void Current_RegionChange3D(object sender, RegionChange3DEventArgs e) {
            try {
                foreach (var hud in Huds)
                    hud.ReMake();


            }
            catch (Exception ex) { Logger.LogException(ex); }
        }

        private unsafe void Event_MouseMove(Point pos) {
            FindElementAt(pos);
        }
        private unsafe bool Event_LMouse_Up(Point pos, double holdTime) {
            //Logger.WriteToChat($"Event_LMouse_Up({pos}, {holdTime})");
            FindElementAt(pos);
            if (MouseOverElement != null && MouseOverElement.IsMouseOver(IsLMousePos)) return MouseOverElement.MouseUp(pos, holdTime);
            return false;
        }
        private unsafe bool Event_LMouse_Down(Point pos) {
            //Logger.WriteToChat($"Event_LMouse_Down({pos})");
            FindElementAt(pos);
            IsLMousePos = pos;
            if (MouseOverElement != null) return MouseOverElement.MouseDown(pos);
            return false;
        }
        private unsafe void Event_Control_Up(double holdTime) {
            //Logger.WriteToChat($"Event_Control_Up({holdTime})");
            IsHoldingControl = false;
            CoreManager.Current.WindowMessage -= DragManager;
            if (MouseOverHud != null) MouseOverHud.Render();
        }
        private unsafe void Event_Control_Down() {
            //Logger.WriteToChat($"Event_Control_Down()");
            IsHoldingControl = true;
            CoreManager.Current.WindowMessage += DragManager;
            if (MouseOverHud != null) MouseOverHud.Render();
        }
        private unsafe void Event_KeyUp(UBHud.WinKeys _key, double holdTime) {
            //Logger.WriteToChat($"Event_KeyDown({_key},{holdTime})");
            foreach (var hud in Huds) hud.Event_Key(_key, false, holdTime);
            if (_key == UBHud.WinKeys.VK_CONTROL) {
                Event_Control_Up(holdTime);
            }
        }
        private unsafe void Event_KeyDown(UBHud.WinKeys _key) {
            //Logger.WriteToChat($"Event_KeyDown({_key})");
            foreach (var hud in Huds) hud.Event_Key(_key, true, 0);
            if (_key == UBHud.WinKeys.VK_CONTROL) {
                Event_Control_Down();
            }
        }





        private bool isDragging = false;
        private Point dragStartPos = Point.Empty;
        private Point dragOffset = Point.Empty;
        private void DragManager(object sender, WindowMessageEventArgs e) {
            if (!IsHoldingControl) return;
            var newMousePos = new Point(e.LParam);

            if (MouseOverHud == null || !isDragging) {
                if ((UBHud.WinUser)e.Msg == UBHud.WinUser.WM_MOUSEMOVE) {
                    LastMousePos = newMousePos;
                    Event_MouseMove(newMousePos);
                }
                if (MouseOverHud == null) return;
            }

            switch ((UBHud.WinUser)e.Msg) {
                case UBHud.WinUser.WM_LBUTTONDOWN:
                    // check for clicking close button
                    if (MouseOverHud.IsCloseable && MouseOverHud.IsCloseClick(newMousePos)) {
                        MouseOverHud.Close();
                        e.Eat = true;
                        return;
                    }

                    if (!isDragging && MouseOverHud.IsDraggable) {
                        isDragging = true;
                        dragStartPos = newMousePos;
                        dragOffset = Point.Empty;
                        e.Eat = true;
                    }

                    break;
                case UBHud.WinUser.WM_LBUTTONUP:
                    isDragging = false;
                    MouseOverHud.Move(MouseOverHud.BBox.X + dragOffset.X, MouseOverHud.BBox.Y + dragOffset.Y);
                    e.Eat = true;
                    break;

                case UBHud.WinUser.WM_MOUSEMOVE:
                    if (isDragging) {
                        dragOffset.X = newMousePos.X - dragStartPos.X;
                        dragOffset.Y = newMousePos.Y - dragStartPos.Y;
                        MouseOverHud.Hud.Location = new Point(MouseOverHud.BBox.X + dragOffset.X, MouseOverHud.BBox.Y + dragOffset.Y);
                        //MouseOverHud.Render();
                        e.Eat = true;
                    }
                    break;
            }
        }


        private unsafe void Current_WindowMessage(object sender, WindowMessageEventArgs e) {
            if (e.Msg < 0xA0) return;
            if ((UBHud.WinUser)e.Msg == UBHud.WinUser.WM_KEYDOWN) {
                UBHud.WinKeys kd = (UBHud.WinKeys)e.WParam;
                if (Keys.ContainsKey(kd)) {
                    if (Keys[kd] == 0) {
                        Keys[kd] = *AcClient.Timer.cur_time;
                        Event_KeyDown(kd);
                    }
                }
            }
            if ((UBHud.WinUser)e.Msg == UBHud.WinUser.WM_KEYUP) {
                UBHud.WinKeys ku = (UBHud.WinKeys)e.WParam;
                if (Keys.ContainsKey(ku)) {
                    if (Keys[ku] != 0) {
                        Event_KeyUp(ku, *AcClient.Timer.cur_time - Keys[ku]);
                        Keys[ku] = 0;
                    }
                }

            }


            if (IsHoldingControl) return;

            var newMousePos = new Point(e.LParam);
            switch ((UBHud.WinUser)e.Msg) {
                case UBHud.WinUser.WM_MOUSEMOVE:
                    //Logger.WriteToChat($"WM_MOUSEMOVE: {newMousePos}");
                    Event_MouseMove(newMousePos);
                    LastMousePos = newMousePos;
                    break;
                case UBHud.WinUser.WM_LBUTTONDOWN:
                    if (!IsLMouse) {
                        e.Eat = Event_LMouse_Down(LastMousePos);
                        IsLMouse = true;
                        IsLMouseTS = *AcClient.Timer.cur_time;
                    }
                    //Logger.WriteToChat($"WM_LBUTTONDOWN: WParam:{e.WParam:X8} LParam:{new Point(e.LParam)}");
                    break;
                case UBHud.WinUser.WM_LBUTTONUP:
                    if (IsLMouse) {
                        e.Eat = Event_LMouse_Up(LastMousePos, *AcClient.Timer.cur_time - IsLMouseTS);
                        IsLMouse = false;
                    }
                    //Logger.WriteToChat($"WM_LBUTTONUP: WParam:{e.WParam:X8} LParam:{new Point(e.LParam)}");
                    break;
                    //case WinUser.WM_KEYDOWN:
                    //    //Logger.WriteToChat($"WM_KEYDOWN: WParam:{e.WParam:X8} LParam:{e.LParam:X8}");
                    //    break;
                    //case WinUser.WM_KEYUP:
                    //    //Logger.WriteToChat($"WM_KEYUP: WParam:{e.WParam:X8} LParam:{e.LParam:X8}");
                    //    break;


                    //case WinUser.WM_RBUTTONDOWN:
                    //    Logger.WriteToChat($"WM_RBUTTONDOWN: WParam:{e.WParam:X8} LParam:{new Point(e.LParam)}");
                    //    break;
                    //case WinUser.WM_RBUTTONUP:
                    //    Logger.WriteToChat($"WM_RBUTTONUP: WParam:{e.WParam:X8} LParam:{new Point(e.LParam)}");
                    //    break;
                    //case WinUser.WM_MBUTTONDOWN:
                    //    IsMouseMiddle = true;
                    //    //Logger.WriteToChat($"WM_MBUTTONDOWN: WParam:{e.WParam:X8} LParam:{new Point(e.LParam)}");
                    //    break;
                    //case WinUser.WM_MBUTTONUP:
                    //    Logger.WriteToChat($"WM_MBUTTONUP: WParam:{e.WParam:X8} LParam:{new Point(e.LParam)}");
                    //    break;
                    //case WinUser.WM_MOUSEWHEEL:
                    //    int change = (e.WParam >> 16) / 120;
                    //    MouseWheelPos += change;
                    //    //Logger.WriteToChat($"WM_MOUSEWHEEL: {(change > 0 ? "UP" : "DOWN")} {MouseWheelPos}");
                    //    break;



                    //case WinUser.WM_CHAR:
                    //    Logger.WriteToChat($"WM_CHAR: WParam:{e.WParam:X8} LParam:{e.LParam:X8}");
                    //    break;
                    //case WinUser.WM_SYSKEYDOWN:
                    //    Logger.WriteToChat($"WM_SYSKEYDOWN: WParam:{e.WParam:X8} LParam:{e.LParam:X8}");
                    //    break;
                    //case WinUser.WM_SYSKEYUP:
                    //    Logger.WriteToChat($"WM_SYSKEYUP: WParam:{e.WParam:X8} LParam:{e.LParam:X8}");
                    //    break;

            }

        }



        #region IDisposable Support
        protected bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {

                    if (isHandlingEvents) {
                        CoreManager.Current.WindowMessage -= Current_WindowMessage;
                        CoreManager.Current.RegionChange3D -= Current_RegionChange3D;
                        CoreManager.Current.WindowMessage -= DragManager;
                        Keys = null;
                        isHandlingEvents = false;
                    }
                    if (Huds != null) {
                        for (int i = Huds.Count - 1; i >= 0; i--) {
                            if (Huds[i] != null) {
                                Huds[i].Dispose();
                            }
                        }
                        Huds = null;
                    }
                }
                disposedValue = true;
            }
        }


        public void Dispose() {
            Dispose(true);
        }
        #endregion



    }
}


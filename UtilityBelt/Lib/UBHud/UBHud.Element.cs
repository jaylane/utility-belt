using System;
using System.Drawing;

namespace UtilityBelt.Lib {
    public partial class UBHud : UBHud.Element, IDisposable {

        /// <summary>
        /// UBHud.Element (interface)
        /// </summary>
        public interface Element {
            /// <summary>
            /// Parent Class
            /// </summary>
            public UBHud hud { get; }

            public int ZIndex { get; set; }

            /// <summary>
            /// element received mousedup.
            /// </summary>
            /// <param name="_pt">previous mouse click position, relative to hud.X,hud.Y</param>
            /// <param name="_hold_time">time in seconds that the mouse button was held</param>
            /// <returns></returns>
            public bool MouseUp(Point _pt, Double _hold_time);

            /// <summary>
            /// element received mousedown.
            /// </summary>
            /// <param name="_pt">mouse click position, relative to hud.X,hud.Y</param>
            public bool MouseDown(Point _pt);

            /// <summary>
            /// mouse is over the element
            /// </summary>
            /// <returns></returns>
            public bool MouseFocus();

            /// <summary>
            /// mouse is no longer over the element
            /// </summary>
            /// <returns></returns>
            public bool MouseBlur();

            /// <summary>
            /// Binding Box of the element
            /// </summary>
            public Rectangle BBox { get; }

            /// <summary>
            /// Binding Box of the element
            /// </summary>
            public bool IsMouseOver(Point _pt);

            /// <summary>
            /// sharpen those crayons, dig that napkin out of the trash, and get to drawin'
            /// </summary>
            public void Draw();


            public event Event OnClick;
        }
    }
}

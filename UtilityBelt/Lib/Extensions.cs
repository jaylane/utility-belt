using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace UtilityBelt.Lib {
    public static class Extensions {
        //https://stackoverflow.com/questions/7714022/how-to-get-a-string-width
        public static Size MeasureString(this Font font, string text) => TextRenderer.MeasureText(text, font);
        //Alternative approach.  Above failed with spaces.  Both give the same value when tested.
        //static Graphics fontGraphics = Graphics.FromImage(new Bitmap(1, 1));
        //public static SizeF MeasureString(this Font font, string text) => fontGraphics.MeasureString(text, font);

        //Enum conversion based on https://stackoverflow.com/questions/12828443/net-3-5-doesnt-have-enum-tryparse-how-to-safely-parse-string-to-enum-then?noredirect=1&lq=1
        public static bool EnumTryParse<T>(this string strType, out T result) {
            string strTypeFixed = strType.Replace(' ', '_');
            if (Enum.IsDefined(typeof(T), strTypeFixed)) {
                result = (T)Enum.Parse(typeof(T), strTypeFixed, true);
                return true;
            }
            else {
                foreach (string value in Enum.GetNames(typeof(T))) {
                    if (value.Equals(strTypeFixed, StringComparison.OrdinalIgnoreCase)) {
                        result = (T)Enum.Parse(typeof(T), value);
                        return true;
                    }
                }
                result = default(T);
                return false;
            }
        }

        public static bool Contains(this string source, string substring, StringComparison comparison) => source?.IndexOf(substring, comparison) > -1;
        public static bool ContainsCaseInsensitive(this string source, string substring) => source?.IndexOf(substring, StringComparison.OrdinalIgnoreCase) > -1;
    }
}

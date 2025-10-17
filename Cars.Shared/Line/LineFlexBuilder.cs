using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Cars.Shared.Line
{
    public static class LineFlexBuilder
    {
        public static object Text(string text, string weight = null, string size = null, string color = null, bool wrap = false)
        {
            var obj = new Dictionary<string, object>
            {
                { "type", "text" },
                { "text", text }
            };

            // 可選屬性
            if (!string.IsNullOrWhiteSpace(weight))
                obj["weight"] = weight.Trim();

            if (!string.IsNullOrWhiteSpace(size))
                obj["size"] = size.Trim();

            if (!string.IsNullOrWhiteSpace(color))
                obj["color"] = color.Trim();

            if (wrap)
                obj["wrap"] = true;

            return obj;
        }




        public static object Button(string label, string data, string style = "primary", string color = "#22c55e")
            => new
            {
                type = "button",
                style,
                color,
                height = "sm",
                action = new { type = "postback", label, data }
            };

        public static object Separator(string margin = null)
        {
            var obj = new Dictionary<string, object>
             {
                 { "type", "separator" }
             };
            if (!string.IsNullOrEmpty(margin))
                obj["margin"] = margin; // 只有需要時才加
            return obj;
        }


        public static object Box(string layout, IEnumerable<object> contents, string spacing = "md", string margin = null)
        {
            var obj = new Dictionary<string, object>
            {
                { "type", "box" },
                { "layout", layout },
                { "contents", contents.ToList() }
            };
            if (!string.IsNullOrEmpty(spacing))
                obj["spacing"] = spacing; 
            if (!string.IsNullOrEmpty(margin))
                obj["margin"] = margin;   
            return obj;
        }
        



        public static object Bubble(object body, object footer = null)
        {
            var bubble = new Dictionary<string, object>
            {
                { "type", "bubble" },
                { "body", body }
            };
            if (footer != null) bubble["footer"] = footer;
            return bubble;
        }

        public static string ToJson(object bubble, string altText = "Flex Message")
        {
            var obj = new
            {
                type = "flex",
                altText,
                contents = bubble
            };

            // 忽略 null 屬性
            return JsonConvert.SerializeObject(
                obj,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
        }
    }
}

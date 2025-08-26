using Microsoft.AspNetCore.Mvc.Rendering;
using Cars.Data;
using System.Linq;

namespace Cars.Helpers
{
    public static class HtmlHelpers
    {
        public static string AliasAddress(this IHtmlHelper html, string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            // 從 DI container 拿 ApplicationDbContext
            var db = (ApplicationDbContext)html.ViewContext.HttpContext.RequestServices
                .GetService(typeof(ApplicationDbContext));

            var aliases = db.PlaceAliases.ToList();

            foreach (var a in aliases)
            {
                if (!string.IsNullOrWhiteSpace(a.Keyword) && input.Contains(a.Keyword))
                {
                    input = input.Replace(a.Keyword, a.Alias);
                }
            }

            return input;
        }
    }
}

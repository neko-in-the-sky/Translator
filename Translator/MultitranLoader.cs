using HtmlAgilityPack;
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace Translator
{
    public class MultitranLoader
    {
        private const string BodyPlaceHolder = "{BODY}";
        private const string HeaderPlaceHolder = "{HEADER}";
        private readonly string _resultPageTemplate;
        private readonly string _errorPageTemplate;
        private readonly CachedWebClient _cachedWebClient;

        public MultitranLoader()
        {
            _resultPageTemplate = File.ReadAllText(Path.Combine("Templates", "translation_template.html"));
            _errorPageTemplate = File.ReadAllText(Path.Combine("Templates", "error_template.html"));
            _cachedWebClient = new CachedWebClient(100);
        }

        public async Task<string> LoadAsync(string selectedText, CancellationToken ct)
        {
            try
            {
                var query = selectedText.Trim()
                    .Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                    ?.Replace(" ", "+");

                if (string.IsNullOrWhiteSpace(query))
                {
                    return MakeErrorPage(Properties.Resources.Error_ClipboardIsEmpty);
                }

                var url = $"https://www.multitran.com/m.exe?l1=1&l2=2&s={query}";

                string page = await _cachedWebClient.DownloadOrGetFromCacheAsync(url, ct);
                var transformedPage = TransformPage(page);
                return transformedPage;
            }
            catch (Exception e)
            {
                return MakeErrorPage(e.ToString());
            }
        }

        private string TransformPage(string page)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(page);

            var table = htmlDoc.DocumentNode.Descendants("table")
                .Where(n => n.Attributes.Count == 1 && n.Attributes.Contains("width") &&
                            n.Attributes["width"].Value == "100%")
                .SingleOrDefault();

            if (table == null)
            {
                return MakeErrorPage(Properties.Resources.Error_NotFound);
            }

            foreach (var a in table.Descendants("a").ToArray())
            {
                var parent = a.ParentNode;
                if (parent != null)
                {
                    parent.ReplaceChild(HtmlNode.CreateNode($"<span>{a.InnerText}</span>"), a);
                }
            }

            var newPage = MakeResultsPage(table.OuterHtml);
            return newPage;
        }

        private string MakeResultsPage(string result)
        {
            var newPage = _resultPageTemplate.Replace(BodyPlaceHolder, result);
            return newPage;
        }

        private string MakeErrorPage(string error)
        {
            var newPage = _errorPageTemplate.Replace(BodyPlaceHolder, error);
            newPage = newPage.Replace(HeaderPlaceHolder, Properties.Resources.Error_CannotTranslate);
            return newPage;
        }
    }
}
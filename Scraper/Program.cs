namespace Scraper;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

class Program
{
    static async Task Main(string[] args)
    {
        var mvnRepositoryBaseUrl = "http://mvnrepository.com";
        var mvnCentralBaseUrl = "http://central.sonatype.com";
        var searchQuery = "java%20library%20github";
        var sortOption = "popular";
        var pageNb = 1;
        var mvnrepositorySearchOptions = $"/search?q={searchQuery}&p={pageNb}&sort={sortOption}";
        var _program = new Program();
        // We will store the html response of the request here
        var siteContent = string.Empty;

        siteContent = await _program.GetContentAsync(mvnRepositoryBaseUrl + mvnrepositorySearchOptions);

        var config = Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);
        var parser = context.GetService<IHtmlParser>();
        var document = parser?.ParseDocument(siteContent);

        var artifactLinkNodes = document?.QuerySelectorAll("[href]").Where(n => n.Attributes["href"]?.Text().Contains("artifact") ?? false);

        var artifactLinks = new List<string>();
        foreach (var link in artifactLinkNodes?.Select(ln => ln.Attributes["href"]?.TextContent) ?? new List<string>())
        {
            if (link is not null)
            {
                artifactLinks.Add(link);
            }
        }
        artifactLinks = artifactLinks.Distinct().ToList();

        foreach (var artifactLink in artifactLinks)
        {
            var mvnDetailPageContent = await _program.GetContentAsync(mvnRepositoryBaseUrl + artifactLink);
            var centralDetailPageContent = await _program.GetContentAsync(mvnCentralBaseUrl + artifactLink);

            var mvnDetailPageDocument = parser?.ParseDocument(mvnDetailPageContent);
            var centralDetailPageDocument = parser?.ParseDocument(centralDetailPageContent);

            var titleNode = mvnDetailPageDocument?.QuerySelector("title");
            var title = titleNode?.TextContent;
            var githubLinkNodes = centralDetailPageDocument?.QuerySelectorAll("a").Where(n => n.Attributes["href"]?.Text().Contains("github") ?? false);
            var githubLinks = string.Join(',', githubLinkNodes?.Select(n => n.Attributes["href"]?.TextContent) ?? new List<string>());

            if (!string.IsNullOrWhiteSpace(githubLinks))
            {
                Console.WriteLine($"{title} : {githubLinks}");
            }
        }
    }

    private async Task<string> GetContentAsync(string url)
    {
        HttpClientHandler handler = new HttpClientHandler
        {
            UseDefaultCredentials = true
        };
        HttpClient httpClient = new HttpClient(handler);

        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "identity");
        // httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.2; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0");
        // httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:109.0) Gecko/20100101 Firefox/119.0");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Other");
        // httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Charset", "ISO-8859-1");
        // httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Key", api_key);

        var response = await httpClient.GetAsync(url);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            var currentInner = ex.InnerException;
            while (currentInner != null)
            {
                Console.WriteLine(currentInner.Message);
                currentInner = currentInner.InnerException;
            }
        }

        var content = string.Empty;
        var stream = await response.Content.ReadAsStreamAsync();

        using (var sr = new StreamReader(stream, Encoding.UTF8))
        {
            content = sr.ReadToEnd();
        }

        return content;
    }
}

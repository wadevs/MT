namespace Scraper;

using System.Net;
using System.Runtime.InteropServices;
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
        // var searchQuery = "PhilJay/MPAndroidChart";
        // var searchQuery = "library";
        var sortOption = "popular";
        var minPageNb = 1;
        var maxPageNb = 1;

        var _program = new Program();

        // We will store the html response of the request here
        var siteContent = string.Empty;

        var now = DateTime.Now.Ticks;
        var outputDirPath = "../../APIDiff/dataset/ToProcess/";
        var outputDirUsagesPath = outputDirPath + "Usages/";
        var exceptionsDirPath = "../../APIDiff/dataset/Exceptions/";
        //var fileName = $"Results/scrap_p{minPageNb}_{maxPageNb}_{now}.csv";
        var csv = ".csv";
        var fileName = $"{now}_scrap_p{minPageNb}_{maxPageNb}";
        var exLog = $"{exceptionsDirPath}{now}_exceptions";
        var builder = new StringBuilder();
        var exceptionsLog = new StringBuilder();

        var mvnRepositorySearchOptions = string.Empty;

        await _program.ProcessQuery(minPageNb, maxPageNb, mvnRepositorySearchOptions, searchQuery, sortOption,
                                    mvnRepositoryBaseUrl, mvnCentralBaseUrl, exceptionsLog, exLog, builder, fileName,
                                    outputDirPath, _program, now, csv, false);

    }

    private async Task ProcessQuery(int minPageNb, int maxPageNb, string mvnRepositorySearchOptions, string searchQuery,
                                    string sortOption, string mvnRepositoryBaseUrl, string mvnCentralBaseUrl,
                                    StringBuilder exceptionsLog, string exLog, StringBuilder builder, string fileName,
                                    string outputDirPath,
                                    Program _program, long now, string csv, bool isUsageQuery)
    {
        for (int curPage = minPageNb; curPage <= maxPageNb; curPage++)
        {
            var searchUrl = string.Empty;

            if (!isUsageQuery)
            {
                mvnRepositorySearchOptions = $"/search?q={searchQuery}&p={curPage}&sort={sortOption}";
                searchUrl = mvnRepositoryBaseUrl + mvnRepositorySearchOptions;
            }
            else
            {
                searchUrl = mvnRepositorySearchOptions;
            }

            Console.WriteLine($"isUsage: {isUsageQuery}, searchUrl: {searchUrl}");
            string siteContent = await _program.GetContentAsync(searchUrl, exceptionsLog);

            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var parser = context.GetService<IHtmlParser>();
            var document = parser?.ParseDocument(siteContent);

            // var artifactLinkNodes = document?.QuerySelectorAll("[href]").Where(n => n.Attributes["href"]?.Text().Contains("artifact") ?? false);
            var artifactLinkNodes = document?.All.Where(n => n.LocalName == "h2" && n.ClassList.Contains("im-title"));
            //.Where(n => n.LocalName == "a" && (n.Attributes["href"]?.Text().Contains("artifact") ?? false));

            var artifactLinks = new List<string>();
            foreach (var link in artifactLinkNodes?.Select(ln => ln.Children
                                                        .FirstOrDefault(cn => cn.LocalName == "a" && !cn.ClassList.Contains("im-usages"))
                                                        ?.Attributes["href"]?.TextContent) ?? new List<string>())
            {
                if (link is not null)
                {
                    artifactLinks.Add(link);
                }
            }
            artifactLinks = artifactLinks.Distinct().ToList();

            var headerLine = "ArtifactTitle;ArtifactLink;Title;GithubLinks";
            builder.AppendLine(headerLine);

            foreach (var artifactLink in artifactLinks)
            {
                var mvnDetailPageContent = await _program.GetContentAsync(mvnRepositoryBaseUrl + artifactLink, exceptionsLog);
                var centralDetailPageContent = await _program.GetContentAsync(mvnCentralBaseUrl + artifactLink, exceptionsLog);

                var mvnDetailPageDocument = parser?.ParseDocument(mvnDetailPageContent);
                var centralDetailPageDocument = parser?.ParseDocument(centralDetailPageContent);

                var titleNode = mvnDetailPageDocument?.QuerySelector("title");
                var title = titleNode?.TextContent;
                var artifactTitleNode = centralDetailPageDocument?.QuerySelector("h1");
                var artifactTitle = artifactTitleNode?.TextContent;
                //var githubLinkNodes = centralDetailPageDocument?.QuerySelectorAll("a").Where(n => n.Attributes["href"]?.Text().Contains("github") ?? false);
                var githubLinkNodes = centralDetailPageDocument?.All.Where(n => n.LocalName == "a" && (n.Attributes["href"]?.Text().Contains("github") ?? false) && n.Children.Any(cn => cn.TextContent == "Source Control"));
                // var githubLinks = string.Join(',', githubLinkNodes?.Select(n => " " + n.Attributes["href"]?.TextContent) ?? new List<string>());
                var githubLinks = githubLinkNodes?.Select(n => " " + n.Attributes["href"]?.TextContent).ToList() ?? new List<string>();

                var validGitRepoLinks = new List<string>();
                foreach (var link in githubLinks)
                {
                    try
                    {
                        await _program.GetContentAsync(link + ".git", new StringBuilder());
                        validGitRepoLinks.Add(link);
                        Console.WriteLine("Valid link: " + link);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(link + ": " + ex.Message);
                    }
                }

                var validGitRepoLinksString = string.Join(',', validGitRepoLinks);
                if (!string.IsNullOrWhiteSpace(validGitRepoLinksString))
                {
                    // var lineToAdd = $"{artifactTitle}; {artifactLink}; {title}; {githubLinks}";
                    var lineToAdd = $"{artifactTitle}; {artifactLink}; {title}; {validGitRepoLinksString}";
                    Console.WriteLine($"Page: {curPage}, line: {lineToAdd}");
                    builder.AppendLine(lineToAdd);
                }

                if (!isUsageQuery)
                {
                    await ProcessQuery(0, 0, mvnRepositoryBaseUrl + artifactLink + "/usages", string.Empty, string.Empty, mvnRepositoryBaseUrl,
                                       mvnCentralBaseUrl, new StringBuilder(), $"{exLog}_{artifactTitle}_usages",
                                       new StringBuilder(), $"{fileName}_{artifactTitle}_usages",
                                       outputDirPath + "Usages/", _program, now, csv, true);
                }
            }

            try
            {
                File.WriteAllText(outputDirPath + fileName + csv, builder.ToString());
                File.WriteAllText(exLog + csv, exceptionsLog.ToString());
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
        }
    }

    private async Task<string> GetContentAsync(string url, StringBuilder exSb)
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

        var response = new HttpResponseMessage();
        try
        {
            response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{url}: {ex.Message}");
            exSb.AppendLine($"{url}: {ex.Message}");
            var currentInner = ex.InnerException;
            while (currentInner != null)
            {
                Console.WriteLine(currentInner.Message);
                exSb.AppendLine($"{url}: {currentInner.Message}");
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

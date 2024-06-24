using HtmlAgilityPack;
using ShellProgressBar;

class Program
{
    private static ProgressBar progressBar;
    private static int NDownloaded = 0;
    private static int TotalForDownload = 0;
    private static async Task Main(string[] args)
    {
        string url = "https://archive.org/download/mame-merged/mame-merged/";
        List<string> links = await GetLinksAsync(url);
        Console.WriteLine("Starting download process...");

        const int totalTicks = 10;
        var options = new ProgressBarOptions
        {
            ProgressCharacter = '─',
            ProgressBarOnBottom = true
        };
        progressBar = new ProgressBar(totalTicks, "Downloading...", options);

        List<Task> tasks = new List<Task>();
        TotalForDownload = links.Count();
        foreach (string romLink in links)
        {
            tasks.Add(DownloadROM(romLink));
        }
        Task.WaitAll(tasks.ToArray());
    }

    public static async Task<List<string>> GetLinksAsync(string url)
    {
        List<string> linkList = new List<string>();
        using (HttpClient client = new HttpClient())
        {
            try
            {
                string contenidoHtml = await client.GetStringAsync(url);
                HtmlDocument documento = new HtmlDocument();
                documento.LoadHtml(contenidoHtml);
                foreach (HtmlNode nodo in documento.DocumentNode.SelectNodes("//a[@href]"))
                {
                    string href = nodo.GetAttributeValue("href", string.Empty);
                    if (href.EndsWith(".zip"))
                    {
                        if (!href.StartsWith("http://") && !href.StartsWith("https://"))
                        {
                            href = url + href;
                        }
                        linkList.Add(href);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scrapping links: {ex.Message}");
            }
        }

        return linkList;
    }
    public static async Task DownloadROM(string romLink)
    {
        using (HttpClient client = new())
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            string PathOutputRoms = Path.Combine("Roms");
            Directory.CreateDirectory(PathOutputRoms);
            ushort tries = 0;
            bool downloaded = false;
            do
            {
                string romFileName = Path.GetFileName(romLink);
                string fileName = Path.Combine(PathOutputRoms, romFileName);
                if (tries > 0)
                {
                    //Console.WriteLine($"Trying downloading again: {romLink}");
                    client.Timeout *= 2;
                }
                else
                {
                    //Console.WriteLine($"Downloading ROM: {fileName}");
                }
                try
                {
                    if (!File.Exists(Path.Combine(PathOutputRoms, romFileName)))
                    {
                        byte[] fileContent = await client.GetByteArrayAsync(romLink);
                        await File.WriteAllBytesAsync(fileName, fileContent);
                        downloaded = true;
                        //Console.WriteLine($"Downloaded ROM: {fileName}");
                        NDownloaded++;
                        progressBar.Tick($"Downloaded {NDownloaded} of {TotalForDownload}");
                    }
                    else
                    {
                        downloaded = true;
                        NDownloaded++;
                        //Console.WriteLine($"Skipping: {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Error downloading ROM from: {romLink} [{ex.Message}]");
                }
            } while (!downloaded && tries < 3);
        }
    }
}

using HtmlAgilityPack;
using Newtonsoft.Json;
using ROMDownloader;
using ShellProgressBar;

class Program
{
    private static ProgressBar? progressBar;
    private static int NDownloaded = 0;
    private static int TotalForDownload = 0;
    private static async Task Main(string[] args)
    {
        List<ROMSource> romSources = new();
        if (!File.Exists("RomDownloader.json"))
        {
            romSources.Add(new ROMSource() { Type = "MAME", URI = "https://archive.org/download/mame-merged/mame-merged/" });
            File.WriteAllText("RomDownloader.json", JsonConvert.SerializeObject(romSources));
        } else
        {
            string contentJson = File.ReadAllText("RomDownloader.json");
            if (contentJson.Length > 0)
            {
                romSources = JsonConvert.DeserializeObject<List<ROMSource>>(contentJson);
            }
        }

        foreach(ROMSource romSource in romSources)
        {
            List<string> links = await GetLinksAsync(romSource);
            Console.WriteLine($"Starting download process... [{romSource.Type} Roms]");

            const int totalTicks = 10;
            var options = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };
            progressBar = new ProgressBar(totalTicks, "Downloading...", options);

            List<Task> tasks = [];
            TotalForDownload = links.Count();
            foreach (string romLink in links)
            {
                tasks.Add(DownloadROM(romSource, romLink));
            }
            Task.WaitAll(tasks.ToArray());
            progressBar.Tick($"Download Completed.");
        }
    }

    public static async Task<List<string>> GetLinksAsync(ROMSource romSource)
    {
        List<string> linkList = new List<string>();
        using (HttpClient client = new HttpClient())
        {
            try
            {
                string contenidoHtml = await client.GetStringAsync(romSource.URI);
                HtmlDocument documento = new HtmlDocument();
                documento.LoadHtml(contenidoHtml);
                foreach (HtmlNode nodo in documento.DocumentNode.SelectNodes("//a[@href]"))
                {
                    string href = nodo.GetAttributeValue("href", string.Empty);
                    if (href.EndsWith(".zip"))
                    {
                        if (!href.StartsWith("http://") && !href.StartsWith("https://"))
                        {
                            href = romSource.URI + href;
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
    public static async Task DownloadROM(ROMSource romSource, string romLink)
    {
        if (progressBar == null) return;
        using (HttpClient client = new())
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            string PathOutputRoms = Path.Combine("Roms", romSource.Type);
            Directory.CreateDirectory(PathOutputRoms);
            ushort tries = 0;
            bool downloaded = false;
            do
            {
                string romFileName = Path.GetFileName(romLink);
                string fileName = Path.Combine(PathOutputRoms, romFileName);
                if (tries > 0)
                {
                    client.Timeout *= 2;
                }
                try
                {
                    if (!File.Exists(Path.Combine(PathOutputRoms, romFileName)))
                    {
                        byte[] fileContent = await client.GetByteArrayAsync(romLink);
                        await File.WriteAllBytesAsync(fileName, fileContent);
                        downloaded = true;
                        NDownloaded++;
                        progressBar.Tick($"Downloaded {NDownloaded} of {TotalForDownload}");
                    }
                    else
                    {
                        downloaded = true;
                        NDownloaded++;
                        progressBar.Tick($"Downloaded {NDownloaded} of {TotalForDownload}");
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

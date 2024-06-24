using HtmlAgilityPack;

class Program
{
    private static async Task Main(string[] args)
    {
        string url = "https://archive.org/download/mame-merged/mame-merged/";
        List<string> links = await GetLinksAsync(url);

        foreach (string romLink in links)
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
                        Console.WriteLine($"Trying downloading again: {romLink}");
                    } else
                    {
                        Console.WriteLine($"Downloading ROM: {fileName}");
                    }
                    try
                    {
                        if (!File.Exists(Path.Combine(PathOutputRoms, romFileName)))
                        {
                            byte[] fileContent = await client.GetByteArrayAsync(romLink);
                            await File.WriteAllBytesAsync(fileName, fileContent);
                            downloaded = true;
                            Console.WriteLine($"Downloaded ROM: {fileName}");
                        } else
                        {
                            downloaded = true;
                            Console.WriteLine($"Skipping: {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error downloading ROM from: {romLink} [{ex.Message}]");
                    }
                } while (!downloaded && tries < 3);
            }
        }
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
}

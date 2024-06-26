using HtmlAgilityPack;
using Newtonsoft.Json;
using ROMDownloader;
using ShellProgressBar;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Web;

class Program
{
    private static ProgressBar? _progressBar;
    private static int _NDownloaded = 0;
    private static int _TotalForDownload = 0;
    private static CookieContainer? _CookieContainer;
    private static RomDownloaderConfig config = new() { ROMSources = new(), MaxParallelDownloads = 3 };
    private static int _MaxDegreeOfParallelism = 1;
    private static SemaphoreSlim _SemaphoreDownloads;
    private static int _ActiveTasks = 0;
    private static double _LastDownloadSpeed = 0;
    private static ProgressBarOptions _ChildProgressBarOptions;
    private static async Task Main(string[] args)
    {
        const int totalTicks = 100;
        var options = new ProgressBarOptions
        {
            ProgressBarOnBottom = true,
            ShowEstimatedDuration = false,
            DisableBottomPercentage = false,
            ForegroundColor = ConsoleColor.Yellow,
            ForegroundColorDone = ConsoleColor.DarkGreen,
            BackgroundColor = ConsoleColor.DarkGray,
            BackgroundCharacter = '\u2593'
        };
        _ChildProgressBarOptions = new ProgressBarOptions
        {
            ForegroundColor = ConsoleColor.DarkGreen,
            BackgroundColor = ConsoleColor.DarkGray,
            ProgressCharacter = '─'
        };
        string vName = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        Console.Title = $"RomDownloader v{vName}";
        Console.WriteLine($"ROMDownloader v{vName} starting...");
        if (!File.Exists("RomDownloader.json"))
        {
            //config.ROMSources.Add(new ROMSource() { Type = "MAME", URI = "https://archive.org/download/mame-merged/mame-merged/", Extension = "zip" });
            config.ROMSources.Add(new ROMSource() { Type = "GameCube", URI = "https://archive.org/download/rvz-gc-europe-redump/RVZ-GC-EUROPE-REDUMP/", Extension = "rvz" });
            Console.WriteLine("Do you want a authentificated downloads for archive.org: [Y/N]");
            string? ConsoleDownloads = Console.ReadLine();
            if (ConsoleDownloads?.ToUpper() == "Y")
            {
                Console.WriteLine("Username for archive.org:");
                string? Username = Console.ReadLine();
                Console.WriteLine("Password for archive.org:");
                string? Password = Console.ReadLine();
                if (Username != null && Password != null)
                {
                    config.ArchiveUsername = Username;
                    config.ArchivePassword = Password;
                }
            }
            File.WriteAllText("RomDownloader.json", JsonConvert.SerializeObject(config));
        } else
        {
            string contentJson = File.ReadAllText("RomDownloader.json");
            if (contentJson.Length > 0)
            {
                config = JsonConvert.DeserializeObject<RomDownloaderConfig>(contentJson);
            }
        }
        if (config.MaxParallelDownloads > 0)
        {
            _MaxDegreeOfParallelism = (int)config?.MaxParallelDownloads;
        }
        _progressBar = new ProgressBar(totalTicks, "Starting ROMDownloader...", options);
        if (config?.ArchiveUsername != null && config?.ArchivePassword != null)
        {
            await LoginArchive();
        }
        if (config?.ROMSources != null)
        {
            _progressBar.WriteLine($"ROMDownloader config loaded. Made by DaRkFox. {config.ROMSources.Count} Sources for ROM download.");

            foreach (ROMSource romSource in config.ROMSources)
            {
                List<string> links = await GetLinksAsync(romSource);
                _progressBar.WriteLine($"[{romSource.Type}] Download starting...");

                List<Task> tasks = [];
                _SemaphoreDownloads = new SemaphoreSlim(_MaxDegreeOfParallelism);
                _TotalForDownload = links.Count();
                foreach (string romLink in links)
                {
                    await _SemaphoreDownloads.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            _ActiveTasks++;
                            await DownloadROM(romSource, romLink);
                        }
                        finally
                        {
                            _ActiveTasks--;
                            _SemaphoreDownloads.Release();
                        }
                    }));
                }
                await Task.WhenAll(tasks);
                _progressBar.Tick($"[{romSource.Type}] Download Completed.");
            }
        }
    }
    private static async Task LoginArchive()
    {
        var handlerHttp = new HttpClientHandler
        {
            CookieContainer = new CookieContainer()
        };
        _CookieContainer = handlerHttp.CookieContainer;
        using (HttpClient client = new(handlerHttp))
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://archive.org/account/login");
            request.Headers.Add("Cookie", "test-cookie=1");
            request.Headers.Referrer = new Uri("https://archive.org/account/login");
            var boundary = "----WebKitFormBoundary3b99WJs0IQkAZrTx";
            var content = new MultipartFormDataContent(boundary);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=" + boundary);
            content.Add(new StringContent(config.ArchiveUsername), "username");
            content.Add(new StringContent(config.ArchivePassword), "password");
            content.Add(new StringContent("true"), "remember");
            content.Add(new StringContent("https://archive.org/"), "referer");
            content.Add(new StringContent("true"), "login");
            content.Add(new StringContent("true"), "submit_by_js");
            request.Content = content;
            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                _progressBar.WriteLine("Login OK in Archive.org for authentificated downloads.");
            }
        }
    }

    public static async Task<List<string>> GetLinksAsync(ROMSource romSource)
    {
        List<string> linkList = new List<string>();
        using (HttpClient client = new())
        {
            try
            {
                string contenidoHtml = await client.GetStringAsync(romSource.URI);
                HtmlDocument documento = new();
                documento.LoadHtml(contenidoHtml);
                foreach (HtmlNode nodo in documento.DocumentNode.SelectNodes("//a[@href]"))
                {
                    string href = nodo.GetAttributeValue("href", string.Empty);
                    if (href.EndsWith("." + romSource.Extension))
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
    private static async Task DownloadROM(ROMSource romSource, string romLink)
    {
        if (_progressBar == null) return;
        await DownloadWithProgressBar(romSource, romLink);
    }

    private static async Task DownloadWithProgressBar(ROMSource romSource, string romLink)
    {
        if (_CookieContainer == null) _CookieContainer = new CookieContainer();
        var handlerHttp = new HttpClientHandler
        {
            CookieContainer = _CookieContainer
        };
        var request = new HttpRequestMessage(HttpMethod.Get, romLink);
        using (HttpClient client = new(handlerHttp))
        {
            client.Timeout = TimeSpan.FromSeconds(600);
            string PathOutputRoms = Path.Combine("Roms", romSource.Type);
            Directory.CreateDirectory(PathOutputRoms);
            ushort tries = 0;
            bool downloaded = false;
            do
            {
                string romFileName = Path.GetFileName(romLink);
                string fileNameRomOutput = HttpUtility.UrlDecode(romFileName, System.Text.Encoding.UTF8);
                string fileNameRomOutputFixed = Path.Combine(PathOutputRoms, fileNameRomOutput);
                try
                {
                    if (!File.Exists(Path.Combine(PathOutputRoms, fileNameRomOutput)))
                    {
                        var stopwatch = Stopwatch.StartNew();
                        ChildProgressBar childProgressBar = _progressBar.Spawn(100, "ChildBarPercentTest", _ChildProgressBarOptions);
                        childProgressBar?.Tick($"Downloading {fileNameRomOutputFixed}..."); // Initial refresh state
                        long romLength = 0;
                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();

                            var contentLength = response.Content.Headers.ContentLength;
                            if (contentLength != null)
                            {
                                romLength = (long)contentLength;
                            }
                            if (contentLength.HasValue)
                            {
                                using var contentStream = await response.Content.ReadAsStreamAsync();
                                using var fileStream = new FileStream(fileNameRomOutputFixed, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
                                var totalRead = 0L;
                                var buffer = new byte[8192];
                                int bytesRead;

                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalRead += bytesRead;
                                    var progress = (double)totalRead / contentLength.Value * 100;
                                    childProgressBar?.AsProgress<float>().Report((float)progress/100);
                                    childProgressBar?.Tick($"Downloading: {Path.GetFileName(fileNameRomOutputFixed)} {(_LastDownloadSpeed > 0 ? $"[{_LastDownloadSpeed.ToString("N2")} MB/s]" : "")}"); // refresh state

                                }
                            }
                        }
                        var fileSizeInBytes = romLength;
                        var timeInSeconds = stopwatch.Elapsed.TotalSeconds;
                        var speedInBytesPerSecond = fileSizeInBytes / timeInSeconds;
                        var speedInMegabytesPerSecond = speedInBytesPerSecond / (1024.0 * 1024.0);
                        stopwatch.Stop();
                        downloaded = true;
                        _NDownloaded++;
                        tries = 0;
                        _LastDownloadSpeed = speedInMegabytesPerSecond;
                        _progressBar?.Tick($"Downloaded {_NDownloaded}/{_TotalForDownload} [Tries:{tries}] [{_LastDownloadSpeed.ToString("N2")} MB/s] [LastActionMessage: SUCCESS]");
                    }
                    else
                    {
                        downloaded = true;
                        tries = 0;
                        _NDownloaded++;
                        _progressBar?.Tick($"Downloaded {_NDownloaded}/{_TotalForDownload} [Tries:{tries}] [LastActionMessage: Failed Download]");
                    }
                }
                catch (Exception ex)
                {
                    tries++;
                    _progressBar?.Tick($"Downloaded {_NDownloaded}/{_TotalForDownload} [Tries:{tries}] [LastActionMessage: {ex.Message}]");
                }
            } while (!downloaded && tries < 3);
        }
    }
}

namespace ROMDownloader
{
    public class RomDownloaderConfig
    {
        public string ArchiveUsername { get; set; }
        public string ArchivePassword { get; set; }
        public List<ROMSource> ROMSources { get; set; }
    }
}

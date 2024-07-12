namespace ROMDownloader
{
    public class ROMSource
    {
        public string Type { get; set; }
        public string URI { get; set; }
        public string Extension { get; set; }
        public ScrappingType ScrappingType { get; set; }
    }
    public enum ScrappingType
    {
        ListOfLinks,
        DirectLink,
    }
}

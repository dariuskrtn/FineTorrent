namespace FineTorrent.Domain.Models
{
    public class FileItem
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public long Offset { get; set; }
    }
}
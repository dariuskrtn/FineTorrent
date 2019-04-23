using System.Collections.Generic;

namespace FineTorrent.Domain.Models
{
    public class Torrent
    {
        public string Name { get; set; }
        public bool IsPrivate { get; set; }
        public List<FileItem> FileItems { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace FineTorrent.Domain.Models
{
    public class TorrentFile
    {
        public string Name { get; set; }
        public List<string> Trackers { get; set; }
        public DateTime CreationDate { get; set; }
        public string Comment { get; set; }
        public string CreatedBy { get; set; }
        public Encoding Encoding { get; set; }
        public long PieceSize { get; set; }
        public List<byte[]> PieceHashes { get; set; } //every hash consists of 20 bytes
        public List<FileItem> FileItems { get; set; }
        public long Size { get; set; }
        public byte[] InfoHash { get; set; }
        public bool IsPrivate { get; set; }
    }
}
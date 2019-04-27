using System;
using System.Collections.Generic;
using System.Text;

namespace FineTorrent.Domain.Models
{
    public class Torrent
    {
        public string Name { get; set; }
        public bool IsPrivate { get; set; }
        public List<FileItem> FileItems { get; set; }
        public string DownloadDirectory { get; set; }
        public List<string> Trackers { get; set; }
        public string Comment { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public int BlockSize { get; set; }
        public long PieceSize { get; set; }
        public TorrentPiece[] TorrentPieces { get; set; }
        public byte[] TorrentHash { get; set; }
        public Encoding Encoding { get; set; }
    }
}
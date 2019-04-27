namespace FineTorrent.Domain.Models
{
    public class TorrentPiece
    {
        public byte[] Hash { get; set; }
        public bool Verified { get; set; }
    }
}
namespace FineTorrent.Application.SwarmHandler
{
    public class Peer
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public bool[] Pieces { get; set; }
        public bool IsConnected { get; set; }
        public bool IsInterested { get; set; }
        public bool IsInteresting { get; set; }
        public bool IsChoked { get; set; } = true;
        public bool IsChoking { get; set; } = true;

    }
}
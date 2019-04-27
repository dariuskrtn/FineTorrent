using FineTorrent.Domain.Enums;

namespace FineTorrent.Application.TrackerApi
{
    public class TrackerRequest
    {
        public string Tracker { get; set; }
        public byte[] InfoHash { get; set; } //torrent infohash.
        public byte[] PeerId { get; set; } //string to identify ourselves.
        public string Port { get; set; } //the port we're listening on for incoming connections.
        public long Uploaded { get; set; } //the number of bytes we've sent to other peers.
        public long Downloaded { get; set; } //the number of verified bytes we have.
        public long Left { get; set; } //the number of bytes we have left to download.
        public TrackerEvent Event { get; set; } //the current state of the client.
        public bool Compact { get; set; } //whether or not to return a compact peer list.
    }
}
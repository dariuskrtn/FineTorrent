namespace FineTorrent.Application.PeerHandling
{
    public class PeerState
    {
        public bool IsConnected { get; set; }
        public bool IsHandshakeReceived { get; set; }
        public bool IsInterested { get; set; }
        public bool IsChoking { get; set; } = true;

        public override string ToString()
        {
            return "IsConnected: " + IsConnected + ", " +
                   "IsHandshakeReceived: " + IsHandshakeReceived + ", " +
                   "IsInterested: " + IsInterested + ", " +
                   "IsChoking: " + IsChoking;
        }
    }
}
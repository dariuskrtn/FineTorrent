namespace FineTorrent.Application.PeerHandling.Responses
{
    public class RequestResponse
    {
        public int PieceIndex { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }
}
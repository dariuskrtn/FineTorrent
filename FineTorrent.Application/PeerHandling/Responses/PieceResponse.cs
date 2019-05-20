namespace FineTorrent.Application.PeerHandling.Responses
{
    public class PieceResponse
    {
        public int PieceIndex { get; set; }
        public int Offset { get; set; }
        public byte[] Data { get; set; }

        public override string ToString()
        {
            return "PieceIndex: " + PieceIndex+ ", " +
             "Offset: " + Offset+ ", " +
             "DataLength: " + Data.Length;
        }
    }
}
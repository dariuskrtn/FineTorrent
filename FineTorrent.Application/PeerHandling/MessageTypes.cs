namespace FineTorrent.Application.PeerHandling
{
    public static class MessageTypes
    {
        public const byte ChokeMessage = 0;
        public const byte UnchokeMessage = 1;
        public const byte InterestedMessage = 2;
        public const byte NotInterestedMessage = 3;
        public const byte HaveMessage = 4;
        public const byte BitfieldMessage = 5;
        public const byte RequestMessage = 6;
        public const byte PieceMessage = 7;
    }
}
using System;
using FineTorrent.Application.PeerHandling.Responses;

namespace FineTorrent.Application.SwarmHandler
{
    public interface ISwarmHandler : IDisposable
    {
        bool RequestPiece(int pieceIndex, int offset, int length); //Returns false if piece cannot be currently obtained.
        void AddPeer(string address, int port);
        IObservable<PieceResponse> GetPiecesObservable();
    }
}
using System;
using System.Threading.Tasks;
using FineTorrent.Application.PeerHandling.Responses;

namespace FineTorrent.Application.PeerHandling
{
    public interface IPeerHandler : IDisposable
    {
        Task Connect(string address, int port);
        Task SendHandshake();
        Task SendKeepAlive();
        Task SendChoke();
        Task SendUnchoke();
        Task SendInterested();
        Task SendNotInterested();
        Task SendHave(int pieceIndex);
        Task SendBitField(bool[] bits);
        Task SendRequest(int pieceIndex, int offset, int length);
        Task SendPiece(int pieceIndex, int offset, byte[] data);
        IObservable<PieceResponse> GetPiecesObservable();
        IObservable<PeerState> GetPeerStateObservable();
        IObservable<RequestResponse> GetRequestObservable();
        IObservable<bool[]> GetBitfieldObservable();
        IObservable<int> GetHaveObservable();
    }
}
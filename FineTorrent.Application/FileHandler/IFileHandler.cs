using System;
using System.Threading.Tasks;

namespace FineTorrent.Application.FileHandler
{
    public interface IFileHandler : IDisposable
    {
        Task InitializeFiles();
        void SavePiece(int pieceId, int offset, byte[] data);
        byte[] GetPiece(int pieceId, int offset, int length);
        IObservable<int> GetPieceCompletedObservable();
    }
}
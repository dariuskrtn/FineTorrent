using System;

namespace FineTorrent.Application.DownloadStrategy
{
    public interface IDownloadStrategy : IDisposable
    {
        void StartDownload();
        void AddPeer(string ipAddress);
        IObservable<float> GetProgressObservable();
    }
}
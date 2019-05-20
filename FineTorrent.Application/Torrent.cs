using System;
using System.Threading.Tasks;
using FineTorrent.Application.Decoders;

namespace FineTorrent.Application
{
    public class Torrent
    {
        private readonly TorrentHandler.TorrentHandler _torrentHandler;

        public Torrent(string filePath, string downloadPath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("message", nameof(filePath));
            }

            if (string.IsNullOrWhiteSpace(downloadPath))
            {
                throw new ArgumentException("message", nameof(downloadPath));
            }

            var torrentFileHandler = new TorrentFileHandler.TorrentFileHandler(new BDecoder());
            var torrentFile = torrentFileHandler.LoadFromFilePath(filePath);
            var trackerApi = new TrackerApi.TrackerApi(new BDecoder());
            _torrentHandler = new TorrentHandler.TorrentHandler(trackerApi, torrentFile, downloadPath);
        }

        public async Task StartDownload()
        {
            await _torrentHandler.StartDownload();
        }

        public IObservable<float> GetProgressObservable()
        {
            return _torrentHandler.GetProgressObservable();
        }
    }
}
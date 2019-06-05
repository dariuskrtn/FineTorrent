using System;
using System.Threading.Tasks;
using FineTorrent.Application.Decoders;
using FineTorrent.Domain.Models;

namespace FineTorrent.Application
{
    public class Torrent
    {
        private readonly TorrentHandler.TorrentHandler _torrentHandler;
        private readonly TorrentFile _torrentFile;

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
            _torrentFile = torrentFileHandler.LoadFromFilePath(filePath);
            var trackerApi = new TrackerApi.TrackerApi(new BDecoder());
            _torrentHandler = new TorrentHandler.TorrentHandler(trackerApi, _torrentFile, downloadPath);
        }

        public async Task StartDownload()
        {
            await _torrentHandler.StartDownload();
        }

        public IObservable<float> GetProgressObservable()
        {
            return _torrentHandler.GetProgressObservable();
        }

        public TorrentFile GetTorrentFile()
        {
            return _torrentFile;
        }
    }
}
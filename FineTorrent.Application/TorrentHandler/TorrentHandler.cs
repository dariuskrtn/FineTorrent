using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FineTorrent.Application.DownloadStrategy;
using FineTorrent.Application.FileHandler;
using FineTorrent.Application.SwarmHandler;
using FineTorrent.Application.TrackerApi;
using FineTorrent.Domain.Enums;
using FineTorrent.Domain.Models;

namespace FineTorrent.Application.TorrentHandler
{
    public class TorrentHandler : IDisposable
    {
        private readonly ITrackerApi _trackerApi;

        private readonly TorrentFile _torrentFile;
        private readonly IDownloadStrategy _downloadStrategy;
        private readonly IFileHandler _fileHandler;

        private CompositeDisposable _trackersDisposable = new CompositeDisposable();

        private long _downloaded = 0;
        private long _uploaded = 0;
        private string _port;
        private string _peerId;
        
        public TorrentHandler(ITrackerApi trackerApi, TorrentFile torrentFile, string downloadPath)
        {
            _trackerApi = trackerApi ?? throw new System.ArgumentNullException(nameof(trackerApi));
            _torrentFile = torrentFile ?? throw new System.ArgumentNullException(nameof(torrentFile));

            _peerId = ("FT" + Guid.NewGuid().ToString()).Substring(0, 20);
            _port = "9001";

            var swarmHandler = new SwarmHandler.SwarmHandler(_peerId, torrentFile.InfoHash, 10, 30, torrentFile.PieceHashes.Count);
            _fileHandler = new FileHandler.FileHandler(downloadPath, torrentFile.FileItems, torrentFile.PieceHashes,
                (int)torrentFile.PieceSize);
            _downloadStrategy = new RandomDownloadStrategy(_fileHandler, swarmHandler, torrentFile);
        }

        public async Task StartDownload()
        {
            foreach (var tracker in _torrentFile.Trackers)
            {
                var request = CreateTrackerRequest();
                request.Event = TrackerEvent.Started;
                request.Tracker = tracker;

                try
                {
                    var response = await _trackerApi.ConnectTracker(request);

                    if (!String.IsNullOrWhiteSpace(response.FailureReason))
                    {
                        throw new Exception("Failed to connect to tracker. Error: " + response.FailureReason);
                    }
                    

                    _trackersDisposable.Add(Observable.Interval(TimeSpan.FromSeconds(Math.Min(120, response.Interval))).Subscribe(async _ =>
                    {
                        var resp = await _trackerApi.ConnectTracker(CreateTrackerRequest());
                        HandleTrackerResponse(resp);
                    }));

                    HandleTrackerResponse(response);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to connect to tracker: " + tracker);
                }
                
            }


            await _fileHandler.InitializeFiles();
            _downloadStrategy.StartDownload();
        }

        public TrackerRequest CreateTrackerRequest()
        {
            return new TrackerRequest
            {
                Compact = true,
                Downloaded = _downloaded,
                Event = TrackerEvent.Regular,
                InfoHash = _torrentFile.InfoHash,
                Left = _torrentFile.Size - _downloaded,
                PeerId = _peerId,
                Port = _port,
                Tracker = _torrentFile.Trackers[0],
                Uploaded = _uploaded
            };
        }

        private void HandleTrackerResponse(TrackerResponse response)
        {
            if (!String.IsNullOrWhiteSpace(response.FailureReason))
            {
                Console.WriteLine(response.FailureReason);
                return;
            }

            foreach (var peer in response.Peers)
            {
                _downloadStrategy.AddPeer(peer);
            }
        }

        public IObservable<float> GetProgressObservable()
        {
            return _downloadStrategy.GetProgressObservable();
        }

        public void Dispose()
        {
            _trackersDisposable.Dispose();
            _downloadStrategy.Dispose();
            _fileHandler.Dispose();
            
        }
    }
}
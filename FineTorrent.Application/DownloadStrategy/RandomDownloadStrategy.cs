using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using FineTorrent.Application.FileHandler;
using FineTorrent.Application.SwarmHandler;
using FineTorrent.Domain.Models;

namespace FineTorrent.Application.DownloadStrategy
{
    public class RandomDownloadStrategy : IDownloadStrategy
    {
        private readonly Random _random = new Random();

        private readonly IFileHandler _fileHandler;
        private readonly ISwarmHandler _swarmHandler;
        private readonly TorrentFile _torrent;

        private readonly IDisposable _downloadDisposable;

        private readonly bool[] _pieceAquired;
        private readonly int[] _pieceOffsets;
        private readonly bool[] _pieceVerified;

        private int _blockCount;
        private int _blockSize;
        private int _lastBlockSize;

        private int _lastPieceBlockCount;
        private int _lastPieceBlockSize;
        private int _lastPieceSize;

        private long _downloadedTotal = 0;
        private readonly ISubject<float> _progressSubject = new Subject<float>();

        public RandomDownloadStrategy(IFileHandler fileHandler, ISwarmHandler swarmHandler, TorrentFile torrent)
        {
            _fileHandler = fileHandler ?? throw new ArgumentNullException(nameof(fileHandler));
            _swarmHandler = swarmHandler ?? throw new ArgumentNullException(nameof(swarmHandler));
            _torrent = torrent ?? throw new ArgumentNullException(nameof(torrent));

            CalculateBlocks();

            _pieceAquired = new bool[_torrent.PieceHashes.Count];
            _pieceVerified = new bool[_torrent.PieceHashes.Count];
            _pieceOffsets = new int[_torrent.PieceHashes.Count];
            for (int i = 0; i < _pieceOffsets.Length; i++)
            {
                _pieceOffsets[i] = 0;
            }
        }

        private void CalculateBlocks()
        {
            _blockSize = 1 << 14;
            _lastBlockSize = (int)_torrent.PieceSize % _blockSize;

            _blockCount = (int)_torrent.PieceSize / _blockSize;
            if (_lastBlockSize > 0) _blockCount++;

            _lastPieceSize = (int)(_torrent.Size % _torrent.PieceSize);
            _lastPieceBlockSize = _lastPieceSize % _blockSize;
            _lastPieceBlockCount = _lastPieceSize / _blockSize;
            if (_lastPieceBlockSize > 0) _lastPieceBlockCount++;
        }


        public void AddPeer(string ipAddress)
        {
            _swarmHandler.AddPeer(ipAddress.Split(':')[0], Int32.Parse(ipAddress.Split(':')[1]));
        }

        public IObservable<float> GetProgressObservable()
        {
            return _progressSubject;
        }

        public void StartDownload()
        {
            new Thread(t =>
            {
                _swarmHandler.GetPiecesObservable().Subscribe(piece =>
                {
                    Console.WriteLine("Piece obtained: " + piece.ToString());
                    if (_pieceVerified[piece.PieceIndex]) return;
                    _fileHandler.SavePiece(piece.PieceIndex, piece.Offset, piece.Data);
                    int newOffset = piece.Offset + piece.Data.Length;
                    if (_pieceOffsets[piece.PieceIndex] < newOffset)
                    {
                        _pieceOffsets[piece.PieceIndex] = newOffset;
                        _downloadedTotal += piece.Data.Length;
                        UpdateProgress();
                    }

                    if (IsPieceFinished(piece.PieceIndex, _pieceOffsets[piece.PieceIndex]))
                    {
                        if (!VerifyPiece(piece.PieceIndex))
                        {
                            _pieceOffsets[piece.PieceIndex] = 0;
                            _downloadedTotal -= _torrent.PieceSize;
                            UpdateProgress();
                        }
                        else
                        {
                            _pieceVerified[piece.PieceIndex] = true;
                        }
                    }

                });

                Observable.Interval(TimeSpan.FromMilliseconds(15)).Subscribe(_ => RequestRandomPiece());
            }).Start();
        }

        private void RequestRandomPiece()
        {
            bool available = false;
            int currRetries = 0;

            while (!available && currRetries < 15)
            {
                currRetries++;
                int pieceId = _random.Next(0, _torrent.PieceHashes.Count);
                if (_pieceVerified[pieceId]) continue;
                if (pieceId == _torrent.PieceHashes.Count)
                {
                    if (_pieceOffsets[pieceId] + _blockSize > _lastPieceSize)
                    {
                        available = _swarmHandler.RequestPiece(pieceId, _pieceOffsets[pieceId], _lastPieceBlockSize);
                        continue;
                    }
                }
                else
                {
                    if (_pieceOffsets[pieceId] + _blockSize > _torrent.PieceSize)
                    {
                        available = _swarmHandler.RequestPiece(pieceId, _pieceOffsets[pieceId], _lastBlockSize);
                        continue;
                    }
                }
                available = _swarmHandler.RequestPiece(pieceId, _pieceOffsets[pieceId], _blockSize);
            }
        }

        private bool IsPieceFinished(int pieceId, int offset)
        {
            if (pieceId == _torrent.PieceHashes.Count - 1) //Last piece
            {
                return offset >= _lastPieceSize;
            }

            return offset >= _torrent.PieceSize;
        }

        private bool VerifyPiece(int pieceId)
        {
            var pieceLength = _torrent.PieceHashes.Count - 1 == pieceId ? _lastPieceSize : _torrent.PieceSize;
            var data = _fileHandler.GetPiece(pieceId, 0, (int)pieceLength);

            var hash = SHA1.Create().ComputeHash(data);
            return _torrent.PieceHashes[pieceId].SequenceEqual(hash);
        }

        private void UpdateProgress()
        {
            _progressSubject.OnNext((_torrent.Size/_downloadedTotal)*100);
        }

        public void Dispose()
        {
            
        }
    }
}
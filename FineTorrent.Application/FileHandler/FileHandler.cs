using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using FineTorrent.Application.PeerHandling.Responses;
using FineTorrent.Domain.Models;

namespace FineTorrent.Application.FileHandler
{
    public class FileHandler : IFileHandler
    {
        private readonly string _downloadPath;
        private readonly List<FileItem> _torrentFiles;
        private readonly List<byte[]> _pieceHashes;
        private readonly int _pieceSize;
        private readonly ISubject<PieceResponse> _pieceResponseSubject = new Subject<PieceResponse>();

        private readonly ISubject<float> _progressSubject = new Subject<float>();
        private float _totalSize = 1;
        private float _currentSize = 0;

        private object[] _locks;

        public FileHandler(string downloadPath, List<FileItem> torrentFiles, List<byte[]> pieceHashes, int pieceSize)
        {
            if (string.IsNullOrWhiteSpace(downloadPath))
            {
                throw new ArgumentException("message", nameof(downloadPath));
            }

            _downloadPath = downloadPath;
            _torrentFiles = torrentFiles ?? throw new ArgumentNullException(nameof(torrentFiles));
            _pieceHashes = pieceHashes ?? throw new ArgumentNullException(nameof(pieceHashes));
            _pieceSize = pieceSize;

            _pieceResponseSubject.Subscribe(piece => SavePiece(piece));

            _locks = new object[_torrentFiles.Count];
            for (int i = 0; i < _locks.Length; i++) _locks[i] = new object();

            _totalSize = _torrentFiles.Select(file => file.Size).Sum();
        }

        public async Task InitializeFiles()
        {
            foreach (var fileItem in _torrentFiles)
            {
                
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_downloadPath, fileItem.Path)));
                using (var fs = new FileStream(Path.Combine(_downloadPath, fileItem.Path), FileMode.Create, FileAccess.Write))
                {
                    long sizeLeft = fileItem.Size;
                    int offset = 0;
                    while (sizeLeft > 10000)
                    {
                        await fs.WriteAsync(new byte[10000], 0, 10000);
                        sizeLeft -= 10000;
                        offset += 10000;
                    }

                    if (sizeLeft > 0)
                    {
                        await fs.WriteAsync(new byte[sizeLeft], 0, (int)sizeLeft);
                    }
                }

            }
        }

        public byte[] GetPiece(int pieceId, int offset, int length)
        {
            var data = new byte[length];
            int dataOffset = 0;
            long currOffset = _pieceSize * pieceId + offset;

            int fileId = 0;
            foreach (var fileItem in _torrentFiles)
            {
                if (currOffset >= fileItem.Size)
                {
                    currOffset -= fileItem.Size;
                    fileId++;
                    continue;
                }

                var path = Path.Combine(_downloadPath, fileItem.Path);

                if (currOffset > 0)
                {
                    var fileBytes = fileItem.Size - currOffset;
                    if (length > fileBytes)
                    {
                        var fileData = ReadData(path, (int)currOffset, (int) fileBytes, fileId);
                        fileData.CopyTo(data, dataOffset);
                        dataOffset += fileData.Length;
                        length -= (int)fileBytes;
                        currOffset = 0;
                    }
                    else
                    {
                        var fileData = ReadData(path, (int)currOffset, length, fileId);
                        fileData.CopyTo(data, dataOffset);
                        break;
                    }
                }
                else
                {
                    if (length > fileItem.Size)
                    {
                        ReplaceData(path, 0, data.Take((int)fileItem.Size).ToArray(), fileId);
                        data = data.Skip((int)fileItem.Size).ToArray();

                        var fileData = ReadData(path, 0, (int)fileItem.Size, fileId);
                        fileData.CopyTo(data, dataOffset);
                        dataOffset += fileData.Length;
                        length -= fileData.Length;
                    }
                    else
                    {
                        var fileData = ReadData(path, 0, length, fileId);
                        fileData.CopyTo(data, dataOffset);
                        break;
                    }
                }

                fileId++;
            }

            return data;
        }

        public IObservable<int> GetPieceCompletedObservable()
        {
            throw new NotImplementedException();
        }

        public void SavePiece(int pieceId, int offset, byte[] data)
        {
            _pieceResponseSubject.OnNext(new PieceResponse
            {
                Data = data,
                Offset = offset,
                PieceIndex = pieceId
            });
        }

        private void SavePiece(PieceResponse pieceResponse)
        {
            _currentSize += pieceResponse.Data.Length;
            _progressSubject.OnNext((_currentSize/_totalSize)*100);

            var data = pieceResponse.Data;
            long currOffset = _pieceSize * pieceResponse.PieceIndex + pieceResponse.Offset;

            int fileId = 0;
            foreach (var fileItem in _torrentFiles)
            {
                if (currOffset >= fileItem.Size)
                {
                    currOffset -= fileItem.Size;
                    fileId++;
                    continue;
                }

                var path = Path.Combine(_downloadPath, fileItem.Path);

                if (currOffset > 0)
                {
                    var fileBytes = fileItem.Size - currOffset;
                    if (data.Length > fileBytes)
                    {
                        ReplaceData(path, (int)currOffset, data.Take((int)fileBytes).ToArray(), fileId);
                        data = data.Skip((int)fileItem.Size).ToArray();
                        currOffset = 0;
                    }
                    else
                    {
                        ReplaceData(path, (int)currOffset, data, fileId);
                        break;
                    }
                }
                else
                {
                    if (data.Length > fileItem.Size)
                    {
                        ReplaceData(path, 0, data.Take((int)fileItem.Size).ToArray(), fileId);
                        data = data.Skip((int) fileItem.Size).ToArray();
                    }
                    else
                    {
                        ReplaceData(path, 0, data, fileId);
                        break;
                    }
                }

                fileId++;
            }

        }

        private byte[] ReadData(string fileName, int offset, int length, int fileId)
        {
            byte[] data = new byte[length];
            lock (_locks[fileId])
            {
                using (BinaryReader reader = new BinaryReader(new FileStream(fileName, FileMode.Open)))
                {
                    reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                    reader.Read(data, 0, length);
                }

            }

            return data;
        }

        private void ReplaceData(string filename, int offset, byte[] data, int fileId)
        {
            lock (_locks[fileId])
            {
                try
                {
                    using (Stream stream = File.Open(filename, FileMode.Open))
                    {
                        stream.Position = offset;
                        stream.Write(data, 0, data.Length);
                    }

                }
                catch (Exception)
                {

                }
            }
        }

        public IObservable<float> GetProgressObservable()
        {
            return _progressSubject;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
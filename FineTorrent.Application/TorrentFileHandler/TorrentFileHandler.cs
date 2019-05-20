using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FineTorrent.Application.Decoders;
using FineTorrent.Domain.Models;

namespace FineTorrent.Application.TorrentFileHandler
{
    public class TorrentFileHandler
    {
        private readonly BDecoder _decoder;
        public TorrentFileHandler(BDecoder decoder)
        {
            _decoder = decoder ?? throw new System.ArgumentNullException(nameof(decoder));
        }
        public TorrentFile LoadFromFilePath(string torrentFilePath)
        {
            var decoded = _decoder.Decode(((IEnumerable<byte>)File.ReadAllBytes(torrentFilePath)).GetEnumerator());

            var name = Path.GetFileNameWithoutExtension(torrentFilePath);

            var torrent = ParseTorrentObject(decoded);
            torrent.Name = name;

            return torrent;
        }

        public TorrentFile ParseTorrentObject(object obj)
        {
            ValidateTorrentObject(obj);

            var torrent = new TorrentFile();

            var arguments = (Dictionary<string, object>) obj;

            torrent.Trackers = new List<string>();
            torrent.Trackers.Add(ByteObjectToUTF8(arguments["announce"]));
            if (arguments.ContainsKey("announce-list"))
            {
                foreach (var trackerList in arguments["announce-list"] as List<object>)
                {
                    var list = trackerList as List<object>;
                    torrent.Trackers.Add(ByteObjectToUTF8(list[0]));
                }
            }

            var info = (Dictionary<string, object>)arguments["info"];

            torrent.FileItems = new List<FileItem>();

            if (info.ContainsKey("files")) //Can be single, can be many
            {
                long offset = 0;
                foreach (var file in info["files"] as List<object>)
                {
                    torrent.FileItems.Add(ParseMultiFileItemObjects(file, ref offset));

                }
            }
            else
            {
                torrent.FileItems.Add(ParseSingleFileItemObject(info));
            }

            torrent.Size = Enumerable.Sum(torrent.FileItems.Select(file => file.Size));
            torrent.InfoHash = SHA1.Create().ComputeHash(_decoder.Encode(info));

            torrent.PieceSize = Convert.ToInt32(info["piece length"]);

            var pieceHashes = (byte[]) info["pieces"];
            torrent.PieceHashes = new List<byte[]>();

            for (int i = 0; i < pieceHashes.Length; i+=20)
            {
                var hash = new byte[20];
                Array.Copy(pieceHashes, i, hash, 0, 20);
                torrent.PieceHashes.Add(hash);
            }


            //optional arguments

            if (arguments.ContainsKey("comment")) torrent.Comment = ByteObjectToUTF8(arguments["comment"]);
            if (arguments.ContainsKey("created by")) torrent.CreatedBy = ByteObjectToUTF8(arguments["created by"]);
            if (arguments.ContainsKey("creation date")) torrent.CreationDate = UnixTimeStampToDateTime(Convert.ToDouble(arguments["creation date"]));
            if (arguments.ContainsKey("encoding")) torrent.Encoding = Encoding.GetEncoding(ByteObjectToUTF8(arguments["encoding"]));


            return torrent;
        }

        public FileItem ParseSingleFileItemObject(object obj)
        {

            var dict = obj as Dictionary<string, object>;
            var path = ByteObjectToUTF8(dict["name"]);

            var fileItem = new FileItem {Path = path, Size = (long) dict["length"], Offset = 0};

            return fileItem;
        }

        public FileItem ParseMultiFileItemObjects(object obj, ref long offset)
        {
            var dict = obj as Dictionary<string, object>;
            var path = String.Join(Path.DirectorySeparatorChar.ToString(),
                ((List<object>)dict["path"]).Select(s => ByteObjectToUTF8(s)));

            var fileItem = new FileItem { Path = path, Size = (long)dict["length"], Offset = offset };
            offset += fileItem.Size;

            return fileItem;
        }

        private void ValidateTorrentObject(object obj)
        {
            Dictionary<string, object> arguments = (Dictionary<string, object>)obj;

            if (arguments == null) throw new ArgumentException("Invalid torrent object");
            if (!arguments.ContainsKey("info")) throw new ArgumentException("Missing info arguments");
            if (!arguments.ContainsKey("announce")) throw new ArgumentException("Missing announce argument");
        }

        private DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        private string ByteObjectToUTF8(object obj)
        {
            var bytes = obj as byte[];
            if (bytes == null) throw new ArgumentException("Invalid byte object");

            return Encoding.UTF8.GetString(bytes);
        }

    }
}
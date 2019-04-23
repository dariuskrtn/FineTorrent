using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public Torrent LoadFromFilePath(string torrentFilePath)
        {
            var decoded = _decoder.Decode(((IEnumerable<byte>)File.ReadAllBytes(torrentFilePath)).GetEnumerator());

            var name = Path.GetFileNameWithoutExtension(torrentFilePath);


        }

        public Torrent ParseTorrentObject(object obj)
        {
            ValidateTorrentObject(obj);

            var torrent = new Torrent();

            var arguments = (Dictionary<string, object>) obj;

            torrent.Trackers = new List<string>();
            torrent.Trackers.Add(ByteObjectToUTF8(arguments["announce"])); 

            var info = (Dictionary<string, object>)arguments["info"];

            torrent.FileItems = new List<FileItem>();

            if (info.ContainsKey("name")) //Can be single, can be many
            {
                torrent.FileItems.Add(ParseSingleFileItemObject(info));
            }
            else
            {
                long offset = 0;
                foreach (var file in info["files"] as List<object>)
                {
                    torrent.FileItems.Add(ParseMultiFileItemObjects(file, ref offset));

                }
            }

            //TODO: get piece length, etc.


        }

        public FileItem ParseSingleFileItemObject(object obj)
        {
            
            var dict = obj as Dictionary<string, object>;
            var path = String.Join(Path.DirectorySeparatorChar.ToString(),
                ((List<object>) dict["path"]).Select(s => ByteObjectToUTF8(s)));

            var fileItem = new FileItem { Path = path, Size = (long)dict["length"], Offset = 0 };

            return fileItem;
        }

        public FileItem ParseMultiFileItemObjects(object obj, ref long offset)
        {
            var dict = obj as Dictionary<string, object>;
            var fileItem = new FileItem { Path = ByteObjectToUTF8(dict["name"]), Size = (long)dict["length"], Offset = offset };
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

        private string ByteObjectToUTF8(object obj)
        {
            var bytes = obj as byte[];
            if (bytes == null) throw new ArgumentException("Invalid byte object");

            return Encoding.UTF8.GetString(bytes);
        }

    }
}
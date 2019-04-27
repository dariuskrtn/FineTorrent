using System;
using FineTorrent.Application.TorrentFileHandler;

namespace FineTorrent
{
    class Program
    {
        
        static void Main(string[] args)
        {
            var iocContainer = new IocContainer();

            var torrentHandler = iocContainer.Get<TorrentFileHandler>();

            var torrent = torrentHandler.LoadFromFilePath("C:\\Users\\Darius\\Desktop\\torrentfile.torrent");

        }
    }
}

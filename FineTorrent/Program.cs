using System;
using System.Threading;
using FineTorrent.Application;
using FineTorrent.Application.TorrentFileHandler;

namespace FineTorrent
{
    class Program
    {
        
        static void Main(string[] args)
        {
            new Thread(_ =>
            {
                var torrent = new Torrent("C:\\Users\\Darius\\Downloads\\The.Simpsons.S29.Springfield.of.Dreams.The.Legend.of.Homer.Simpson.720p.FOX.WEB-DL.AAC2.0.x264-BTN.torrent", "C:\\Users\\Darius\\Desktop");
                torrent.GetProgressObservable().Subscribe(percent => Console.WriteLine("----------------------------------------------Progress: " + percent));
                torrent.StartDownload().ConfigureAwait(false).GetAwaiter().GetResult();
            }).Start();
            while (true) Thread.Sleep(50000);
        }
    }
}

using System;
using System.Threading;
using FineTorrent.Application;

namespace FineTorrent.App
{
    class Program
    {
        static void Main(string[] args)
        {
            var torrent = new Torrent("C:\\Users\\Darius\\Downloads\\[Torrent.LT]_Filmai-Eng-La-La-Land-2016-DVDScr-XVID-AC3-HQ-Hive-CM8.torrent", "C:\\Users\\Darius\\Desktop");
            torrent.GetProgressObservable().Subscribe(percent => Console.WriteLine(percent));
            torrent.StartDownload().ConfigureAwait(false).GetAwaiter().GetResult();
            Thread.Sleep(500000);
        }
    }
}

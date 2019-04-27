using FineTorrent.Application.TorrentFileHandler;
using Ninject;
using System;
using System.Collections.Generic;
using System.Text;
using FineTorrent.Application.Decoders;

namespace FineTorrent
{
    class IocContainer : IDisposable
    {
        private readonly StandardKernel _standardKernel;

        public IocContainer()
        {
            //TODO: Setup dependencies
            _standardKernel = new StandardKernel();

            _standardKernel.Bind<BDecoder>().ToMethod(_ => new BDecoder()).InSingletonScope();
            _standardKernel.Bind<TorrentFileHandler>().ToMethod(_ => new TorrentFileHandler(Get<BDecoder>())).InSingletonScope();

        }

        public T Get<T>()
        {
            return _standardKernel.Get<T>();
        } 

        public void Dispose()
        {
            _standardKernel?.Dispose();
        }
    }
}

using Ninject;
using System;
using System.Collections.Generic;
using System.Text;

namespace FineTorrent
{
    class IocContainer : IDisposable
    {
        private readonly StandardKernel _standardKernel;

        public IocContainer()
        {
            //TODO: Setup dependencies
            _standardKernel = new StandardKernel();

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

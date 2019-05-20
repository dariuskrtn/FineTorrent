using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using FineTorrent.Application.PeerHandling;
using FineTorrent.Application.PeerHandling.Responses;

namespace FineTorrent.Application.SwarmHandler
{
    //TODO: Implement automatic upload
    public class SwarmHandler : ISwarmHandler
    {
        private int _counter = 0;
        private int _unchoked = 0;

        private readonly int _unchokedLimit;
        private readonly int _connectionsLimit;

        private readonly int _pieceCount;

        private readonly ConcurrentQueue<Peer> _pendingPeers = new ConcurrentQueue<Peer>();
        private readonly ConcurrentDictionary<int, IPeerHandler> _peerHandlers = new ConcurrentDictionary<int, IPeerHandler>();
        private readonly ConcurrentDictionary<int, Peer> _peers = new ConcurrentDictionary<int, Peer>();
        private readonly ConcurrentDictionary<int, CompositeDisposable> _peerDisposables = new ConcurrentDictionary<int, CompositeDisposable>();

        private readonly string _peerId;
        private readonly byte[] _infoHash;

        private bool[] _isHandshakeSent = new bool[10024];

        private readonly ISubject<PieceResponse> _piecesSubject = new Subject<PieceResponse>();

        private readonly ConcurrentQueue<RequestResponse> _requests = new ConcurrentQueue<RequestResponse>();

        private IDisposable _disposable;

        private bool[] _interestedSent = new bool[4196];

        public SwarmHandler(string peerId, byte[] infoHash, int unchokedLimit, int connectionsLimit, int pieceCount)
        {
            if (string.IsNullOrWhiteSpace(peerId))
            {
                throw new ArgumentException("message", nameof(peerId));
            }

            _peerId = peerId;
            _infoHash = infoHash ?? throw new ArgumentNullException(nameof(infoHash));
            _unchokedLimit = unchokedLimit;
            _connectionsLimit = connectionsLimit;
            _pieceCount = pieceCount;

            _disposable = Observable.Interval(TimeSpan.FromSeconds(10)).Subscribe(async _ => await UpdateConnectedUsers());
        }

        public void AddPeer(string address, int port)
        {
            _pendingPeers.Enqueue(new Peer {Address = address, Port = port});
        }

        private async Task ConnectToPeer(string address, int port)
        {
            var peerHandler = new PeerHandler(null, _peerId, _infoHash);
            var peer = new Peer();
            peer.Pieces = new bool[_pieceCount];
            var id = GetNextId();

            _peers.TryAdd(id, peer);
            _peerHandlers.TryAdd(id, peerHandler);

            await InitializePeerHandler(peerHandler, address, port, id);
        }

        private async Task InitializePeerHandler(PeerHandler peerHandler, string address, int port, int peerId)
        {
            var disposable = new CompositeDisposable();
            _peerDisposables[peerId] = disposable;

            disposable.Add(peerHandler.GetPiecesObservable().Subscribe(piece => _piecesSubject.OnNext(piece)));
            disposable.Add(peerHandler.GetPeerStateObservable().Subscribe(async peerState => await HandlePeerStateUpdated(peerState, peerId)));
            disposable.Add(peerHandler.GetRequestObservable().Subscribe(request => _requests.Enqueue(request)));
            disposable.Add(peerHandler.GetBitfieldObservable().Subscribe(bitfield => HandlePeerBitfield(bitfield, peerId)));
            disposable.Add(peerHandler.GetHaveObservable().Subscribe(pieceId => HandlePeerHave(pieceId, peerId)));
            disposable.Add(Observable.Interval(TimeSpan.FromSeconds(120)).Subscribe(async _ =>
            {
                if (_peerHandlers.ContainsKey(peerId))
                    try
                    {
                        await _peerHandlers[peerId].SendKeepAlive();
                    } catch (Exception) { }
            }));

            await peerHandler.Connect(address, port);

        }

        private async Task UpdateConnectedUsers()
        {
            while (_connectionsLimit > _peers.Count && _pendingPeers.Count > 0)
            {
                _pendingPeers.TryDequeue(out var peer);
                if (peer == null) continue;
                await ConnectToPeer(peer.Address, peer.Port);
            }
        }

        private async Task HandlePeerStateUpdated(PeerState peerState, int peerId)
        {
            Console.WriteLine(peerId + ": " + peerState.ToString());

            if (!_peerHandlers.ContainsKey(peerId)) return;
            Peer peer;
            IPeerHandler peerHandler;
            try
            {
                peer = _peers[peerId];
                peerHandler = _peerHandlers[peerId];
            }
            catch (Exception)
            {
                return;
            }

            if (!peerState.IsConnected)
            {
                _peerDisposables[peerId].Dispose();
                _peers.TryRemove(peerId, out var peerr);
                peerHandler?.Dispose();
                _peerHandlers.TryRemove(peerId, out var handler);
                return;
            }
            else
            {
                if (!_isHandshakeSent[peerId])
                {
                    _isHandshakeSent[peerId] = true;
                    await peerHandler?.SendHandshake();
                }
            }

            if (peerState.IsChoking)
            {
                if (!peer.IsChoking)
                {
                    //Starts choking. Fucker. Update our pieceOwners because this fucker does not share.
                    peer.IsChoking = true;
                }
            }
            else
            {
                if (peer.IsChoking)
                {
                    //No longer choking. Ask for any stuff we would like to have.
                    peer.IsChoking = false;
                }
            }

            if (peerState.IsInterested)
            {
                if (!peer.IsInterested)
                {
                    //Wants something. Maybe we can unchoke?
                    peer.IsInterested = true;
                    if (_unchokedLimit < _unchoked)
                    {
                        peerHandler?.SendUnchoke();
                        peer.IsChoked = false;
                        Interlocked.Increment(ref _unchoked);
                    }
                }
            }
            else
            {
                if (peer.IsInterested)
                {
                    //No longer wants something. We can choke him if not already
                    peer.IsInterested = false;
                    if (!peer.IsChoked)
                    {
                        peerHandler?.SendChoke();
                        peer.IsChoked = true;
                        Interlocked.Decrement(ref _unchoked);
                    }
                }
            }
        }
        private void HandlePeerBitfield(bool[] bitfield, int peerId)
        {
            for (int i = 0; i < bitfield.Length; i++)
            {
                if (bitfield[i]) HandlePeerHave(i, peerId);
            }
        }

        private void HandlePeerHave(int pieceId, int peerId)
        {
            if (!_peerHandlers.ContainsKey(peerId)) return;

            if (pieceId >= _pieceCount) return;
            if (_peers.ContainsKey(peerId))
            {
                var peer = _peers[peerId];
                peer.Pieces[pieceId] = true;
                if (peer.IsChoking)
                {
                    if (!_interestedSent[peerId])
                    {
                        _peerHandlers[peerId]?.SendInterested();
                        _interestedSent[peerId] = true;
                    }
                }
            }
        }

        public IObservable<PieceResponse> GetPiecesObservable()
        {
            return _piecesSubject;
        }

        public bool RequestPiece(int pieceIndex, int offset, int length)
        {

            var peers = _peers.Where(peer => !peer.Value.IsChoking && peer.Value.Pieces[pieceIndex]).ToArray();
            if (peers.Length <= 0) return false;
            var rand = new Random((int)DateTime.Now.ToFileTimeUtc());
            

            _peerHandlers[peers[rand.Next(0, peers.Length-1)].Key].SendRequest(pieceIndex, offset, length);

            return true;
        }

        private int GetNextId()
        {
            return Interlocked.Increment(ref _counter);
        }

        public void Dispose()
        {
            _disposable?.Dispose();
            foreach (var keyVal in _peerDisposables)
            {
                keyVal.Value?.Dispose();
            }

            foreach (var keyVal in _peerHandlers)
            {
                keyVal.Value.Dispose();
            }
        }
    }
}
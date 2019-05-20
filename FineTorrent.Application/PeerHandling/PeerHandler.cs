using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using BitConverter;
using FineTorrent.Application.PeerHandling.Responses;
using FineTorrent.Domain.Enums;

namespace FineTorrent.Application.PeerHandling
{
    public class PeerHandler : IPeerHandler
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        
        private readonly ISubject<PieceResponse> _piecesSubject;
        private readonly ISubject<PeerState> _peerStateSubject;
        private readonly ISubject<RequestResponse> _requestSubject;
        private readonly ISubject<bool[]> _bitfieldSubject;
        private readonly ISubject<int> _haveSubject;

        private readonly string _peerId;
        private readonly byte[] _infoHash;

        private readonly byte[] _readBuffer = new byte[1024]; //Magic value :)
        private readonly Queue<byte> _unprocessedData = new Queue<byte>();

        private readonly PeerState _peerState = new PeerState();

        private DateTime _lastActive = DateTime.Now;

        private bool _isDisposed = false;

        public PeerHandler(TcpClient tcpClient, string peerId, byte[] infoHash)
        {
            if (string.IsNullOrWhiteSpace(peerId))
            {
                throw new ArgumentException("message", nameof(peerId));
            }

            _tcpClient = tcpClient;
            _peerId = peerId;
            _infoHash = infoHash ?? throw new ArgumentNullException(nameof(infoHash));
            _piecesSubject = new Subject<PieceResponse>();
            _peerStateSubject = new Subject<PeerState>();
            _bitfieldSubject = new Subject<bool[]>();
            _haveSubject = new Subject<int>();
            _requestSubject = new Subject<RequestResponse>();

            if (_tcpClient != null)
            {
                _peerState.IsConnected = true;
                _peerStateSubject.OnNext(_peerState);
                try
                {
                    ReadFromPeer(null);
                }
                catch (Exception)
                {
                    Disconnect();
                }
            }
        }

        public async Task Connect(string address, int port)
        {
            _tcpClient = new TcpClient();
            try
            {
                await _tcpClient.ConnectAsync(address, port);
                _stream = _tcpClient.GetStream();
                _peerState.IsConnected = true;
                _peerStateSubject.OnNext(_peerState);
            }
            catch (Exception)
            {
                Disconnect();
            }

            try
            {
                _stream.BeginRead(_readBuffer, 0, _readBuffer.Length, new AsyncCallback(ReadFromPeer), null);
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        public IObservable<PieceResponse> GetPiecesObservable()
        {
            return _piecesSubject;
        }

        public IObservable<PeerState> GetPeerStateObservable()
        {
            return _peerStateSubject;
        }

        public IObservable<RequestResponse> GetRequestObservable()
        {
            return _requestSubject;
        }

        public IObservable<bool[]> GetBitfieldObservable()
        {
            return _bitfieldSubject;
        }

        public IObservable<int> GetHaveObservable()
        {
            return _haveSubject;
        }

        public async Task SendHandshake()
        {
            Console.WriteLine("Sending Handshake");
            byte length = 19;
            var str = "BitTorrent protocol";
            byte zero = 0;

            var bytes = new byte[68];

            bytes[0] = length;
            byte[] byteArray = Encoding.UTF8.GetBytes(str.ToCharArray());
            byteArray.CopyTo(bytes, 1);
            for (int i = 20; i < 28; i++)
            {
                bytes[i] = zero;
            }
            _infoHash.CopyTo(bytes, 28);
            byte[] byteArray2 = Encoding.UTF8.GetBytes(_peerId.ToCharArray());
            byteArray2.CopyTo(bytes, 48);

            try
            {
                await _stream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        public async Task SendKeepAlive()
        {
            Console.WriteLine("Sending KeepAlive");
            await SendMessage(new byte[0]);
        }

        public async Task SendChoke()
        {
            Console.WriteLine("Sending Choke");
            await SendMessage(new byte[] {MessageTypes.ChokeMessage});
        }

        public async Task SendUnchoke()
        {
            Console.WriteLine("Sending Unchoke");
            await SendMessage(new byte[] { MessageTypes.UnchokeMessage });
        }

        public async Task SendInterested()
        {
            Console.WriteLine("Sending Interested");
            await SendMessage(new byte[] { MessageTypes.InterestedMessage });
        }

        public async Task SendNotInterested()
        {
            await SendMessage(new byte[] { MessageTypes.NotInterestedMessage });
        }

        public async Task SendHave(int pieceIndex)
        {
            Console.WriteLine("Sending Have");
            var bytes = new byte[5];
            bytes[0] = MessageTypes.HaveMessage;
            EndianBitConverter.BigEndian.GetBytes(pieceIndex).CopyTo(bytes, 1);

            await SendMessage(bytes);
        }

        public async Task SendBitField(bool[] bits)
        {

            int byteCount = (bits.Length + 7) / 8; //Round to bigger
            var bytes = new byte[byteCount+1]; //one more for message type
            bytes[0] = MessageTypes.BitfieldMessage;

            bits.CopyTo(bytes, 1);

            await SendMessage(bytes);
        }

        public async Task SendRequest(int pieceIndex, int offset, int length)
        {
            Console.WriteLine("Sending Request");
            var bytes = new byte[13];
            bytes[0] = MessageTypes.RequestMessage;
            EndianBitConverter.BigEndian.GetBytes(pieceIndex).CopyTo(bytes, 1);
            EndianBitConverter.BigEndian.GetBytes(offset).CopyTo(bytes, 5);
            EndianBitConverter.BigEndian.GetBytes(length).CopyTo(bytes, 9);

            await SendMessage(bytes);
        }

        public async Task SendPiece(int pieceIndex, int offset, byte[] data)
        {
            Console.WriteLine("Sending Piece");
            var bytes = new byte[9 + data.Length];
            bytes[0] = MessageTypes.PieceMessage;
            EndianBitConverter.BigEndian.GetBytes(pieceIndex).CopyTo(bytes, 1);
            EndianBitConverter.BigEndian.GetBytes(offset).CopyTo(bytes, 5);
            data.CopyTo(bytes, 9);

            await SendMessage(bytes);
        }

        private async Task SendMessage(byte[] message)
        {
            await SendMessage(message, message.Length);
        }

        private async Task SendMessage(byte[] message, int length)
        {
            var bytes = new byte[message.Length + 4];
            EndianBitConverter.BigEndian.GetBytes(length).CopyTo(bytes, 0);
            message.CopyTo(bytes, 4);

            try
            {
                await _stream.WriteAsync(bytes, 0, bytes.Length);
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        private void ReadFromPeer(IAsyncResult ar)
        {
            if (_isDisposed) return;

            if (DateTime.Now - _lastActive > TimeSpan.FromMinutes(4)) //If nothing is received after some time, this peer is probably unresponsive.
            {
                Disconnect();
                return;
            }

            int bytes = 0;
            try
            {
                bytes = _stream.EndRead(ar);
            }
            catch (Exception)
            {
                Disconnect();
                return;
            }

            for (int i = 0; i < bytes; i++)
            {
                _unprocessedData.Enqueue(_readBuffer[i]);
            }

            var msgLength = GetMessageLength();

            while (msgLength <= _unprocessedData.Count) //Handle all received messages
            {
                var message = new byte[msgLength];
                for (int i = 0; i < msgLength; i++)
                {
                    message[i] = _unprocessedData.Dequeue();
                }
                HandleReceivedMessage(message);

                msgLength = GetMessageLength();
            }

            try
            {
                _stream.BeginRead(_readBuffer, 0, _readBuffer.Length, new AsyncCallback(ReadFromPeer), null);
            }
            catch (Exception)
            {
                Disconnect();
            }

        }

        private int GetMessageLength()
        {
            if (!_peerState.IsHandshakeReceived)
            {
                return 68;
            }
            if (_unprocessedData.Count < 4) return Int32.MaxValue;

            var lengthBytes = new byte[4];

            int curr = 0;
            foreach (var b in _unprocessedData)
            {
                if (curr >= 4) break;

                lengthBytes[curr] = b;

                curr++;
            }

            var length = EndianBitConverter.BigEndian.ToInt32(lengthBytes, 0);
            return length + 4; // 4 more for lengthBytes
        }

        private void HandleReceivedMessage(byte[] message)
        {
            _lastActive = DateTime.Now;

            var msgType = GetMessageType(message);

            Console.WriteLine("Msg received:" + msgType.ToString());

            switch (msgType)
            {
                case MessageType.Handshake:
                    HandleHandshakeReceived();
                    break;
                case MessageType.Choke:
                    HandleChokeReceived(true);
                    break;
                case MessageType.Unchoke:
                    HandleChokeReceived(false);
                    break;
                case MessageType.Interested:
                    HandleInterestedReceived(true);
                    break;
                case MessageType.NotInterested:
                    HandleInterestedReceived(false);
                    break;
                case MessageType.Have:
                    HandleHaveReceived(EndianBitConverter.BigEndian.ToInt32(message, 5));
                    break;
                case MessageType.Bitfield:
                    var bytefield = message.Skip(5).ToArray();
                    var bitArray = new BitArray(bytefield);
                    var bitfield = new bool[bytefield.Length*8];

                    int id = 0;
                    for (int i = 0; i < bytefield.Length; i++)
                    {
                        for (int j = 7; j >= 0; j--)
                        {
                            bitfield[id] = bitArray[i * 8 + j];
                            id++;
                        }
                    }
                    HandleBitfieldReceived(bitfield);
                    break;
                case MessageType.Request:
                    HandleRequestReceived(
                        EndianBitConverter.BigEndian.ToInt32(message, 5),
                        EndianBitConverter.BigEndian.ToInt32(message, 9),
                        EndianBitConverter.BigEndian.ToInt32(message, 13)
                        );
                    break;
                case MessageType.Piece:
                    var pieceIndex = EndianBitConverter.BigEndian.ToInt32(message, 5);
                    var offset = EndianBitConverter.BigEndian.ToInt32(message, 9);
                    HandlePieceReceived(pieceIndex, offset, message.Skip(13).ToArray());
                    break;
            }
        }

        private void HandleHandshakeReceived()
        {
            _peerState.IsHandshakeReceived = true;
            _peerStateSubject.OnNext(_peerState);
        }

        private void HandlePieceReceived(int pieceIndex, int offset, byte[] data)
        {
            _piecesSubject.OnNext(new PieceResponse
            {
                Data = data,
                Offset = offset,
                PieceIndex = pieceIndex
            });
        }

        private void HandleRequestReceived(int pieceIndex, int offset, int length)
        {
            _requestSubject.OnNext(new RequestResponse
            {
                Length = length,
                Offset = offset,
                PieceIndex = pieceIndex
            });
        }

        private void HandleBitfieldReceived(bool[] bitfield)
        {
            _bitfieldSubject.OnNext(bitfield);
        }

        private void HandleHaveReceived(int pieceIndex)
        {
            _haveSubject.OnNext(pieceIndex);
        }

        private MessageType GetMessageType(byte[] message)
        {
            if (!_peerState.IsHandshakeReceived)
            {
                return MessageType.Handshake;
            }

            if (message.Length == 4) return MessageType.KeepAlive;

            if (message.Length > 4 && Enum.IsDefined(typeof(MessageType), (int)message[4]))
                return (MessageType)message[4];

            return MessageType.Unknown;
        }

        private void HandleChokeReceived(bool isChoking)
        {
            _peerState.IsChoking = isChoking;
            _peerStateSubject.OnNext(_peerState);
        }
        private void HandleInterestedReceived(bool isInterested)
        {
            _peerState.IsInterested = isInterested;
            _peerStateSubject.OnNext(_peerState);
        }

        public void Disconnect()
        {
            _tcpClient?.Close();
            _stream?.Close();
            _peerState.IsConnected = false;
            _peerStateSubject?.OnNext(_peerState);
            _isDisposed = true;
        }

        public void Dispose()
        {
            _stream?.Close();
            _tcpClient?.Close();
            _isDisposed = true;
        }
    }
}
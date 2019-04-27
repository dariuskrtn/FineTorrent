using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BitConverter;
using FineTorrent.Application.Decoders;
using FineTorrent.Domain.Enums;

namespace FineTorrent.Application.TrackerApi
{
    public class TrackerApi
    {
        private readonly BDecoder _decoder;

        public TrackerApi(BDecoder decoder)
        {
            _decoder = decoder ?? throw new System.ArgumentNullException(nameof(decoder));
        }

        public async Task<TrackerResponse> ConnectTracker(TrackerRequest request)
        {
            var url = SetupGetUrl(request);

            var client = new HttpClient();
            using (var response = await client.GetAsync(url))
            {
                if (response.StatusCode != HttpStatusCode.OK) throw new Exception("Failed to get tracker information.");

                var data = await response.Content.ReadAsByteArrayAsync();

                return ParseResponse(data);
            }
        }

        private string SetupGetUrl(TrackerRequest request)
        {
            return String.Format("{0}?info_hash={1}&peer_id={2}&port={3}&uploaded={4}&downloaded={5}&left={6}&event={7}&compact={8}",
                request.Tracker, request.InfoHash,
                request.PeerId, request.Port,
                request.Uploaded, request.Downloaded, request.Left,
                Enum.GetName(typeof(TrackerEvent), request.Event).ToLower());
        }

        private TrackerResponse ParseResponse(byte[] data)
        {
            var response = new TrackerResponse();

            Dictionary<string, object> info = _decoder.Decode((IEnumerator<byte>)data.GetEnumerator()) as Dictionary<string, object>;

            response.Interval = (long) info["interval"];
            response.Complete = (long) info["complete"];
            response.Incomplete = (long) info["incomplete"];
            response.Downloaded = (long) info["downloaded"];

            response.Peers = new List<string>();

            var peerInfo = (byte[]) info["peers"];
            for (int i = 0; i < peerInfo.Length / 6; i++)
            {
                int offset = i * 6;
                string address = peerInfo[offset] + "." + peerInfo[offset + 1] + "." + peerInfo[offset + 2] + "." + peerInfo[offset + 3];
                int port = EndianBitConverter.BigEndian.ToChar(peerInfo, offset + 4);
                address += ":" + port;

                response.Peers.Add(address);
            }


            return response;
        }

    }
}
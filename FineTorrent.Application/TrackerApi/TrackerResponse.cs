using System.Collections.Generic;

namespace FineTorrent.Application.TrackerApi
{
    public class TrackerResponse
    {
        public string FailureReason { get; set; }
        public long Interval { get; set; }
        public long Complete { get; set; }
        public long Incomplete { get; set; }
        public List<string> Peers { get; set; }
    }
}
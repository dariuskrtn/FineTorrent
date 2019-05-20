using System.Threading.Tasks;

namespace FineTorrent.Application.TrackerApi
{
    public interface ITrackerApi
    {
        Task<TrackerResponse> ConnectTracker(TrackerRequest request);
    }
}
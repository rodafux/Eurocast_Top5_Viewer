using System.Collections.Generic;
using System.Threading.Tasks;
using Eurocast_Top5_Viewer.Models;

namespace Eurocast_Top5_Viewer.Interfaces
{
    public interface IQualityService
    {
        Task<List<string>> GetRecentFlashUrlsAsync();
        Task<FlashDetail?> GetFlashDetailAsync(string url);
    }
}
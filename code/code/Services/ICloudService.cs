using System.Collections.Generic;
using System.Threading.Tasks;
using code.Models;

namespace code.Services
{
    public interface ICloudService
    {
        Task<IEnumerable<Cloud>> GetAllCloudsAsync();
        Task<Cloud?> GetCloudByIdAsync(int id);
        Task AddCloudAsync(Cloud cloud);
        Task UpdateCloudAsync(Cloud cloud);
        Task DeleteCloudAsync(int id);
        Task<string[]> RenderCloudAnimationAsync(Cloud cloud, int nFrames = 36, int width = 800, int height = 600);
    }
}

using code.Models;

namespace code.Utils
{
    public interface IRenderer
    {
        void Render(Camera camera, int width, int height, string filename);
    }
}


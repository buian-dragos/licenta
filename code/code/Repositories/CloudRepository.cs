using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using code.Domain;
namespace code.Repositories
{
    public class CloudRepository : ICloudRepository
    {
        private readonly List<Cloud> _clouds = new();
        private int _nextId = 1;
        public IEnumerable<Cloud> GetAll()
        {
            return _clouds;
        }
        public Cloud? GetById(int id)
        {
            return _clouds.FirstOrDefault(c => c.Id == id);
        }
        public void Add(Cloud cloud)
        {
            cloud.Id = _nextId++;
            _clouds.Add(cloud);
        }
        public void Update(Cloud cloud)
        {
            var existing = GetById(cloud.Id);
            if (existing != null)
            {
                _clouds.Remove(existing);
                _clouds.Add(cloud);
            }
        }
        public void Delete(int id)
        {
            var cloud = GetById(id);
            if (cloud != null)
            {
                _clouds.Remove(cloud);
            }
        }
    }
}

using System.Collections.Generic;
using code.Domain;

namespace code.Repositories
{
    public interface ICloudRepository
    {
        IEnumerable<Cloud> GetAll();
        Cloud? GetById(int id);
        void Add(Cloud cloud);
        void Update(Cloud cloud);
        void Delete(int id);
    }
}


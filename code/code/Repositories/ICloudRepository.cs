using System.Collections.Generic;
using code.Models;

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


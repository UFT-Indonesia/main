using Ardalis.Specification.EntityFrameworkCore;
using Erp.Core.Interfaces;

namespace Erp.Infrastructure.Persistence;

public class EfRepository<T> : RepositoryBase<T>, IRepository<T>, IReadRepository<T>
    where T : class
{
    public EfRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }
}

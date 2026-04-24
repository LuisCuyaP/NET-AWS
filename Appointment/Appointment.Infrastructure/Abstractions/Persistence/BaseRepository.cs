
using Microsoft.EntityFrameworkCore;
using Appointment.Domain.Abstractions.Persistence;

namespace Appointment.Infrastructure.Abstractions.Persistence;

 public abstract class BaseRepository<T>(DbContext context) : IBaseRepository<T> where T : class
    {
        public void Add(T entity)
        {
            context.Set<T>().Add(entity);
        }

        public IQueryable<T> Queryable() => context.Set<T>();

        public void Remove(T entity)
        {
            context.Set<T>().Remove(entity);
        }

        public void Update(T entity)
        {
            context.Set<T>().Update(entity);
        }
    }
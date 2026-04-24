namespace Appointment.Domain.Abstractions.Persistence;

public interface IBaseRepository<T> where T : class
{
    IQueryable<T> Queryable();
    void Add(T entity);
    void Update(T entity);
    void Remove(T entity);
}

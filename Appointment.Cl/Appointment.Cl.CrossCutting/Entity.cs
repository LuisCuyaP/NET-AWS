namespace Appointment.Cl.CrossCutting;

public abstract class Entity
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty", nameof(id));
        }

        Id = id;
    }

    protected Entity() { }

    public Guid Id { get; protected set; }
}

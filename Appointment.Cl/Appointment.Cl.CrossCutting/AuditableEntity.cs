namespace Appointment.Cl.CrossCutting;

public abstract class AuditableEntity : Entity
{
    public string? CreatedBy { get; private set; }
    public string? ModifiedBy { get; private set; }
    public DateTime? CreatedDate { get; private set; }
    public DateTime? ModifiedDate { get; private set; }

    protected AuditableEntity(Guid id) : base(id) { }
    protected AuditableEntity() { }

    public void AddCreateInfo(string? user, DateTime? date)
    {
        CreatedBy = user;
        CreatedDate = date;
    }

    public void AddModifyInfo(string? user, DateTime? date)
    {
        ModifiedBy = user;
        ModifiedDate = date;
    }
}

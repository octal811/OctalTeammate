namespace OctalPulse.API.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class UserOperationLockAttribute : Attribute
{
    public string? Operation { get; }

    public UserOperationLockAttribute(string? operation = null)
    {
        Operation = operation;
    }
}
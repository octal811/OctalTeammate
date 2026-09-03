namespace OctalPulse.API.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ApiAvailabilityAttribute : Attribute
{
    public string Key { get; }

    public ApiAvailabilityAttribute(string key)
    {
        Key = key;
    }
}
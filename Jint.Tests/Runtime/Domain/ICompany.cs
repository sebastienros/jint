namespace Jint.Tests.Runtime.Domain
{
    public interface ICompany
    {
        string Name { get; set; }
        string this[string key] { get; set; }
    }
}
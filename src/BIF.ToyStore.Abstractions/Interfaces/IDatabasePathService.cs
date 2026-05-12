namespace BIF.ToyStore.Core.Interfaces
{
    public interface IDatabasePathService
    {
        string ResolveDatabasePath(string configuredPath);
    }
}

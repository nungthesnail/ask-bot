namespace Core.Services.Interfaces;

public interface IResourceManager
{
    string Get(string name, params object[] format);
}

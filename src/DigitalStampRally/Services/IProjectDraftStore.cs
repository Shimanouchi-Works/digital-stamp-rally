namespace DigitalStampRally.Services;

public interface IProjectDraftStore
{
    string Save(string json);
    bool TryGet(string token, out string json);
    void Remove(string token);
}

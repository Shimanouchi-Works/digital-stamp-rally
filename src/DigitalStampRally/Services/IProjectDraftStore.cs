using DigitalStampRally.Models;

namespace DigitalStampRally.Services;

public interface IProjectDraftStore
{
    // string Save(string json);
    string Save(string json, DraftImagePayload? image = null);

    // bool TryGet(string token, out string json);
    bool TryGet(string token, out ProjectDraftPayload payload);

    void Remove(string token);
}

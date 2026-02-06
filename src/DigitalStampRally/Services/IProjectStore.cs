using DigitalStampRally.Models;

namespace DigitalStampRally.Services;


public interface IProjectStore
{
    bool TryGet(string eventId, out ProjectDto project);

    // CreateNewで作ったプロジェクトを登録する用途（後でCreateNew側に追加します）
    Task SaveAsync(ProjectDto project);
}

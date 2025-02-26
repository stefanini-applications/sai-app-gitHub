using SAI.App.GitHub.Models.Requests;
using SAI.App.GitHub.src.Models.DTO;

namespace SAI.App.GitHub;

public interface IGitHubService
{
    Task ExecuteAsync(GitHubRequest request, List<GitHubContentDTO> gitHubContents, string commitName);
}

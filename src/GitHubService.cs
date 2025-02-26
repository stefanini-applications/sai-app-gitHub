using Octokit;
using SAI.App.GitHub.Models.Requests;
using SAI.App.GitHub.src.Models.DTO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SAI.App.GitHub;

public class GitHubService(GitHubClient gitHubClient) : IGitHubService
{
    private readonly GitHubClient _client = gitHubClient;

    public async Task ExecuteAsync(
        GitHubRequest request,
        List<GitHubContentDTO> gitHubContents,
        string commitName)
    {
        try
        {
            if(gitHubContents is null || gitHubContents.Count == 0) throw new Exception("In GitHubService: File not found");

            var user = await GetUserName(request.AccessToken);

            var repo = await GetRepository(user.Login, request.Repository);

            var baseBranch = await GetBranchAndCreateIfNecessary(request.AccessToken, user.Login, repo.Name, request.BaseBranch);

            string branchNameSendCommit = baseBranch!.Name;

            if (request.CreateNewBranch)
            {
                if(string.IsNullOrEmpty(request.NewBranch)) throw new Exception("In GitHubService: When CreateNewBranch is true NewBranch is required");

                var newBranch = await CreateBranch(user.Login, repo.Name, baseBranch.Commit.Sha, request.NewBranch);
                branchNameSendCommit = newBranch!.Name;
            }

            await CreateCommit(user.Login, repo.Name, branchNameSendCommit, gitHubContents, commitName);

            if (request.CreatePullRequest && request.CreateNewBranch)
            {
                await CreatePullRequest(user.Login, repo.Name, commitName, branchNameSendCommit, baseBranch.Name);
            }
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    private async Task<Octokit.User> GetUserName(string accessToken)
    {
        var tokenAuth = new Credentials(accessToken);
        _client.Credentials = tokenAuth;

        try
        {
            return await _client.User.Current();
        }
        catch
        {
            throw new Exception("In GitHubService: Invalid access token");
        }
    }

    private async Task<Octokit.Repository> GetRepository(string userName, string repository)
    {
        try
        {
            return await _client.Repository.Get(userName, repository);
        }
        catch
        {
            throw new Exception("In GitHubService: Invalid repository");
        }
    }

    private async Task<Octokit.Branch?> GetBranch(string userName, string repository, string branch)
    {
        try
        {
            return await _client.Repository.Branch.Get(userName, repository, branch);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private async Task<Octokit.Branch?> GetBranchAndCreateIfNecessary(string token, string userName, string repository, string branch)
    {
        var branchData = await GetBranch(userName, repository, branch);
        if (branchData != null) return branchData;

        var repo = await _client.Repository.Get(userName, repository);
        var defaultBranch = repo.DefaultBranch;

        var defaultBranchData = await GetBranch(userName, repository, defaultBranch);

        if (defaultBranchData is null)
        {
            await InitializeRepository(token, userName, repository);
            defaultBranchData = await GetBranch(userName, repository, defaultBranch);
        }

        if(defaultBranchData!.Name.Equals(branch) || branch.Equals("main") || branch.Equals("master")) 
            return defaultBranchData;

        return await CreateBranch(userName, repository, defaultBranchData.Commit.Sha, branch);
    }

    private async Task<Octokit.Branch?> CreateBranch(string userName, string repository, string commitSha, string newBranch)
    {
        var newRef = new NewReference($"refs/heads/{newBranch}", commitSha);
        await _client.Git.Reference.Create(userName, repository, newRef);

        return await GetBranch(userName, repository, newBranch);
    }

    private async Task InitializeRepository(string token, string userName, string repository)
    {
        var content = Convert.ToBase64String(Encoding.UTF8.GetBytes("Arquivo inicial"));

        var request = new
        {
            message = "Initial commit",
            content,
            branch = "main"
        };

        var url = $"https://api.github.com/repos/{userName}/{repository}/contents/README.md";
        var json = JsonSerializer.Serialize(request);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SAI-APP"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"In GitHubService: Error creating initial commit, status code: {response.StatusCode}");
        }
    }


    private async Task CreateCommit(string userName, string repository, string branchName, List<GitHubContentDTO> gitHubContents, string commitName)
    {
        var branchRef = await _client.Git.Reference.Get(userName, repository, $"heads/{branchName}");
        var latestCommitSha = branchRef.Object.Sha;

        var latestCommit = await _client.Git.Commit.Get(userName, repository, latestCommitSha);

        var newTree = new NewTree { BaseTree = latestCommit.Tree.Sha };

        foreach (var item in gitHubContents)
        {
            var newBlob = new NewBlob
            {
                Content = item.Content,
                Encoding = EncodingType.Utf8
            };

            var blobResult = await _client.Git.Blob.Create(userName, repository, newBlob);

            newTree.Tree.Add(new NewTreeItem
            {
                Path = item.Path,
                Mode = Octokit.FileMode.File,
                Type = TreeType.Blob,
                Sha = blobResult.Sha
            });
        }

        var treeResult = await _client.Git.Tree.Create(userName, repository, newTree);

        var newCommit = new NewCommit(commitName, treeResult.Sha, latestCommitSha);
        var commitResult = await _client.Git.Commit.Create(userName, repository, newCommit);

        await _client.Git.Reference.Update(userName, repository, $"heads/{branchName}", new ReferenceUpdate(commitResult.Sha));
    }

    private async Task CreatePullRequest(string userName, string repository, string title, string branchOrigin, string branchDestiny)
    {
        var pullRequest = new NewPullRequest(title, branchOrigin, branchDestiny)
        {
            Body = string.Empty
        };
        await _client.PullRequest.Create(userName, repository, pullRequest);
    }
}

using System.ComponentModel.DataAnnotations;

namespace SAI.App.GitHub.Models.Requests;

public class GitHubRequest : IValidatableObject
{
    public required string AccessToken { get; set; } = null!;

    public required string Repository { get; set; } = null!;

    public required string BaseBranch { get; set; } = null!;

    public bool CreateNewBranch { get; set; } = false;

    public string? NewBranch { get; set; } = null;

    public bool CreatePullRequest { get; set; } = false;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(AccessToken)) yield return new ValidationResult("AccessToken is required", [nameof(AccessToken)]);

        if (string.IsNullOrEmpty(Repository)) yield return new ValidationResult("Repository is required", [nameof(Repository)]);

        if (string.IsNullOrEmpty(BaseBranch)) yield return new ValidationResult("BaseBranch is required", [nameof(BaseBranch)]);

        if (CreateNewBranch && string.IsNullOrEmpty(NewBranch)) yield return new ValidationResult("When CreateNewBranch is true NewBranch is required", [nameof(NewBranch)]);
    }
}

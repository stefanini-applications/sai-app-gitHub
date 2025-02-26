using System.ComponentModel.DataAnnotations;

namespace SAI.App.GitHub.src.Models.DTO;

public class GitHubContentDTO(string path, string content) : IValidatableObject
{
    public string Path { get; set; } = path;
    public string Content { get; set; } = content;
    public static GitHubContentDTO Create(string path, string content) => new(path, content);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if(string.IsNullOrEmpty(Path)) yield return new ValidationResult("Path is required", [nameof(Path)]);
    }
}

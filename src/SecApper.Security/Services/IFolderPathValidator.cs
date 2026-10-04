using System.Collections.Generic;

namespace SecApper.Security.Services;

public record ValidationResult(bool IsValid, string? ErrorMessage);

public interface IFolderPathValidator
{
    ValidationResult ValidatePath(string path, IEnumerable<string> existingRegisteredPaths);
    bool IsNtfsFileSystem(string path);
}

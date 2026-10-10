using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServicePortal.Application.Interfaces;

namespace ServicePortal.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _uploadDirectory;
    private readonly long _maxFileSizeBytes;
    private readonly HashSet<string> _allowedExtensions;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger)
    {
        _logger = logger;

        // Secure storage outside public web root (default to App_Data/Uploads)
        var configuredPath = configuration["FileStorage:UploadDirectory"];
        _uploadDirectory = !string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.Combine(AppContext.BaseDirectory, "App_Data", "Uploads");

        if (!Directory.Exists(_uploadDirectory))
        {
            Directory.CreateDirectory(_uploadDirectory);
        }

        // Max file size: 10 MB default
        _maxFileSizeBytes = configuration.GetValue("FileStorage:MaxFileSizeMB", 10) * 1024L * 1024L;

        // Configurable allowed extensions
        var extList = configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>()
            ?? new[] { ".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg", ".txt", ".csv" };

        _allowedExtensions = new HashSet<string>(extList, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && _allowedExtensions.Contains(ext);
    }

    public async Task<(string StoredFileName, long FileSizeBytes)> SaveFileAsync(
        Stream fileStream,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName);
        if (!IsAllowedExtension(originalFileName))
        {
            throw new InvalidOperationException($"File extension '{ext}' is not permitted.");
        }

        if (fileStream.Length > _maxFileSizeBytes)
        {
            throw new InvalidOperationException($"File size exceeds the allowed limit of {_maxFileSizeBytes / (1024 * 1024)} MB.");
        }

        // Generate sanitized unique stored file name
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var destinationPath = Path.Combine(_uploadDirectory, storedName);

        using (var dest = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(dest, cancellationToken);
        }

        _logger.LogInformation("Saved file {OriginalName} as {StoredName} in {Directory}", originalFileName, storedName, _uploadDirectory);

        return (storedName, fileStream.Length);
    }

    public Task<Stream> GetFileStreamAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        // Prevent path traversal attacks
        var safeFileName = Path.GetFileName(storedFileName);
        var fullPath = Path.Combine(_uploadDirectory, safeFileName);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The requested attachment was not found on disk.", safeFileName);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public bool DeleteFile(string storedFileName)
    {
        var safeFileName = Path.GetFileName(storedFileName);
        var fullPath = Path.Combine(_uploadDirectory, safeFileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return true;
        }
        return false;
    }
}

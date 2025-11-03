using Dapper;
using DiaryApi.Models;

namespace DiaryApi.Services;

public interface IImageService
{
    Task<DiaryImages?> UploadImageAsync(int entryId, IFormFile image);
    Task<byte[]?> GetImageAsync(int imageId);
    Task<bool> DeleteImageAsync(int imageId);
}

public class ImageService : IImageService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly string _uploadPath;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public ImageService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        
        if (!Directory.Exists(_uploadPath))
            Directory.CreateDirectory(_uploadPath);
    }

    public async Task<DiaryImages?> UploadImageAsync(int entryId, IFormFile image)
    {
        if (image == null || image.Length == 0)
            throw new ArgumentException("Image file is required");

        if (image.Length > MaxFileSize)
            throw new ArgumentException($"File size exceeds maximum allowed size of {MaxFileSize / (1024 * 1024)} MB");

        // Validate file extension
        var fileExtension = Path.GetExtension(image.FileName).ToLower();
        if (!_allowedExtensions.Contains(fileExtension))
            throw new ArgumentException($"File type '{fileExtension}' is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}");

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // Check if entry exists
        var entryExists = await connection.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM diary_entries WHERE Id = @EntryId",
            new { EntryId = entryId }
        );

        if (entryExists == 0)
            throw new ArgumentException($"Diary entry with ID {entryId} not found");

        // Insert image record
        const string sql = @"
            INSERT INTO diary_images (EntryId)
            VALUES (@EntryId);
            SELECT LAST_INSERT_ID();";

        var imageId = await connection.QuerySingleAsync<int>(sql, new { EntryId = entryId });

        // Save image file
        try
        {
            var filePath = Path.Combine(_uploadPath, $"{imageId}{fileExtension}");
            using var stream = new FileStream(filePath, FileMode.Create);
            await image.CopyToAsync(stream);
        }
        catch (Exception ex)
        {
            // Delete database record if file save fails
            await connection.ExecuteAsync("DELETE FROM diary_images WHERE Id = @Id", new { Id = imageId });
            throw new InvalidOperationException("Failed to save image file", ex);
        }

        return new DiaryImages { Id = imageId, EntryId = entryId };
    }

    public async Task<byte[]?> GetImageAsync(int imageId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // Check if image exists in database
        var imageExists = await connection.QuerySingleOrDefaultAsync<DiaryImages>(
            "SELECT Id, EntryId FROM diary_images WHERE Id = @Id",
            new { Id = imageId }
        );

        if (imageExists == null)
            return null;

        // Find the image file (try different extensions)
        string? imagePath = null;
        foreach (var ext in _allowedExtensions)
        {
            var path = Path.Combine(_uploadPath, $"{imageId}{ext}");
            if (File.Exists(path))
            {
                imagePath = path;
                break;
            }
        }

        if (imagePath == null)
            return null;

        return await File.ReadAllBytesAsync(imagePath);
    }

    public async Task<bool> DeleteImageAsync(int imageId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        var image = await connection.QuerySingleOrDefaultAsync<DiaryImages>(
            "SELECT Id, EntryId FROM diary_images WHERE Id = @Id",
            new { Id = imageId }
        );

        if (image == null)
            return false;

        // Delete file from filesystem
        foreach (var ext in _allowedExtensions)
        {
            var filePath = Path.Combine(_uploadPath, $"{imageId}{ext}");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                break;
            }
        }

        // Delete database record
        await connection.ExecuteAsync("DELETE FROM diary_images WHERE Id = @Id", new { Id = imageId });

        return true;
    }
}

using Dapper;
using DiaryApi.Models;

namespace DiaryApi.Services;

public interface IDiaryService
{
    Task<DiaryEntry?> CreateEntryAsync(string title, string? description, DateTime? date = null);
    Task<PaginatedResponse<EntryWithImages>> GetEntriesAsync(int page = 1, int pageSize = 10, string sortBy = "DESC", DateTime? filterDate = null);
    Task<List<EntryWithImages>> GetEntriesByDateAsync(DateTime date);
    Task<EntryWithImages?> GetEntryByIdAsync(int id);
    Task<bool> UpdateEntryAsync(int id, UpdateEntryRequest request);
    Task<bool> DeleteEntryAsync(int id);
    Task InitializeDatabase();
}

public class DiaryService : IDiaryService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DiaryService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeDatabase()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        string createTableSql = @"
            CREATE TABLE IF NOT EXISTS diary_entries (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Title VARCHAR(255) NOT NULL,
                Description TEXT,
                Date DATETIME NOT NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS diary_images (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                EntryId INT NOT NULL,
                FOREIGN KEY (EntryId) REFERENCES diary_entries(Id) ON DELETE CASCADE,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );
        ";

        var statements = createTableSql.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var statement in statements)
        {
            if (!string.IsNullOrWhiteSpace(statement))
            {
                await connection.ExecuteAsync(statement.Trim());
            }
        }
    }

    public async Task<DiaryEntry?> CreateEntryAsync(string title, string? description, DateTime? date = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        var entryDate = date ?? DateTime.UtcNow;

        const string sql = @"
            INSERT INTO diary_entries (Title, Description, Date)
            VALUES (@Title, @Description, @Date);
            SELECT LAST_INSERT_ID();";

        var id = await connection.QuerySingleAsync<int>(sql, new { Title = title, Description = description, Date = entryDate });

        return new DiaryEntry { Id = id, Title = title, Description = description, Date = entryDate };
    }

    public async Task<PaginatedResponse<EntryWithImages>> GetEntriesAsync(int page = 1, int pageSize = 10, string sortBy = "DESC", DateTime? filterDate = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // Validate sort parameter
        if (sortBy != "ASC" && sortBy != "DESC")
            sortBy = "DESC";

        // Build WHERE clause
        string whereClause = "";
        if (filterDate.HasValue)
        {
            whereClause = " WHERE DATE(Date) = @FilterDate";
        }

        // Get total count
        string countSql = $"SELECT COUNT(*) FROM diary_entries{whereClause}";
        int total = await connection.QuerySingleAsync<int>(countSql, new { FilterDate = filterDate?.Date });

        // Calculate offset
        int offset = (page - 1) * pageSize;

        // Get paginated entries
        string entriesSql = $@"
            SELECT Id, Title, Description, Date 
            FROM diary_entries
            {whereClause}
            ORDER BY Date {sortBy}
            LIMIT @Limit OFFSET @Offset";

        var entries = (await connection.QueryAsync<DiaryEntry>(entriesSql, new { FilterDate = filterDate?.Date, Limit = pageSize, Offset = offset })).ToList();

        // Get images for each entry
        var entriesWithImages = new List<EntryWithImages>();
        foreach (var entry in entries)
        {
            var imageIds = (await connection.QueryAsync<int>(
                "SELECT Id FROM diary_images WHERE EntryId = @EntryId",
                new { EntryId = entry.Id }
            )).ToList();

            entriesWithImages.Add(new EntryWithImages
            {
                Entry = entry,
                ImgIds = imageIds
            });
        }

        int totalPages = (int)Math.Ceiling((double)total / pageSize);

        return new PaginatedResponse<EntryWithImages>
        {
            Items = entriesWithImages,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<List<EntryWithImages>> GetEntriesByDateAsync(DateTime date)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        var entries = (await connection.QueryAsync<DiaryEntry>(
            "SELECT Id, Title, Description, Date FROM diary_entries WHERE DATE(Date) = @Date",
            new { Date = date.Date }
        )).ToList();

        var entriesWithImages = new List<EntryWithImages>();
        foreach (var entry in entries)
        {
            var imageIds = (await connection.QueryAsync<int>(
                "SELECT Id FROM diary_images WHERE EntryId = @EntryId",
                new { EntryId = entry.Id }
            )).ToList();

            entriesWithImages.Add(new EntryWithImages
            {
                Entry = entry,
                ImgIds = imageIds
            });
        }

        return entriesWithImages;
    }

    public async Task<EntryWithImages?> GetEntryByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        var entry = await connection.QuerySingleOrDefaultAsync<DiaryEntry>(
            "SELECT Id, Title, Description, Date FROM diary_entries WHERE Id = @Id",
            new { Id = id }
        );

        if (entry == null)
            return null;

        var imageIds = (await connection.QueryAsync<int>(
            "SELECT Id FROM diary_images WHERE EntryId = @EntryId",
            new { EntryId = entry.Id }
        )).ToList();

        return new EntryWithImages
        {
            Entry = entry,
            ImgIds = imageIds
        };
    }

    public async Task<bool> UpdateEntryAsync(int id, UpdateEntryRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        var entry = await connection.QuerySingleOrDefaultAsync<DiaryEntry>(
            "SELECT Id, Title, Description, Date FROM diary_entries WHERE Id = @Id",
            new { Id = id }
        );

        if (entry == null)
            return false;

        string title = request.Title ?? entry.Title;
        string? description = request.Description ?? entry.Description;
        DateTime date = request.Date ?? entry.Date;

        const string sql = @"
            UPDATE diary_entries 
            SET Title = @Title, Description = @Description, Date = @Date
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, new { Title = title, Description = description, Date = date, Id = id });
        return true;
    }

    public async Task<bool> DeleteEntryAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // Get all image IDs for this entry
        var imageIds = (await connection.QueryAsync<int>(
            "SELECT Id FROM diary_images WHERE EntryId = @EntryId",
            new { EntryId = id }
        )).ToList();

        // Delete images from filesystem
        string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        foreach (var imgId in imageIds)
        {
            var imagePath = Path.Combine(uploadPath, $"{imgId}.jpg");
            if (File.Exists(imagePath))
                File.Delete(imagePath);
        }

        // Delete database records (cascade will delete images)
        await connection.ExecuteAsync("DELETE FROM diary_entries WHERE Id = @Id", new { Id = id });

        return true;
    }
    
}

namespace DiaryApi.Models;

public class DiaryEntry
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Date { get; set; }
}

public class DiaryImages
{
    public int Id { get; set; }
    public int EntryId { get; set; }
}

public class EntryWithImages
{
    public DiaryEntry? Entry { get; set; }
    public List<int> ImgIds { get; set; } = new();
}

// DTO für Upload
public class CreateEntryRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// DTO für Update
public class UpdateEntryRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? Date { get; set; }
}

// Response Model
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

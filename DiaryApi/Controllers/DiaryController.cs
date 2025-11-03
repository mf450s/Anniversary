using Microsoft.AspNetCore.Mvc;
using DiaryApi.Models;
using DiaryApi.Services;

namespace DiaryApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiaryController : ControllerBase
{
    private readonly IDiaryService _diaryService;
    private readonly IImageService _imageService;
    private readonly ILogger<DiaryController> _logger;

    public DiaryController(IDiaryService diaryService, IImageService imageService, ILogger<DiaryController> logger)
    {
        _diaryService = diaryService;
        _imageService = imageService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new diary entry
    /// </summary>
    /// <param name="request">Entry data with title and optional description</param>
    /// <returns>Created entry with ID</returns>
    [HttpPost("entries")]
    public async Task<ActionResult<ApiResponse<DiaryEntry>>> CreateEntry([FromBody] CreateEntryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new ApiResponse<DiaryEntry> { Success = false, Message = "Title is required" });

            var entry = await _diaryService.CreateEntryAsync(request.Title, request.Description);

            return CreatedAtAction(nameof(GetEntryById), new { id = entry?.Id },
                new ApiResponse<DiaryEntry> { Success = true, Message = "Entry created successfully", Data = entry });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating entry");
            return StatusCode(500, new ApiResponse<DiaryEntry> { Success = false, Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get entries with pagination, filtering, and sorting
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="sortBy">Sort order: ASC or DESC</param>
    /// <param name="filterDate">Optional filter by date (yyyy-MM-dd format)</param>
    /// <returns>Paginated list of entries with images</returns>
    [HttpGet("entries")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<EntryWithImages>>>> GetEntries(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "DESC",
        [FromQuery] string? filterDate = null)
    {
        try
        {
            if (page < 1)
                page = 1;

            if (pageSize < 1 || pageSize > 100)
                pageSize = 10;

            DateTime? filterDateTime = null;
            if (!string.IsNullOrEmpty(filterDate) && DateTime.TryParse(filterDate, out var parsedDate))
            {
                filterDateTime = parsedDate;
            }

            var result = await _diaryService.GetEntriesAsync(page, pageSize, sortBy, filterDateTime);

            return Ok(new ApiResponse<PaginatedResponse<EntryWithImages>>
            {
                Success = true,
                Message = "Entries retrieved successfully",
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entries");
            return StatusCode(500, new ApiResponse<PaginatedResponse<EntryWithImages>>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Get all entries for a specific date
    /// </summary>
    /// <param name="date">Date in yyyy-MM-dd format</param>
    /// <returns>List of entries with images for the specified date</returns>
    [HttpGet("entries/by-date/{date}")]
    public async Task<ActionResult<ApiResponse<List<EntryWithImages>>>> GetEntriesByDate(string date)
    {
        try
        {
            if (!DateTime.TryParse(date, out var parsedDate))
                return BadRequest(new ApiResponse<List<EntryWithImages>>
                {
                    Success = false,
                    Message = "Invalid date format. Use yyyy-MM-dd"
                });

            var entries = await _diaryService.GetEntriesByDateAsync(parsedDate);

            return Ok(new ApiResponse<List<EntryWithImages>>
            {
                Success = true,
                Message = $"Entries for {parsedDate:yyyy-MM-dd} retrieved successfully",
                Data = entries
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entries by date");
            return StatusCode(500, new ApiResponse<List<EntryWithImages>>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Get a specific entry with its images
    /// </summary>
    /// <param name="id">Entry ID</param>
    /// <returns>Entry with image IDs</returns>
    [HttpGet("entries/{id}")]
    public async Task<ActionResult<ApiResponse<EntryWithImages>>> GetEntryById(int id)
    {
        try
        {
            var entry = await _diaryService.GetEntryByIdAsync(id);

            if (entry == null)
                return NotFound(new ApiResponse<EntryWithImages>
                {
                    Success = false,
                    Message = $"Entry with ID {id} not found"
                });

            return Ok(new ApiResponse<EntryWithImages>
            {
                Success = true,
                Message = "Entry retrieved successfully",
                Data = entry
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entry");
            return StatusCode(500, new ApiResponse<EntryWithImages>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Update entry metadata (title, description, date)
    /// </summary>
    /// <param name="id">Entry ID</param>
    /// <param name="request">Updated entry data</param>
    /// <returns>Success status</returns>
    [HttpPut("entries/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateEntry(int id, [FromBody] UpdateEntryRequest request)
    {
        try
        {
            var result = await _diaryService.UpdateEntryAsync(id, request);

            if (!result)
                return NotFound(new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Entry with ID {id} not found"
                });

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Entry updated successfully",
                Data = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating entry");
            return StatusCode(500, new ApiResponse<bool>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Delete an entry and all associated images
    /// </summary>
    /// <param name="id">Entry ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("entries/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEntry(int id)
    {
        try
        {
            var result = await _diaryService.DeleteEntryAsync(id);

            if (!result)
                return NotFound(new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Entry with ID {id} not found"
                });

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Entry deleted successfully",
                Data = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting entry");
            return StatusCode(500, new ApiResponse<bool>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Upload an image to an entry
    /// </summary>
    /// <param name="entryId">Entry ID</param>
    /// <param name="image">Image file</param>
    /// <returns>Image information</returns>
    [HttpPost("entries/{entryId}/images")]
    public async Task<ActionResult<ApiResponse<DiaryImages>>> UploadImage(int entryId, [FromForm(Name = "image")] IFormFile image)

    {
        try
        {
            var result = await _imageService.UploadImageAsync(entryId, image);

            return CreatedAtAction(nameof(GetImage), new { id = result?.Id },
                new ApiResponse<DiaryImages> { Success = true, Message = "Image uploaded successfully", Data = result });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid image upload");
            return BadRequest(new ApiResponse<DiaryImages> { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return StatusCode(500, new ApiResponse<DiaryImages>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Get an image by ID
    /// </summary>
    /// <param name="id">Image ID</param>
    /// <returns>Image file</returns>
    [HttpGet("images/{id}")]
    public async Task<ActionResult> GetImage(int id)
    {
        try
        {
            var imageBytes = await _imageService.GetImageAsync(id);

            if (imageBytes == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = $"Image with ID {id} not found"
                });

            return File(imageBytes, "application/octet-stream", $"image_{id}.jpg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving image");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Delete an image
    /// </summary>
    /// <param name="id">Image ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("images/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteImage(int id)
    {
        try
        {
            var result = await _imageService.DeleteImageAsync(id);

            if (!result)
                return NotFound(new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Image with ID {id} not found"
                });

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Image deleted successfully",
                Data = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image");
            return StatusCode(500, new ApiResponse<bool>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }
}

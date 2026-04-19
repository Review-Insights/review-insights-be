using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using ReviewInsights.Api.Domain.Entities;

namespace ReviewInsights.Api.Features.Uploads;

public class ParsedReviewsResult
{
    public List<Review> Reviews { get; set; } = [];
    public int RejectedCount { get; set; }
}

public class CsvJsonReviewParser
{
    public ParsedReviewsResult Parse(Stream stream, string fileName, Guid uploadId)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => ParseCsv(stream, uploadId),
            ".json" => ParseJson(stream, uploadId),
            _ => throw new NotSupportedException($"Unsupported file extension '{extension}'")
        };
    }

    private static ParsedReviewsResult ParseCsv(Stream stream, Guid uploadId)
    {
        var result = new ParsedReviewsResult();
        using var reader = new StreamReader(stream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        };
        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var row = new CsvRow
            {
                ClothingId = csv.GetField("Clothing ID"),
                Age = csv.GetField("Age"),
                Title = csv.GetField("Title"),
                ReviewText = csv.GetField("Review Text"),
                Rating = csv.GetField("Rating"),
                RecommendedInd = csv.GetField("Recommended IND"),
                PositiveFeedbackCount = csv.GetField("Positive Feedback Count"),
                DivisionName = csv.GetField("Division Name"),
                DepartmentName = csv.GetField("Department Name"),
                ClassName = csv.GetField("Class Name")
            };

            var review = MapRow(row, uploadId);
            if (review is null) result.RejectedCount++;
            else result.Reviews.Add(review);
        }

        return result;
    }

    private static ParsedReviewsResult ParseJson(Stream stream, Guid uploadId)
    {
        var result = new ParsedReviewsResult();
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var records = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(stream, options)
                      ?? [];

        foreach (var record in records)
        {
            var row = new CsvRow
            {
                ClothingId = GetStringFromJson(record, "Clothing ID"),
                Age = GetStringFromJson(record, "Age"),
                Title = GetStringFromJson(record, "Title"),
                ReviewText = GetStringFromJson(record, "Review Text"),
                Rating = GetStringFromJson(record, "Rating"),
                RecommendedInd = GetStringFromJson(record, "Recommended IND"),
                PositiveFeedbackCount = GetStringFromJson(record, "Positive Feedback Count"),
                DivisionName = GetStringFromJson(record, "Division Name"),
                DepartmentName = GetStringFromJson(record, "Department Name"),
                ClassName = GetStringFromJson(record, "Class Name")
            };

            var review = MapRow(row, uploadId);
            if (review is null) result.RejectedCount++;
            else result.Reviews.Add(review);
        }

        return result;
    }

    private static string? GetStringFromJson(Dictionary<string, JsonElement> record, string key)
    {
        if (!record.TryGetValue(key, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            _ => value.GetRawText()
        };
    }

    private static Review? MapRow(CsvRow row, Guid uploadId)
    {
        if (!int.TryParse(row.ClothingId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var clothingId)
            || clothingId <= 0)
        {
            return null;
        }

        if (!int.TryParse(row.Rating, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating)
            || rating < 1 || rating > 5)
        {
            return null;
        }

        if (!int.TryParse(row.RecommendedInd, NumberStyles.Integer, CultureInfo.InvariantCulture, out var recommendedInt)
            || (recommendedInt != 0 && recommendedInt != 1))
        {
            return null;
        }

        if (!int.TryParse(row.Age, NumberStyles.Integer, CultureInfo.InvariantCulture, out var age)
            || age <= 0 || age >= 150)
        {
            age = 0;
        }

        int.TryParse(row.PositiveFeedbackCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var positiveFeedback);
        if (positiveFeedback < 0) positiveFeedback = 0;

        return new Review
        {
            Id = Guid.NewGuid(),
            ClothingId = clothingId,
            Age = age,
            Title = string.IsNullOrWhiteSpace(row.Title) ? null : row.Title,
            ReviewText = string.IsNullOrWhiteSpace(row.ReviewText) ? null : row.ReviewText,
            Rating = rating,
            RecommendedInd = recommendedInt == 1,
            PositiveFeedbackCount = positiveFeedback,
            DivisionName = string.IsNullOrWhiteSpace(row.DivisionName) ? null : row.DivisionName,
            DepartmentName = string.IsNullOrWhiteSpace(row.DepartmentName) ? null : row.DepartmentName,
            ClassName = string.IsNullOrWhiteSpace(row.ClassName) ? null : row.ClassName,
            UploadId = uploadId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private class CsvRow
    {
        public string? ClothingId { get; set; }
        public string? Age { get; set; }
        public string? Title { get; set; }
        public string? ReviewText { get; set; }
        public string? Rating { get; set; }
        public string? RecommendedInd { get; set; }
        public string? PositiveFeedbackCount { get; set; }
        public string? DivisionName { get; set; }
        public string? DepartmentName { get; set; }
        public string? ClassName { get; set; }
    }
}

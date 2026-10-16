using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctalPulse.Application.Contracts;

public class NotificationTypeFlexibleConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? "General";
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            int intVal = reader.GetInt32();
            return intVal switch
            {
                0 => "General",
                1 => "MajorTask",
                2 => "MinorTask",
                3 => "Event",
                4 => "Post",
                5 => "Comment",
                6 => "Project",
                7 => "Track",
                _ => intVal.ToString()
            };
        }

        return "General";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

public record NotificationItemDto(
    Guid Id,
    Guid UserId,
    Guid? ActorId,
    string? ActorName,
    string? ActorProfilePictureUrl,
    Guid? ProjectId,
    string? ProjectName,
    Guid? TrackId,
    string? TrackName,
    string Title,
    string Message,
    [property: JsonConverter(typeof(NotificationTypeFlexibleConverter))]
    string Type,
    Guid? EntityId,
    bool IsRead,
    DateTime CreatedDate);

public record PagedNotificationsDto(
    List<NotificationItemDto> Items,
    int TotalCount,
    int UnreadCount,
    int PageNumber,
    int PageSize,
    bool HasMore);

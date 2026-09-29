using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TodoApp.Api.Data;

/// <summary>
/// SQLite has no native date type and drops <see cref="DateTimeKind"/> on read. These converters make sure
/// every value round-trips as UTC so the API always serialises an unambiguous instant ("...Z").
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

public sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime()) : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

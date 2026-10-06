using Npgsql;

namespace TruvoID.Infrastructure.Postgres;

public sealed record NotificationRow(
    Guid Id,
    string Kind,
    string Title,
    string? Body,
    string? Link,
    DateTime CreatedAt,
    DateTime? ReadAt);

/// <summary>In-app notifications for signed-in users.</summary>
public sealed class NotificationStore(NpgsqlDataSource controlPlane)
{
    private const string Columns = "id, kind, title, body, link, created_at, read_at";

    public async Task<IReadOnlyList<NotificationRow>> ListAsync(Guid userId, bool unreadOnly = false, int limit = 20, CancellationToken ct = default)
    {
        var where = unreadOnly ? "AND read_at IS NULL" : "";
        await using var command = controlPlane.CreateCommand($"""
            SELECT {Columns} FROM control.notification
            WHERE user_id = @userId {where}
            ORDER BY created_at DESC
            LIMIT @limit
            """);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 100));
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<NotificationRow>();
        while (await reader.ReadAsync(ct))
            rows.Add(new NotificationRow(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetFieldValue<DateTime>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTime>(6)));
        return rows;
    }

    public async Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        await using var command = controlPlane.CreateCommand(
            "SELECT count(*) FROM control.notification WHERE user_id = @userId AND read_at IS NULL");
        command.Parameters.AddWithValue("userId", userId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        await using var command = controlPlane.CreateCommand(
            "UPDATE control.notification SET read_at = now() WHERE id = @id AND user_id = @userId AND read_at IS NULL");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("userId", userId);
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        await using var command = controlPlane.CreateCommand(
            "UPDATE control.notification SET read_at = now() WHERE user_id = @userId AND read_at IS NULL");
        command.Parameters.AddWithValue("userId", userId);
        return await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Notifies every active administrator of an organization.</summary>
    public async Task NotifyOrganizationAdminsAsync(Guid organizationId, string kind, string title, string? body, string? link, CancellationToken ct = default)
    {
        await using var command = controlPlane.CreateCommand("""
            INSERT INTO control.notification (user_id, organization_id, kind, title, body, link)
            SELECT u.id, @org, @kind, @title, @body, @link
            FROM control.app_user u
            WHERE u.organization_id = @org
              AND u.status = 'active'
              AND u.role IN ('institution_admin', 'agency_admin')
            """);
        command.Parameters.AddWithValue("org", organizationId);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("body", (object?)body ?? DBNull.Value);
        command.Parameters.AddWithValue("link", (object?)link ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }
}

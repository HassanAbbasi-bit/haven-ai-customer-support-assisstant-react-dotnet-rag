namespace HavenSupport.Domain;

public sealed class GuestSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
public sealed class RefreshSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public Guid FamilyId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}
public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public string Title { get; set; } = "New conversation";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Deleted { get; set; }
}
public sealed class ChatRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string Question { get; set; } = "";
    public string State { get; set; } = "queued";
    public string Stage { get; set; } = "Checking Haven policies";
    public string? ErrorCode { get; set; }
    public bool CancelRequested { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid RequestId { get; set; }
    public string Role { get; set; } = "user";
    public string Text { get; set; } = "";
    public string Kind { get; set; } = "answered";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Category { get; set; } = "General";
    public Guid? ActiveVersionId { get; set; }
    public bool Deleted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
public sealed class DocumentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string Label { get; set; } = "1.0";
    public DateTimeOffset EffectiveDate { get; set; }
    public string Hash { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string FileType { get; set; } = "txt";
    public long ByteCount { get; set; }
    public string State { get; set; } = "queued";
    public string Publication { get; set; } = "draft";
    public string? ErrorCode { get; set; }
    public int ChunkCount { get; set; }
    public int IndexedCount { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
public sealed class KnowledgeChunk
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public int Ordinal { get; set; }
    public string SourceKey { get; set; } = "";
    public int? Page { get; set; }
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }
    public string Text { get; set; } = "";
}
public sealed class Citation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public Guid ChunkId { get; set; }
    public string Quote { get; set; } = "";
    public string Claim { get; set; } = "";
}
public sealed class Feedback
{
    public Guid MessageId { get; set; }
    public bool Helpful { get; set; }
    public string? Comment { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class SupportTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public Guid ClientRequestId { get; set; }
    public string Reference { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Email { get; set; } = "";
    public string? OrderReference { get; set; }
    public string SharedContext { get; set; } = "";
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ConsentAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CorpusRevision
{
    public int Id { get; set; }
    public long Revision { get; set; }
}
public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Actor { get; set; } = "";
    public string Operation { get; set; } = "";
    public Guid ResourceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

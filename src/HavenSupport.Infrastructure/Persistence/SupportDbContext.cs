using HavenSupport.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HavenSupport.Infrastructure.Persistence;

public sealed class HavenUser : IdentityUser;
public sealed class FoundationProbe
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}
public sealed class SupportDbContext(DbContextOptions<SupportDbContext> options) : IdentityDbContext<HavenUser>(options)
{
    public DbSet<FoundationProbe> FoundationProbes => Set<FoundationProbe>();
    public DbSet<GuestSession> Guests => Set<GuestSession>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatRequest> ChatRequests => Set<ChatRequest>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<KnowledgeDocument> Documents => Set<KnowledgeDocument>();
    public DbSet<DocumentVersion> Versions => Set<DocumentVersion>();
    public DbSet<KnowledgeChunk> Chunks => Set<KnowledgeChunk>();
    public DbSet<Citation> Citations => Set<Citation>();
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<SupportTicket> Tickets => Set<SupportTicket>();
    public DbSet<CorpusRevision> Corpus => Set<CorpusRevision>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<FoundationProbe>().Property(p => p.Label).HasMaxLength(160).IsRequired();
        model.Entity<GuestSession>().HasIndex(p => p.TokenHash).IsUnique();
        model.Entity<RefreshSession>().HasIndex(p => p.TokenHash).IsUnique();
        model.Entity<Conversation>().HasIndex(p => new { p.OwnerId, p.UpdatedAt });
        model.Entity<ChatRequest>().HasIndex(p => new { p.ConversationId, p.ClientRequestId }).IsUnique();
        model.Entity<ChatRequest>().HasIndex(p => p.ConversationId).IsUnique().HasFilter("[State] IN ('queued', 'processing')");
        model.Entity<ChatRequest>().Property(p => p.RowVersion).IsRowVersion();
        model.Entity<Message>().HasIndex(p => new { p.RequestId, p.Role }).IsUnique();
        model.Entity<KnowledgeDocument>().Property(p => p.RowVersion).IsRowVersion();
        model.Entity<DocumentVersion>().Property(p => p.RowVersion).IsRowVersion();
        model.Entity<DocumentVersion>().HasIndex(p => new { p.DocumentId, p.Hash }).IsUnique();
        model.Entity<KnowledgeChunk>().HasIndex(p => new { p.VersionId, p.Ordinal }).IsUnique();
        model.Entity<Feedback>().HasKey(p => p.MessageId);
        model.Entity<SupportTicket>().HasIndex(p => new { p.OwnerId, p.ClientRequestId }).IsUnique();
        model.Entity<SupportTicket>().HasIndex(p => p.Reference).IsUnique();
        model.Entity<SupportTicket>().Property(p => p.RowVersion).IsRowVersion();
        model.Entity<ChatRequest>().HasOne<Conversation>().WithMany().HasForeignKey(p => p.ConversationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Message>().HasOne<Conversation>().WithMany().HasForeignKey(p => p.ConversationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Message>().HasOne<ChatRequest>().WithMany().HasForeignKey(p => p.RequestId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<DocumentVersion>().HasOne<KnowledgeDocument>().WithMany().HasForeignKey(p => p.DocumentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<KnowledgeChunk>().HasOne<DocumentVersion>().WithMany().HasForeignKey(p => p.VersionId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Citation>().HasOne<Message>().WithMany().HasForeignKey(p => p.MessageId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Citation>().HasOne<KnowledgeChunk>().WithMany().HasForeignKey(p => p.ChunkId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Feedback>().HasOne<Message>().WithMany().HasForeignKey(p => p.MessageId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RefreshSession>().HasOne<HavenUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        foreach (var entity in model.Model.GetEntityTypes().Where(e => e.ClrType.Namespace == "HavenSupport.Domain"))
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string)))
                property.SetMaxLength(property.Name switch
                {
                    "Text" or "Question" or "Summary" or "SharedContext" or "Claim" => 4000,
                    "Quote" => 1200,
                    "Comment" => 1000,
                    "OwnerId" or "UserId" => 450,
                    "Title" or "Subject" => 160,
                    "Email" => 254,
                    _ => 200
                });
    }
}

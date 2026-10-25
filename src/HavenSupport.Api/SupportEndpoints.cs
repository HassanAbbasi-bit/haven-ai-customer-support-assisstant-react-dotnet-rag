using System.Security.Claims;
using System.Security.Cryptography;
using HavenSupport.Domain;
using HavenSupport.Infrastructure;
using HavenSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HavenSupport.Api;

public static class SupportEndpoints
{
    public static void MapSupport(WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();
        api.MapPost("/conversations", async (ClaimsPrincipal user, SupportDbContext db) =>
        {
            var conversation = new Conversation { OwnerId = user.Owner() }; db.Conversations.Add(conversation); await db.SaveChangesAsync();
            return Results.Created($"/api/v1/conversations/{conversation.Id}", new { conversation.Id, conversation.Title });
        });
        api.MapGet("/conversations", async (ClaimsPrincipal user, SupportDbContext db) => Results.Ok(await Owned(db, user).OrderByDescending(c => c.UpdatedAt).Take(100).Select(c => new { c.Id, c.Title, c.UpdatedAt }).ToArrayAsync()));
        api.MapGet("/conversations/{id:guid}", async (Guid id, ClaimsPrincipal user, SupportDbContext db) =>
        {
            var conversation = await Owned(db, user).SingleOrDefaultAsync(c => c.Id == id); if (conversation is null) return Results.NotFound();
            var messages = await db.Messages.Where(m => m.ConversationId == id).OrderBy(m => m.CreatedAt).ThenBy(m => m.Role).ToArrayAsync();
            var messageIds = messages.Select(m => m.Id).ToArray();
            var citations = await CitationQuery(db, messageIds).ToArrayAsync();
            var feedback = await db.Feedback.Where(f => messageIds.Contains(f.MessageId)).ToArrayAsync();
            var requests = await db.ChatRequests.Where(r => r.ConversationId == id).OrderBy(r => r.CreatedAt).Select(r => new { r.Id, r.State, r.Stage, r.ErrorCode, r.Question, r.CreatedAt, r.DurationMs }).ToArrayAsync();
            return Results.Ok(new { conversation.Id, conversation.Title, messages = messages.Select(m => new { m.Id, m.RequestId, m.Role, m.Text, m.Kind, m.CreatedAt, citations = citations.Where(c => c.MessageId == m.Id), feedback = feedback.SingleOrDefault(f => f.MessageId == m.Id) }), requests });
        });
        api.MapDelete("/conversations/{id:guid}", async (Guid id, ClaimsPrincipal user, SupportDbContext db) =>
        {
            if (!await Owned(db, user).AnyAsync(c => c.Id == id)) return Results.NotFound();
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Conversations.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Deleted, true));
            await db.ChatRequests.Where(r => r.ConversationId == id && (r.State == "queued" || r.State == "processing")).ExecuteUpdateAsync(s => s.SetProperty(r => r.State, "canceled").SetProperty(r => r.CancelRequested, true));
            await tx.CommitAsync(); return Results.NoContent();
        });
        api.MapPost("/conversations/{id:guid}/requests", async (Guid id, QuestionInput input, ClaimsPrincipal user, SupportDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 4000 || input.ClientRequestId == Guid.Empty) return ApiErrors.Bad("invalid_question", "Enter a question between 1 and 4,000 characters and a valid request ID.");
            var conversation = await Owned(db, user).SingleOrDefaultAsync(c => c.Id == id); if (conversation is null) return Results.NotFound();
            var previous = await db.ChatRequests.SingleOrDefaultAsync(r => r.ConversationId == id && r.ClientRequestId == input.ClientRequestId);
            if (previous is not null) return previous.Question == input.Message.Trim() ? Results.Accepted($"/api/v1/conversations/{id}/requests/{previous.Id}", new { previous.Id, previous.State }) : ApiErrors.Bad("idempotency_conflict", "This request ID was already used for a different question.", 409);
            if (await db.ChatRequests.AnyAsync(r => r.ConversationId == id && (r.State == "queued" || r.State == "processing"))) return ApiErrors.Bad("request_in_progress", "Wait for the current answer or stop it before asking again.", 409);
            if (await db.ChatRequests.CountAsync(r => r.State == "queued" || r.State == "processing") >= 20) return ApiErrors.Bad("queue_full", "The assistant is busy. Please try again shortly.", 429);
            var request = new ChatRequest { ConversationId = id, ClientRequestId = input.ClientRequestId, Question = input.Message.Trim() };
            db.ChatRequests.Add(request); db.Messages.Add(new Message { ConversationId = id, RequestId = request.Id, Role = "user", Text = request.Question });
            conversation.Title = request.Question[..Math.Min(80, request.Question.Length)]; conversation.UpdatedAt = DateTimeOffset.UtcNow;
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return ApiErrors.Bad("request_conflict", "A request was already accepted. Refresh the conversation before retrying.", 409); }
            return Results.Accepted($"/api/v1/conversations/{id}/requests/{request.Id}", new { request.Id, request.State });
        }).RequireRateLimiting("chat");
        api.MapGet("/conversations/{id:guid}/requests/{requestId:guid}", async (Guid id, Guid requestId, ClaimsPrincipal user, SupportDbContext db) =>
        {
            if (!await Owned(db, user).AnyAsync(c => c.Id == id)) return Results.NotFound();
            var request = await db.ChatRequests.Where(r => r.Id == requestId && r.ConversationId == id).Select(r => new { r.Id, r.State, r.Stage, r.ErrorCode, r.DurationMs }).SingleOrDefaultAsync();
            return request is null ? Results.NotFound() : Results.Ok(request);
        });
        api.MapPost("/conversations/{id:guid}/requests/{requestId:guid}/cancel", async (Guid id, Guid requestId, ClaimsPrincipal user, SupportDbContext db) =>
        {
            if (!await Owned(db, user).AnyAsync(c => c.Id == id) || !await db.ChatRequests.AnyAsync(r => r.Id == requestId && r.ConversationId == id)) return Results.NotFound();
            await db.ChatRequests.Where(r => r.Id == requestId && r.ConversationId == id && (r.State == "queued" || r.State == "processing"))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, "canceled").SetProperty(r => r.CancelRequested, true).SetProperty(r => r.Stage, "Canceled").SetProperty(r => r.CompletedAt, DateTimeOffset.UtcNow));
            return Results.NoContent();
        });
        api.MapGet("/messages/{messageId:guid}/citations/{citationId:guid}", async (Guid messageId, Guid citationId, ClaimsPrincipal user, SupportDbContext db) =>
        {
            if (!await (from m in db.Messages join c in Owned(db, user) on m.ConversationId equals c.Id where m.Id == messageId select m.Id).AnyAsync()) return Results.NotFound();
            var source = await CitationQuery(db, [messageId], citationId).SingleOrDefaultAsync();
            if (source is null) return Results.NotFound();
            return source.Available ? Results.Ok(source) : ApiErrors.Bad("source_withdrawn", "This source is no longer published. Ask again using current policies.", 410);
        });
        api.MapPut("/messages/{id:guid}/feedback", async (Guid id, FeedbackInput input, ClaimsPrincipal user, SupportDbContext db) =>
        {
            if ((input.Comment?.Length ?? 0) > 1000 || (input.Reason?.Length ?? 0) > 100) return ApiErrors.Bad("feedback_too_long", "Keep feedback under 1,000 characters.");
            if (!await (from m in db.Messages join c in Owned(db, user) on m.ConversationId equals c.Id where m.Id == id && m.Role == "assistant" select m.Id).AnyAsync()) return Results.NotFound();
            var vote = await db.Feedback.FindAsync(id); if (vote is null) { vote = new Feedback { MessageId = id }; db.Feedback.Add(vote); }
            vote.Helpful = input.Helpful; vote.Comment = input.Comment; vote.Reason = input.Reason; vote.UpdatedAt = DateTimeOffset.UtcNow;
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return ApiErrors.Bad("feedback_conflict", "Feedback changed concurrently. Refresh and try again.", 409); }
            return Results.Ok(vote);
        });
        api.MapPost("/tickets", async (TicketInput input, ClaimsPrincipal user, SupportDbContext db) =>
        {
            if (!input.Consent || input.ClientRequestId == Guid.Empty || string.IsNullOrWhiteSpace(input.Subject) || input.Subject.Length > 160 || string.IsNullOrWhiteSpace(input.Summary) || input.Summary.Length > 4000 || input.Email is null || input.Email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(input.Email, out _) || (input.OrderReference?.Length ?? 0) > 100 || (input.SharedContext?.Length ?? 0) > 4000)
                return ApiErrors.Bad("invalid_ticket", "Enter a subject, description and valid email. Review the shared details and confirm consent.");
            var existing = await db.Tickets.SingleOrDefaultAsync(t => t.OwnerId == user.Owner() && t.ClientRequestId == input.ClientRequestId);
            if (existing is not null) return existing.Subject == input.Subject && existing.Summary == input.Summary && existing.Email == input.Email && existing.OrderReference == input.OrderReference && existing.SharedContext == (input.SharedContext ?? "") ? Results.Ok(existing) : ApiErrors.Bad("idempotency_conflict", "This submission ID was already used with different details.", 409);
            var ticket = new SupportTicket { OwnerId = user.Owner(), ClientRequestId = input.ClientRequestId, Reference = $"HV-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}", Subject = input.Subject, Summary = input.Summary, Email = input.Email, OrderReference = input.OrderReference, SharedContext = input.SharedContext ?? "", ConsentAt = DateTimeOffset.UtcNow };
            db.Tickets.Add(ticket);
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return ApiErrors.Bad("ticket_conflict", "A submission was already accepted. Check My tickets before retrying.", 409); }
            return Results.Created($"/api/v1/tickets/{ticket.Id}", ticket);
        }).RequireRateLimiting("chat");
        api.MapGet("/tickets", async (ClaimsPrincipal user, SupportDbContext db) => Results.Ok(await db.Tickets.Where(t => t.OwnerId == user.Owner()).OrderByDescending(t => t.CreatedAt).Take(100).ToArrayAsync()));
        api.MapGet("/tickets/{id:guid}", async (Guid id, ClaimsPrincipal user, SupportDbContext db) => { var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == user.Owner()); return ticket is null ? Results.NotFound() : Results.Ok(ticket); });
        MapAdmin(app);
    }
    private static IQueryable<Conversation> Owned(SupportDbContext db, ClaimsPrincipal user) => db.Conversations.Where(c => c.OwnerId == user.Owner() && !c.Deleted);
    private static IQueryable<SourceView> CitationQuery(SupportDbContext db, Guid[] messageIds, Guid? citationId = null) => from c in db.Citations.Where(c => messageIds.Contains(c.MessageId) && (citationId == null || c.Id == citationId)) join chunk in db.Chunks on c.ChunkId equals chunk.Id join v in db.Versions on chunk.VersionId equals v.Id join d in db.Documents on v.DocumentId equals d.Id select new SourceView(c.Id, c.MessageId, d.Title, v.Label, v.EffectiveDate, chunk.Page, chunk.LineStart, chunk.LineEnd, chunk.SourceKey, !d.Deleted && d.ActiveVersionId == v.Id ? c.Quote : "", c.Claim, !d.Deleted && d.ActiveVersionId == v.Id, !d.Deleted && d.ActiveVersionId == v.Id ? chunk.Text : "");
    public sealed record SourceView(Guid Id, Guid MessageId, string DocumentTitle, string Version, DateTimeOffset EffectiveDate, int? Page, int? LineStart, int? LineEnd, string SourceKey, string Quote, string Claim, bool Available, string Text);
    public sealed record QuestionInput(Guid ClientRequestId, string Message);
    public sealed record FeedbackInput(bool Helpful, string? Comment, string? Reason);
    public sealed record TicketInput(Guid ClientRequestId, string Subject, string Summary, string Email, string? OrderReference, string? SharedContext, bool Consent);
    public sealed record StatusInput(string Status);
    private static bool Matches(HttpRequest request, byte[] rowVersion) => request.Headers.IfMatch.ToString().Trim('"') == Convert.ToBase64String(rowVersion);
    private static void Audit(SupportDbContext db, ClaimsPrincipal user, string operation, Guid resourceId) => db.Audit.Add(new AuditEvent { Actor = user.Owner(), Operation = operation, ResourceId = resourceId });
    private static void MapAdmin(WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization(policy => policy.RequireRole("Admin"));
        admin.MapPost("/retrieval-preview", async (QuestionInput input, SupportDbContext db, RagEngine engine, CancellationToken token) =>
        {
            if (string.IsNullOrWhiteSpace(input.Message) || input.Message.Length > 4000) return ApiErrors.Bad("invalid_question", "Enter a question under 4,000 characters.");
            var allowed = await db.Documents.Where(d => !d.Deleted && d.ActiveVersionId != null).Select(d => d.ActiveVersionId!.Value).ToArrayAsync(token);
            var ids = await engine.RetrieveAsync(input.Message, allowed, token);
            var passages = await (from c in db.Chunks join v in db.Versions on c.VersionId equals v.Id join d in db.Documents on v.DocumentId equals d.Id where ids.Contains(c.Id) && allowed.Contains(v.Id) select new { c.Id, c.SourceKey, c.Text, c.Page, documentTitle = d.Title }).ToArrayAsync(token);
            return Results.Ok(new { ids, passages });
        });
        admin.MapGet("/documents", async (SupportDbContext db) => Results.Ok(await db.Documents.Where(d => !d.Deleted).OrderByDescending(d => d.UpdatedAt).Take(100).Select(d => new { d.Id, d.Title, d.Category, d.ActiveVersionId, d.RowVersion, versions = db.Versions.Where(v => v.DocumentId == d.Id).OrderByDescending(v => v.CreatedAt).Select(v => new { v.Id, v.Label, v.EffectiveDate, v.State, v.Publication, v.ErrorCode, v.ChunkCount, v.IndexedCount, v.CreatedAt }).ToArray() }).ToArrayAsync()));
        admin.MapPost("/documents", UploadAsync).RequireRateLimiting("upload");
        admin.MapGet("/documents/{id:guid}/versions/{versionId:guid}", async (Guid id, Guid versionId, SupportDbContext db) =>
        {
            var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == id && !d.Deleted); var version = await db.Versions.SingleOrDefaultAsync(v => v.Id == versionId && v.DocumentId == id);
            if (document is null || version is null) return Results.NotFound();
            return Results.Ok(new { document, version, passages = await db.Chunks.Where(c => c.VersionId == versionId).OrderBy(c => c.Ordinal).Take(1500).ToArrayAsync() });
        });
        admin.MapPost("/versions/{id:guid}/retry", async (Guid id, ClaimsPrincipal user, SupportDbContext db) =>
        {
            var version = await db.Versions.SingleOrDefaultAsync(v => v.Id == id); if (version is null) return Results.NotFound();
            if (!await db.Documents.AnyAsync(d => d.Id == version.DocumentId && !d.Deleted) || version.State is not ("failed" or "canceled")) return ApiErrors.Bad("invalid_state", "Only failed or canceled documents can be retried.", 409);
            version.State = "queued"; version.ErrorCode = null; version.IndexedCount = 0; version.LeaseId = null; Audit(db, user, "retry", id); await db.SaveChangesAsync(); return Results.Accepted();
        });
        admin.MapPost("/versions/{id:guid}/cancel", async (Guid id, ClaimsPrincipal user, SupportDbContext db) =>
        {
            var changed = await db.Versions.Where(v => v.Id == id && (v.State == "queued" || v.State == "extracting" || v.State == "indexing")).ExecuteUpdateAsync(s => s.SetProperty(v => v.State, "canceled"));
            if (changed == 0) return ApiErrors.Bad("invalid_state", "The document cannot be canceled in its current state.", 409);
            Audit(db, user, "cancel", id); await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapPost("/versions/{id:guid}/publish", async (Guid id, HttpRequest request, ClaimsPrincipal user, SupportDbContext db, RagEngine engine, CancellationToken token) =>
        {
            var version = await db.Versions.SingleOrDefaultAsync(v => v.Id == id, token); if (version is null) return Results.NotFound();
            var document = await db.Documents.SingleAsync(d => d.Id == version.DocumentId, token);
            if (!Matches(request, document.RowVersion)) return ApiErrors.Bad("version_conflict", "This document changed. Refresh the review before publishing.", 412);
            if (document.Deleted || version.State != "ready" || version.EffectiveDate > DateTimeOffset.UtcNow) return ApiErrors.Bad("not_ready", "Only fully indexed documents effective today or earlier can be published.", 409);
            var chunks = await db.Chunks.Where(c => c.VersionId == id).ToArrayAsync(token);
            if (chunks.Length != version.ChunkCount || !await engine.VerifyAsync(chunks, token)) return ApiErrors.Bad("index_incomplete", "Index verification failed. Retry indexing before publishing.", 409);
            await using var tx = await db.Database.BeginTransactionAsync(token);
            await db.Versions.Where(v => v.DocumentId == document.Id && v.Publication == "published").ExecuteUpdateAsync(s => s.SetProperty(v => v.Publication, "superseded"), token);
            document.ActiveVersionId = id; document.UpdatedAt = DateTimeOffset.UtcNow; version.Publication = "published";
            await db.Corpus.ExecuteUpdateAsync(s => s.SetProperty(c => c.Revision, c => c.Revision + 1), token); Audit(db, user, "publish", id);
            await db.SaveChangesAsync(token); await tx.CommitAsync(token); return Results.Ok(new { published = true });
        });
        admin.MapPost("/documents/{id:guid}/archive", (Guid id, HttpRequest request, ClaimsPrincipal user, SupportDbContext db) => WithdrawAsync(id, false, request, user, db));
        admin.MapDelete("/documents/{id:guid}", (Guid id, HttpRequest request, ClaimsPrincipal user, SupportDbContext db) => WithdrawAsync(id, true, request, user, db));
        admin.MapGet("/tickets", async (SupportDbContext db) => Results.Ok(await db.Tickets.OrderByDescending(t => t.CreatedAt).Take(100).ToArrayAsync()));
        admin.MapPatch("/tickets/{id:guid}", async (Guid id, StatusInput input, HttpRequest request, ClaimsPrincipal user, SupportDbContext db) =>
        {
            if (input.Status is not ("open" or "in_progress" or "resolved" or "closed")) return ApiErrors.Bad("invalid_status", "Choose an available ticket status.");
            var ticket = await db.Tickets.FindAsync(id); if (ticket is null) return Results.NotFound();
            if (!Matches(request, ticket.RowVersion)) return ApiErrors.Bad("version_conflict", "Another administrator changed this ticket. Refresh and review their changes.", 412);
            ticket.Status = input.Status; Audit(db, user, "ticket_status", id); await db.SaveChangesAsync(); return Results.Ok(ticket);
        });
        admin.MapGet("/feedback", async (SupportDbContext db) => Results.Ok(await (from f in db.Feedback join m in db.Messages on f.MessageId equals m.Id orderby f.UpdatedAt descending select new { f.MessageId, f.Helpful, f.Reason, f.Comment, f.UpdatedAt, answer = m.Text }).Take(100).ToArrayAsync()));
        admin.MapGet("/metrics", async (SupportDbContext db) =>
        {
            var since = DateTimeOffset.UtcNow.AddDays(-30); var requests = await db.ChatRequests.Where(r => r.CreatedAt >= since).ToArrayAsync();
            var completed = requests.Where(r => r.State == "completed").ToArray(); var votes = await db.Feedback.Where(f => f.UpdatedAt >= since).ToArrayAsync();
            var unknown = await db.Messages.CountAsync(m => m.Role == "assistant" && m.Kind == "not_found" && m.CreatedAt >= since);
            var durations = completed.Select(r => r.DurationMs).Order().ToArray();
            return Results.Ok(new { since, until = DateTimeOffset.UtcNow, requests = requests.Length, completed = completed.Length, failed = requests.Count(r => r.State == "failed"), unknown, feedbackCount = votes.Length, helpfulCount = votes.Count(v => v.Helpful), p95Ms = durations.Length == 0 ? (long?)null : durations[(int)Math.Ceiling(durations.Length * .95) - 1], openTickets = await db.Tickets.CountAsync(t => t.Status == "open" || t.Status == "in_progress"), failedDocuments = await db.Versions.CountAsync(v => v.State == "failed") });
        });
    }
    private static async Task<IResult> UploadAsync(HttpRequest request, ClaimsPrincipal user, SupportDbContext db, IConfiguration configuration, CancellationToken token)
    {
        if (!request.HasFormContentType) return ApiErrors.Bad("multipart_required", "Choose a PDF or TXT file.");
        var form = await request.ReadFormAsync(token); var file = form.Files.GetFile("file");
        var title = form["title"].ToString().Trim(); var label = form["version"].ToString().Trim(); var category = form["category"].ToString().Trim();
        if (title.Length is 0 or > 160 || label.Length is 0 or > 40 || category.Length > 80 || !DateOnly.TryParseExact(form["effectiveDate"], "yyyy-MM-dd", out var date)) return ApiErrors.Bad("invalid_metadata", "Provide a title, version and effective date.");
        if (file is null || file.Length is 0 or > 20 * 1024 * 1024) return ApiErrors.Bad("file_size", "Choose a file between 1 byte and 20 MiB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".pdf" or ".txt")) return ApiErrors.Bad("unsupported_file", "Only text-based PDF and UTF-8 TXT files are supported.");
        using var buffer = new MemoryStream(); await file.CopyToAsync(buffer, token); var bytes = buffer.ToArray();
        if (extension == ".pdf" && (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))) return ApiErrors.Bad("invalid_pdf", "The file does not contain a PDF signature.");
        if (extension == ".txt") { try { var text = new System.Text.UTF8Encoding(false, true).GetString(bytes); if (text.Contains('\0')) return ApiErrors.Bad("invalid_text", "Upload a UTF-8 text file."); } catch (System.Text.DecoderFallbackException) { return ApiErrors.Bad("invalid_text", "Upload a UTF-8 text file."); } }
        KnowledgeDocument? document = null;
        if (Guid.TryParse(form["documentId"], out var documentId)) document = await db.Documents.SingleOrDefaultAsync(d => d.Id == documentId && !d.Deleted, token);
        if (!string.IsNullOrWhiteSpace(form["documentId"]) && document is null) return Results.NotFound();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (document is not null)
        {
            var duplicate = await db.Versions.SingleOrDefaultAsync(v => v.DocumentId == document.Id && v.Hash == hash, token);
            if (duplicate is not null) return Results.Ok(new { documentId = document.Id, versionId = duplicate.Id, duplicate = true });
        }
        else { document = new KnowledgeDocument { Title = title, Category = category.Length > 0 ? category : "General" }; db.Documents.Add(document); }
        var version = new DocumentVersion { DocumentId = document.Id, Label = label, EffectiveDate = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), Hash = hash, StorageKey = Guid.NewGuid().ToString("N") + extension, FileType = extension[1..], ByteCount = bytes.Length };
        var path = Path.Combine(configuration["Storage:Path"]!, version.StorageKey); Directory.CreateDirectory(configuration["Storage:Path"]!);
        await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) await target.WriteAsync(bytes, token);
        db.Versions.Add(version); document.UpdatedAt = DateTimeOffset.UtcNow; Audit(db, user, "upload", version.Id);
        try { await db.SaveChangesAsync(token); } catch { File.Delete(path); throw; }
        return Results.Accepted($"/api/v1/admin/documents/{document.Id}/versions/{version.Id}", new { documentId = document.Id, versionId = version.Id, state = "queued" });
    }
    private static async Task<IResult> WithdrawAsync(Guid id, bool delete, HttpRequest request, ClaimsPrincipal user, SupportDbContext db)
    {
        var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == id && !d.Deleted); if (document is null) return Results.NotFound();
        if (!Matches(request, document.RowVersion)) return ApiErrors.Bad("version_conflict", "The document changed. Refresh before trying again.", 412);
        await using var tx = await db.Database.BeginTransactionAsync();
        document.ActiveVersionId = null; document.Deleted = delete; document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.Versions.Where(v => v.DocumentId == id && v.Publication == "published").ExecuteUpdateAsync(s => s.SetProperty(v => v.Publication, delete ? "deleted" : "archived"));
        await db.Versions.Where(v => v.DocumentId == id && (v.State == "queued" || v.State == "extracting" || v.State == "indexing")).ExecuteUpdateAsync(s => s.SetProperty(v => v.State, "canceled"));
        await db.Corpus.ExecuteUpdateAsync(s => s.SetProperty(c => c.Revision, c => c.Revision + 1)); Audit(db, user, delete ? "delete" : "archive", id);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.NoContent();
    }
}

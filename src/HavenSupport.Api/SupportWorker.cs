using HavenSupport.Domain;
using HavenSupport.Infrastructure;
using HavenSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HavenSupport.Api;

// SQL leases make accepted work recoverable after a process or machine restart.
public sealed class SupportWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<SupportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
                if (!await db.Database.CanConnectAsync(stoppingToken)) { await Task.Delay(3000, stoppingToken); continue; }
                if (!await ProcessChatAsync(scope.ServiceProvider, stoppingToken)) await ProcessDocumentAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Worker cycle failed ({ErrorType}); work remains recoverable.", error.GetType().Name); }
            await Task.Delay(1000, stoppingToken);
        }
    }
    private async Task ProcessDocumentAsync(IServiceProvider services, CancellationToken stoppingToken)
    {
        var db = services.GetRequiredService<SupportDbContext>();
        var now = DateTimeOffset.UtcNow;
        var version = await db.Versions.AsNoTracking().OrderBy(v => v.CreatedAt).FirstOrDefaultAsync(v => v.State == "queued" || ((v.State == "extracting" || v.State == "indexing") && v.LeaseUntil < now), stoppingToken);
        if (version is null) return;
        var lease = Guid.NewGuid();
        var claimed = await db.Versions.Where(v => v.Id == version.Id && (v.State == "queued" || ((v.State == "extracting" || v.State == "indexing") && v.LeaseUntil < now)))
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.State, "extracting").SetProperty(v => v.LeaseId, lease).SetProperty(v => v.LeaseUntil, now.AddMinutes(16)).SetProperty(v => v.ErrorCode, (string?)null), stoppingToken);
        if (claimed == 0) return;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken); deadline.CancelAfter(TimeSpan.FromMinutes(15));
        var token = deadline.Token;
        try
        {
            var engine = services.GetRequiredService<RagEngine>();
            await engine.EnsureCollectionAsync(token);
            var path = Path.Combine(configuration["Storage:Path"]!, version.StorageKey);
            var bytes = await File.ReadAllBytesAsync(path, token);
            if (bytes.LongLength != version.ByteCount || Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) != version.Hash)
                throw new ProviderFailure("document_integrity_failed");
            var chunks = await db.Chunks.Where(c => c.VersionId == version.Id).OrderBy(c => c.Ordinal).ToListAsync(token);
            if (chunks.Count == 0)
            {
                chunks = RagEngine.Extract(version, bytes).ToList();
                db.Chunks.AddRange(chunks); await db.SaveChangesAsync(token);
            }
            if (await db.Versions.Where(v => v.Id == version.Id && v.LeaseId == lease && v.State == "extracting").ExecuteUpdateAsync(s => s.SetProperty(v => v.State, "indexing").SetProperty(v => v.ChunkCount, chunks.Count), token) == 0) return;
            var count = 0;
            foreach (var chunk in chunks)
            {
                if (!await db.Versions.AnyAsync(v => v.Id == version.Id && v.LeaseId == lease && v.State == "indexing", token)) return;
                await engine.IndexAsync(chunk, token); count++;
                await db.Versions.Where(v => v.Id == version.Id && v.LeaseId == lease && v.State == "indexing").ExecuteUpdateAsync(s => s.SetProperty(v => v.IndexedCount, count), token);
            }
            if (!await engine.VerifyAsync(chunks, token)) throw new ProviderFailure("index_verification_failed");
            await db.Versions.Where(v => v.Id == version.Id && v.LeaseId == lease && v.State == "indexing")
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.State, "ready").SetProperty(v => v.LeaseUntil, (DateTimeOffset?)null), token);
        }
        catch (Exception error) when (!stoppingToken.IsCancellationRequested)
        {
            var code = error is ProviderFailure failure ? failure.Code : error is OperationCanceledException ? "ingestion_timeout" : "document_processing_failed";
            await db.Versions.Where(v => v.Id == version.Id && v.LeaseId == lease && v.State != "canceled").ExecuteUpdateAsync(s => s.SetProperty(v => v.State, "failed").SetProperty(v => v.ErrorCode, code), stoppingToken);
            logger.LogWarning("Document processing failed ({Code}, {ErrorType}).", code, error.GetType().Name);
        }
    }
    private async Task<bool> ProcessChatAsync(IServiceProvider services, CancellationToken stoppingToken)
    {
        var db = services.GetRequiredService<SupportDbContext>(); var now = DateTimeOffset.UtcNow;
        var request = await db.ChatRequests.AsNoTracking().OrderBy(r => r.CreatedAt).FirstOrDefaultAsync(r => r.State == "queued" || (r.State == "processing" && r.LeaseUntil < now), stoppingToken);
        if (request is null) return false;
        var lease = Guid.NewGuid();
        if (await db.ChatRequests.Where(r => r.Id == request.Id && (r.State == "queued" || (r.State == "processing" && r.LeaseUntil < now)))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, "processing").SetProperty(r => r.LeaseId, lease).SetProperty(r => r.LeaseUntil, now.AddMinutes(4)), stoppingToken) == 0) return true;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken); deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        var cancellationWatch = WatchCancellationAsync(request.Id, lease, deadline);
        try
        {
            var engine = services.GetRequiredService<RagEngine>();
            var revision = await db.Corpus.Select(c => c.Revision).SingleAsync(token);
            var allowed = await db.Documents.Where(d => !d.Deleted && d.ActiveVersionId != null).Select(d => d.ActiveVersionId!.Value).ToArrayAsync(token);
            var historyMessages = await db.Messages.Where(m => m.ConversationId == request.ConversationId && m.RequestId != request.Id && m.Role == "user").OrderByDescending(m => m.CreatedAt).Take(3).Select(m => m.Text).ToArrayAsync(token);
            var history = string.Join("\n", historyMessages.Reverse()); if (history.Length > 1000) history = history[^1000..];
            var query = string.Join("\n", historyMessages.Take(1).Append(request.Question));
            var ids = await engine.RetrieveAsync(query, allowed, token);
            var chunks = await db.Chunks.AsNoTracking().Where(c => ids.Contains(c.Id) && allowed.Contains(c.VersionId)).ToArrayAsync(token);
            chunks = chunks.OrderBy(c => Array.IndexOf(ids.ToArray(), c.Id)).ToArray();
            logger.LogInformation("RAG request {RequestId} retrieved {Count} authorized passages.", request.Id, chunks.Length);
            await db.ChatRequests.Where(r => r.Id == request.Id && r.State == "processing" && r.LeaseId == lease).ExecuteUpdateAsync(s => s.SetProperty(r => r.Stage, "Preparing your answer"), token);
            var answer = await engine.AnswerAsync(request.Question, history, chunks, token);
            logger.LogInformation("RAG request {RequestId} produced response kind {Kind}.", request.Id, answer.Kind);
            // Keep uncited model prose out of customer-visible policy answers.
            var text = answer.Kind switch
            {
                "answered" => string.Join("\n\n", answer.Claims.Select(c => c.Text)),
                "clarification" => answer.Question,
                "human_help" => "I can explain Haven policies, but I can’t access live orders or approve changes or refunds. You can review and submit a support ticket for human help.",
                _ => "I couldn’t find that in the available Haven policies. You can ask another question or get human help."
            };
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token);
            if (await db.Corpus.Select(c => c.Revision).SingleAsync(token) != revision) throw new ProviderFailure("knowledge_changed");
            if (!await db.Conversations.AnyAsync(c => c.Id == request.ConversationId && !c.Deleted, token)) throw new ProviderFailure("conversation_deleted");
            var complete = await db.ChatRequests.Where(r => r.Id == request.Id && r.State == "processing" && r.LeaseId == lease && !r.CancelRequested)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.State, "completed").SetProperty(r => r.Stage, "Complete").SetProperty(r => r.CompletedAt, DateTimeOffset.UtcNow)
                    .SetProperty(r => r.DurationMs, (long)(DateTimeOffset.UtcNow - request.CreatedAt).TotalMilliseconds), token);
            if (complete == 0) return true;
            var message = new Message { ConversationId = request.ConversationId, RequestId = request.Id, Role = "assistant", Kind = answer.Kind, Text = text };
            db.Messages.Add(message);
            if (answer.Kind == "answered") foreach (var claim in answer.Claims) db.Citations.Add(new Citation { MessageId = message.Id, ChunkId = Guid.Parse(claim.SourceId), Quote = claim.Quote, Claim = claim.Text });
            await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        }
        catch (Exception error) when (!stoppingToken.IsCancellationRequested)
        {
            var code = error is ProviderFailure failure ? failure.Code : error is OperationCanceledException ? "answer_timeout" : "answer_service_unavailable";
            await db.ChatRequests.Where(r => r.Id == request.Id && r.LeaseId == lease && r.State == "processing").ExecuteUpdateAsync(s => s.SetProperty(r => r.State, "failed").SetProperty(r => r.ErrorCode, code).SetProperty(r => r.CompletedAt, DateTimeOffset.UtcNow), stoppingToken);
            logger.LogWarning("Answer failed ({Code}, {ErrorType}).", code, error.GetType().Name);
        }
        finally
        {
            await deadline.CancelAsync();
            await cancellationWatch;
        }
        return true;
    }
    private async Task WatchCancellationAsync(Guid requestId, Guid lease, CancellationTokenSource deadline)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            while (!deadline.IsCancellationRequested)
            {
                await Task.Delay(1000, deadline.Token);
                if (!await db.ChatRequests.AnyAsync(r => r.Id == requestId && r.LeaseId == lease && r.State == "processing" && !r.CancelRequested, deadline.Token))
                { await deadline.CancelAsync(); break; }
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
        catch { await deadline.CancelAsync(); }
    }
}

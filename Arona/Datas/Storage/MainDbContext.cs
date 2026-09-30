using Microsoft.EntityFrameworkCore;

namespace Arona.Datas.Storage;

public class MainDbContext(DbContextOptions options, Config config) : DbContext(options)
{
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<Fact> Facts { get; set; }
    public DbSet<ApiProvider> ApiProviders { get; set; }
    public DbSet<ChatModel> ChatModels { get; set; }
    public DbSet<EmbeddingModel> EmbeddingModels { get; set; }
    public DbSet<StringConfig> Configs { get; set; }

    public bool EnsureCreated()
    {
        var res = Database.EnsureCreated();
        if(res)
        {
            Database.ExecuteSqlRaw(
                $"CREATE FULLTEXT CATALOG ftCatalog AS DEFAULT;" +
                $"CREATE FULLTEXT INDEX ON Facts(Content) KEY INDEX PK_Facts;" + 
                $"CREATE FULLTEXT INDEX ON ChatMessages(Message) KEY INDEX PK_ChatMessages;");
        }
        return res;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>()
            .Property(cm => cm.Embedding)
            .HasColumnType($"vector({config.EmbeddingDimension})");
        modelBuilder.Entity<Fact>()
            .Property(f => f.Embedding)
            .HasColumnType($"vector({config.EmbeddingDimension})");
        modelBuilder.Entity<ChatMessage>()
            .HasOne(cm => cm.Session)
            .WithMany(cs => cs.Messages)
            .HasForeignKey(cm => cm.SessionId)
            .IsRequired();
        modelBuilder.Entity<Fact>()
            .HasOne(f => f.Session)
            .WithMany(s => s.Facts)
            .HasForeignKey(f => f.SessionId)
            .IsRequired(false);
        modelBuilder.Entity<ChatModel>()
            .HasOne(cm => cm.Provider)
            .WithMany(p => p.ChatModels)
            .HasForeignKey(cm => cm.ProviderId)
            .IsRequired();
        modelBuilder.Entity<EmbeddingModel>()
            .HasOne(em => em.Provider)
            .WithMany(p => p.EmbeddingModels)
            .HasForeignKey(em => em.ProviderId)
            .IsRequired();
        base.OnModelCreating(modelBuilder);
    }
}

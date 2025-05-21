using Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntityFrameworkCore;

public sealed class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Asking> Askings { get; set; }
    public DbSet<Reply> Replies { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var userEntity = modelBuilder.Entity<User>();
        ConfigureUser(userEntity);
        var askingEntity = modelBuilder.Entity<Asking>();
        ConfigureAsking(askingEntity);
        var replyEntity = modelBuilder.Entity<Reply>();
        ConfigureReply(replyEntity);

        return;

        static void ConfigureUser(EntityTypeBuilder<User> userEntity)
        {
            userEntity
                .ToTable("user")
                .HasKey(static x => x.Id);
            userEntity
                .Property(static x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd()
                .IsRequired();
            userEntity
                .Property(static x => x.TelegramId)
                .HasColumnName("telegram_id")
                .IsRequired();
            userEntity
                .Property(static x => x.CurrentReplyingAskingId)
                .HasColumnName("current_replying_asking_id");
            userEntity
                .HasIndex(static x => x.TelegramId)
                .IsUnique();
        }

        static void ConfigureAsking(EntityTypeBuilder<Asking> askingEntity)
        {
            askingEntity
                .ToTable("asking")
                .HasKey(static x => x.Id);
            askingEntity
                .Property(static x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd()
                .IsRequired();
            askingEntity
                .Property(static x => x.UserId)
                .HasColumnName("user_id")
                .IsRequired();
            askingEntity
                .Property(static x => x.Text)
                .HasColumnName("text")
                .IsRequired()
                .HasMaxLength(Asking.MaxTextLength);
            askingEntity
                .Property(static x => x.Active)
                .HasColumnName("active")
                .HasDefaultValue(true)
                .IsRequired();
            askingEntity
                .Property(static x => x.CreatedAt)
                .HasColumnName("created_ts")
                .IsRequired();
            askingEntity
                .Property(static x => x.NeedRepliesCount)
                .HasColumnName("need_replies_count")
                .HasDefaultValue(1)
                .IsRequired();
            askingEntity
                .HasOne(static x => x.User)
                .WithMany(static x => x.Askings)
                .HasForeignKey(static x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
        
        static void ConfigureReply(EntityTypeBuilder<Reply> replyEntity)
        {
            replyEntity
                .ToTable("reply")
                .HasKey(static x => x.Id);
            replyEntity
                .Property(static x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd()
                .IsRequired();
            replyEntity
                .Property(static x => x.UserId)
                .HasColumnName("user_id")
                .IsRequired();
            replyEntity
                .Property(static x => x.AskingId)
                .HasColumnName("asking_id")
                .IsRequired();
            replyEntity
                .Property(static x => x.CreatedAt)
                .HasColumnName("created_ts")
                .IsRequired();
            replyEntity
                .Property(static x => x.Text)
                .HasColumnName("text")
                .IsRequired()
                .HasMaxLength(Asking.MaxTextLength);
            replyEntity
                .HasOne(static x => x.User)
                .WithMany(static x => x.Replies)
                .HasForeignKey(static x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            replyEntity
                .HasOne(static x => x.Asking)
                .WithMany(static x => x.Replies)
                .HasForeignKey(static x => x.AskingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

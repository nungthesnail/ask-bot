using Core.Models;
using EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTests;

[TestFixture]
public class AppDbContextTests
{
    [SetUp]
    public void Setup()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=eftest.db")
            .Options;
    }

    private DbContextOptions<AppDbContext> _options;

    [Test, Order(1)]
    public void AddUser_SavesUserToDatabase()
    {
        using (var context = new AppDbContext(_options))
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
            
            var user = new User { TelegramId = 1 };
            context.Users.Add(user);
            context.SaveChanges();

            var savedUser = context.Users.FirstOrDefault(u => u.TelegramId == 1);
            Assert.IsNotNull(savedUser);
            Assert.That(savedUser.TelegramId, Is.EqualTo(1));
        }
    }

    [Test, Order(2)]
    public void AddAsking_SavesAskingToDatabase()
    {
        using (var context = new AppDbContext(_options))
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var user = new User { TelegramId = 2 };
            context.Users.Add(user);
            context.SaveChanges();

            var asking = new Asking
                { Text = "Test question", UserId = user.Id, CreatedAt = DateTimeOffset.UtcNow, Active = true };
            context.Askings.Add(asking);
            context.SaveChanges();

            var savedAsking = context.Askings.FirstOrDefault(a => a.Text == "Test question");
            Assert.IsNotNull(savedAsking);
            Assert.That(savedAsking.Text, Is.EqualTo("Test question"));
            Assert.That(savedAsking.UserId, Is.EqualTo(user.Id));
        }
    }

    [Test, Order(3)]
    public void AddReply_SavesReplyToDatabase()
    {
        using (var context = new AppDbContext(_options))
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var user = new User { TelegramId = 3 };
            context.Users.Add(user);
            context.SaveChanges();

            var asking = new Asking
                { Text = "Test question", UserId = user.Id, CreatedAt = DateTimeOffset.UtcNow, Active = true };
            context.Askings.Add(asking);
            context.SaveChanges();

            var reply = new Reply
                { Text = "Test answer", UserId = user.Id, AskingId = asking.Id, CreatedAt = DateTimeOffset.UtcNow };
            context.Replies.Add(reply);
            context.SaveChanges();

            var savedReply = context.Replies.FirstOrDefault(r => r.Text == "Test answer");
            Assert.IsNotNull(savedReply);
            Assert.That(savedReply.Text, Is.EqualTo("Test answer"));
            Assert.That(savedReply.UserId, Is.EqualTo(user.Id));
            Assert.That(savedReply.AskingId, Is.EqualTo(asking.Id));
        }
    }

    [Test, Order(4)]
    public void AskingAndReplies_RelationshipWorksCorrectly()
    {
        using (var context = new AppDbContext(_options))
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var user = new User { TelegramId = 4 };
            context.Users.Add(user);
            context.SaveChanges();

            var asking = new Asking
                { Text = "Test question", UserId = user.Id, CreatedAt = DateTimeOffset.UtcNow, Active = true };
            context.Askings.Add(asking);
            context.SaveChanges();

            var reply1 = new Reply
                { Text = "Test answer 1", UserId = user.Id, AskingId = asking.Id, CreatedAt = DateTimeOffset.UtcNow };
            var reply2 = new Reply
                { Text = "Test answer 2", UserId = user.Id, AskingId = asking.Id, CreatedAt = DateTimeOffset.UtcNow };
            context.Replies.AddRange(reply1, reply2);
            context.SaveChanges();

            var loadedAsking = context.Askings.Include(a => a.Replies).FirstOrDefault(a => a.Id == asking.Id);
            Assert.IsNotNull(loadedAsking);
            Assert.IsNotNull(loadedAsking.Replies);
            Assert.That(loadedAsking.Replies.Count, Is.EqualTo(2));
            Assert.IsTrue(loadedAsking.Replies.Any(r => r.Text == "Test answer 1"));
            Assert.IsTrue(loadedAsking.Replies.Any(r => r.Text == "Test answer 2"));
        }
    }

    [Test, Order(6)]
    public void EnsureDatabaseIsCreatedAndDeleted()
    {
        using (var context = new AppDbContext(_options))
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            Assert.IsTrue(context.Database.CanConnect());
        }
    }
}

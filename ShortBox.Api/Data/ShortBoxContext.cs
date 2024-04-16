using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ShortBox.Api.Data; 
public class ShortBoxContext : DbContext {
    public ShortBoxContext(DbContextOptions<ShortBoxContext> options) : base(options)
    {
        
    }

    public DbSet<Book> Books => this.Set<Book>();
    public DbSet<PullListEntry> PullList => this.Set<PullListEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<BookId>()
            .HaveConversion<IntIdConverter<BookId>>();

        configurationBuilder
            .Properties<PullListEntryId>()
            .HaveConversion<IntIdConverter<PullListEntryId>>();
    }

    private class IntIdConverter<T> : ValueConverter<T, int> where T : IntId 
    {
        public IntIdConverter() : base(
            id => id.Value, 
            value => (T)Activator.CreateInstance(typeof(T), value)!)
        {
        }
    }
}


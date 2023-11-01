using Microsoft.EntityFrameworkCore;

namespace ShortBox.Api.Data; 
public class ShortBoxContext : DbContext {
    public ShortBoxContext(DbContextOptions<ShortBoxContext> options) : base(options)
    {
        
    }

    public DbSet<Book> Books => this.Set<Book>();
}

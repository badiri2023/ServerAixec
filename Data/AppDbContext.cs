using Microsoft.EntityFrameworkCore;
using AixecAPI.Models;

namespace AixecAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<PlayerCard> PlayerCards => Set<PlayerCard>();
}
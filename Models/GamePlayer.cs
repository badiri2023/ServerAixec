namespace AixecAPI.Models;

public class GamePlayer
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int Score { get; set; } = 0;
    public int Level { get; set; } = 1;
    public bool IsCurrentTurn { get; set; } = false;
}
namespace AixecAPI.Models;

public class PlayerCard
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
    public int Quantity { get; set; } = 1;
}
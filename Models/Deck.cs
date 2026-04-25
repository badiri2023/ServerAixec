namespace AixecAPI.Models;

public class Deck
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = "Mi Mazo Principal";
    
    // Relación con las cartas que componen el mazo
    public List<DeckCard> DeckCards { get; set; } = new();
}

public class DeckCard
{
    public int Id { get; set; }
    public int DeckId { get; set; }
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
}
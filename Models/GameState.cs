namespace AixecAPI.Models
{
    public class GameState
    {
        public int GameId { get; set; }
        public string Status { get; set; } = "waiting"; // "waiting" o "playing"
        public int CurrentTurnUserId { get; set; }
        public int Round { get; set; } = 1;
        public List<PlayerGameState> Players { get; set; } = new();
        public List<FieldCard> Field { get; set; } = new();
    }

public class PlayerGameState
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        
        public List<int> HandCardIds { get; set; } = new(); // Cartas en la mano
        public int CardsInHand => HandCardIds.Count;       
        
        public List<int> DeckCardIds { get; set; } = new(); 
        public int CardsInDeck => DeckCardIds.Count; // Lo que verá el rival/Godot

        public int Health { get; set; } = 5;
        public int Mana { get; set; } = 1;
        public int MaxMana { get; set; } = 8;
    }

    public class FieldCard
    {
        public int UserId { get; set; }
        public int CardId { get; set; }
        public string CardName { get; set; } = string.Empty;
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int SlotIndex { get; set; }
        public bool HasAttacked { get; set; } = false;
    }
}
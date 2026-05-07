namespace AixecAPI.Models
{
    public class GameState
    {
        public int GameId { get; set; }
        public List<PlayerHandInfo> Players { get; set; } = new();
    }

    public class PlayerHandInfo
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int CardsInHand { get; set; }
    }
}

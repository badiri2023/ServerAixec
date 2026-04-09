namespace AixecAPI.Models
{
    public class Game
    {
        public int Id { get; set; }
        public string Status { get; set; } = "waiting"; // waiting, playing, finished
        public int CurrentTurn { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<GamePlayer> Players { get; set; } = new();

    }
}

namespace AixecAPI.Models
{
    public class Card
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Attack {  get; set; }
        public int Defense { get; set; }
        public int Rarity { get; set; }
        public Ability Ability { get; set; }
        public string Expansion { get; set; }
        public int Mana { get; set; }
        public string Type { get; set; }
    }
}

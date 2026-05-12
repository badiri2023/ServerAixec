using AixecAPI.Models;
using AixecAPI.Data;                  
using Microsoft.EntityFrameworkCore;  
using System.Linq;
using System.Threading.Tasks;
namespace AixecAPI.Logic;

public static class GameEngine
{
    // Constantes sincronizadas con tu Godot (GameUI.gd)
    public const int VIDA_MAXIMA = 5;
    public const int MANA_MAXIMO = 8;
    public const int MAX_MONSTRUOS = 3;

public static string? TryPlayCard(GameState state, int userId, Card cardData, int slotIndex)
    {
        var player = state.Players.FirstOrDefault(p => p.UserId == userId);
        if (player == null) return "Jugador no encontrado.";
        if (state.CurrentTurnUserId != userId) return "No es tu turno.";
        
        // 1. Validar Maná
        if (player.Mana < cardData.Mana) return "Maná insuficiente.";

        // 2. Validar que tiene la carta en mano
        if (!player.HandCardIds.Contains(cardData.Id)) return "No tienes esa carta en la mano.";

        // 3. Validar espacio en campo
        if (state.Field.Count(f => f.UserId == userId) >= MAX_MONSTRUOS) 
            return "Campo de monstruos lleno.";

        // --- EJECUCIÓN DE LA JUGADA ---
        
        // A) Restamos el maná y quitamos la carta de la mano
        player.Mana -= cardData.Mana;
        player.HandCardIds.Remove(cardData.Id);

        // B) Creamos la carta y la añadimos al campo directamente
        state.Field.Add(new FieldCard
        {
            UserId = userId,
            CardId = cardData.Id,
            CardName = cardData.Name,
            Attack = cardData.Attack,
            Defense = cardData.Defense,
            SlotIndex = slotIndex
        });

        // C) Ejecutamos la habilidad (si la carta tiene una)
        if (cardData.Ability != null)
        {
            EjecutarHabilidadAlJugar(state, userId, cardData.Ability);
        }

        // Todo salió bien
        return null; 
    }
    public static string? TryAttack(GameState state, int userId, int attackerId, int? targetId)
    {
        if (state.CurrentTurnUserId != userId) return "No es tu turno.";

        // Buscamos la carta atacante en el campo (usando CardId)
        var attacker = state.Field.FirstOrDefault(f => f.CardId == attackerId && f.UserId == userId);
        if (attacker == null) return "Atacante no encontrado en el tablero.";
        if (attacker.HasAttacked) return "Esta carta ya ha atacado este turno.";

        var oponente = state.Players.FirstOrDefault(p => p.UserId != userId);
        if (oponente == null) return "No se encontró al oponente.";

        // ATAQUE DIRECTO AL JUGADOR (Si no hay objetivo seleccionado)
        if (targetId == null)
        {
            oponente.Health -= attacker.Attack;
        }
        // ATAQUE A OTRA CARTA
        else
        {
            var target = state.Field.FirstOrDefault(f => f.CardId == targetId && f.UserId != userId);
            if (target == null) return "El objetivo ya no está en el tablero.";

            target.Defense -= attacker.Attack;
            
            // Si la defensa llega a 0, la carta se elimina (muere)
            if (target.Defense <= 0) state.Field.Remove(target);
        }

        attacker.HasAttacked = true;
        return null;
    }

    public static void ProcessEndTurn(GameState state){
        // 1. Resetear el estado de ataque para todas las cartas en el campo
        foreach (var card in state.Field) card.HasAttacked = false;

        // 2. Calcular quién es el siguiente jugador
        var currentIndex = state.Players.FindIndex(p => p.UserId == state.CurrentTurnUserId);
        var nextIndex = (currentIndex + 1) % state.Players.Count;
        
        var nextPlayer = state.Players[nextIndex];
        state.CurrentTurnUserId = nextPlayer.UserId;

        // 3. Lógica del inicio de turno del nuevo jugador:
        
        // a) Robar una carta si le quedan en el mazo
        if (nextPlayer.DeckCardIds.Count > 0)
        {
            int cardToDraw = nextPlayer.DeckCardIds[0]; // Cogemos la primera
            nextPlayer.DeckCardIds.RemoveAt(0);         // La quitamos del mazo
            nextPlayer.HandCardIds.Add(cardToDraw);     // La metemos en la mano
        }

        // 4. Si el ciclo vuelve al Jugador 1 (índice 0), es una nueva Ronda
        if (nextIndex == 0)
        {
            state.Round++;
            foreach (var p in state.Players)
            {
                // Aumentar maná máximo hasta un tope (ej. 8) y rellenarlo
                if (p.MaxMana < MANA_MAXIMO) p.MaxMana++;
                p.Mana = p.MaxMana;
            }
        }
    }

public static async Task PlayBotTurn(GameState state, AppDbContext db)
    {
        var botPlayer = state.Players.FirstOrDefault(p => p.UserId == 10);
        if (botPlayer == null) return;

        bool playedSomething = true;
        
        // El bot intentará jugar cartas mientras le siga sobrando maná y tenga huecos
        while (playedSomething)
        {
            playedSomething = false;

            // 1. Obtenemos las cartas reales que tiene el bot en la mano
            var handCards = await db.Cards
                .Where(c => botPlayer.HandCardIds.Contains(c.Id))
                .ToListAsync();

            // 2. Buscamos la primera carta que le alcance con su maná
            var cardToPlay = handCards.FirstOrDefault(c => c.Mana <= botPlayer.Mana);

            if (cardToPlay != null)
            {
                // 3. Buscamos un hueco vacío en el tablero (0, 1 o 2)
                int emptySlot = -1;
                for (int i = 0; i < MAX_MONSTRUOS; i++)
                {
                    // Si no hay ninguna carta del bot en este slot, está vacío
                    if (!state.Field.Any(f => f.UserId == 10 && f.SlotIndex == i))
                    {
                        emptySlot = i;
                        break;
                    }
                }

                // 4. Si hay hueco, jugamos la carta
                if (emptySlot != -1)
                {
                    TryPlayCard(state, 10, cardToPlay, emptySlot);
                    playedSomething = true; // Volvemos a intentar por si podemos jugar otra
                }
            }
        }
    }

// gestor d ehabilidades
    public static void EjecutarHabilidadAlJugar(GameState state, int userId, Ability ability)
    {
        // Encontramos quién es el jugador que lanzó la carta y quién es su oponente
        var player = state.Players.FirstOrDefault(p => p.UserId == userId);
        var opponent = state.Players.FirstOrDefault(p => p.UserId != userId);
        
        if (player == null || opponent == null) return;

        // Evaluamos qué hace la habilidad por su Nombre (o podrías usar su ID)
        switch (ability.Id)
        {
            case 1:
                opponent.Health -= 2;
                break;
            
            case 2: 
                player.Health += 3;
                if (player.Health > VIDA_MAXIMA) player.Health = VIDA_MAXIMA;
                break;

            case 3: 
                if (player.DeckCardIds.Count > 0)
                {
                    player.HandCardIds.Add(player.DeckCardIds[0]);
                    player.DeckCardIds.RemoveAt(0);
                }
                break;

            case 4: 
                player.Mana += 1;
                break;
                
            // añadir la logica de las habilidades completa
        }
    }
}

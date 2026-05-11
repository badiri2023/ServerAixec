using AixecAPI.Models; // Única importación necesaria para los datos
using System.Linq;

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
        
        // 1. Validar Maná (Usa .Mana o .ManaCost según tu modelo Card de la DB)
        if (player.Mana < cardData.Mana) return "Maná insuficiente.";

        // 2. Validar que tiene la carta en mano
        if (!player.HandCardIds.Contains(cardData.Id)) return "No tienes esa carta en la mano.";

        // 3. Validar espacio en campo
        if (state.Field.Count(f => f.UserId == userId) >= MAX_MONSTRUOS) 
            return "Campo de monstruos lleno.";

        // --- EJECUCIÓN DE LA JUGADA ---
        player.Mana -= cardData.Mana;
        player.HandCardIds.Remove(cardData.Id);
        
        state.Field.Add(new FieldCard {
            UserId = userId,
            CardId = cardData.Id,
            CardName = cardData.Name,
            Attack = cardData.Attack,
            Defense = cardData.Defense,
            SlotIndex = slotIndex,
            HasAttacked = true // No pueden atacar el mismo turno que entran
        });

        return null; // Éxito
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

    public static void ProcessEndTurn(GameState state)
    {
        // 1. Resetear el estado de ataque para todas las cartas
        foreach (var card in state.Field) card.HasAttacked = false;

        // 2. Calcular quién es el siguiente jugador
        var currentIndex = state.Players.FindIndex(p => p.UserId == state.CurrentTurnUserId);
        var nextIndex = (currentIndex + 1) % state.Players.Count;
        state.CurrentTurnUserId = state.Players[nextIndex].UserId;

        // 3. Si el ciclo vuelve al Jugador 1 (índice 0), es una nueva Ronda
        if (nextIndex == 0)
        {
            state.Round++;
            foreach (var p in state.Players)
            {
                // Aumentar maná máximo hasta el límite y recargar
                if (p.MaxMana < MANA_MAXIMO) p.MaxMana++;
                p.Mana = p.MaxMana; 
            }
        }
    }
}

# Aixec Card Game: Wiki del servidor y la base de datos

## Diseño de la base de datos

La base de datos consta de 10 tablas:

- **Users**: Donde se almacenan los usuarios registrados en el juego.
- **Cards**: Donde se guardan las cartas del juego.
- **PlayerCards**: Tabla relacional donde se guardan las cartas que tiene cada jugador en su colección.
- **Decks**: Donde se guardan los identificadores de mazo junto a su nombre y el id de usuario.
- **DeckCards**: Tabla relacional donde se guardan las cartas que tiene cada mazo.
- **Abilities**: Donde se almacenan las habilidades existentes en todas las cartas.
- **Games**: El historial de partidas jugadas y en progreso.
- **GamePlayers**: Donde se guarda el estado de un jugador en la partida en la que está.
- **ChatMessages**: Donde se almacenan los mensajes enviados por chat.
- **EFMigrationHistory**: Historial de cambios de la base de datos por Entity Framework.

---

# Endpoints: Requests y respuestas

A continuación se describen todos los endpoints agrupados por lo que devuelven, qué uso se les da, qué envían, qué devuelven y qué errores posibles contemplan.

---

# AuthController  

**Ruta:** `api/auth`

## GET /ping

Comprueba que la API funciona correctamente enviando un mensaje.

### Request

No envías nada.

### Response

```
{ "mensaje": "¡La API está viva y conectada!"}
```

---

## GET /perfil

Obtiene el perfil del usuario.

### Request

No envías nada.

### Response

```
{ "id": 1, "username": "usuario", "money": 500, "level": 3}
```

### Errores posibles

- `401`: No hay token o es inválido
- `404`: Usuario no encontrado

---

## POST /register

Registra un nuevo usuario y obtiene un token JWT.

### Request

```
{ "username": "usuario", "email": "usuario@gmail.com", "password": "contraseña"}
```

### Response

```
{ "token": "tokenJWTlargo"}
```

### Errores posibles

- `400`: El email ya está en uso

---

## POST /login

Inicia sesión con un usuario existente y obtiene un token JWT.

### Request

```
{ "email": "usuario@gmail.com", "password": "contraseña"}
```

### Response

```
{ "token": "tokenJWTlargo"}
```

### Errores posibles

- `401`: Credenciales incorrectas

---

## POST /loginprueba

Misma funcionalidad que el login pero, en caso de introducir incorrectamente las credenciales, devuelve la contraseña hasheada.

### Request

```
{ "email": "usuario@gmail.com", "password": "contraseña"}
```

### Response

Correcto:

```
{ "token": "tokenJWTlargo"}
```

Incorrecto:

```
{ "hashedPassword": "contraseñaHasheada"}
```

---

# CardController

**Ruta:** `api/card`

## GET /:id

Devuelve una carta incluyendo la habilidad.

### Request

Envía el id por la URL.

### Response

```
{  
	"id": 5,  
	"name": "IronSlime",  
	"description": "...",  
	"attack": 2,  
	"defense": 2,  
	"rarity": 1,  
	"mana": 1,  
	"expansion": "Fantásticas",  
	"type": "Monstruo",  
	"ability": 
		{    
			"name": "Resiliencia",    
			"description": "..."  
		}
}
```

### Errores posibles

- `404`: Carta no encontrada

---

## GET /

Devuelve todas las cartas de la base de datos.

### Request

No envía nada.

### Response

Una array con el siguiente formato:

```
[
    {
        "id": 1,
        "name": "MagmaSlime",
        "description": "..",
        "attack": 1,
        "defense": 1,
        "rarity": 1,
        "ability": {
            "id": 1,
            "name": "Cuerpo elemental",
            "description": "...",
            "isPassive": true
        },
        "expansion": "Fantasticas",
        "mana": 1,
        "type": 1,
        "imageUrl": "aixec-card-images.s3.eu-north-1.amazonaws.com/card001.jpg"
    },
    {
	    "id": 2,
		"name": "AcidSlime",
		"description": "...",
		"attack": 1,
		"defense": 1,
		"rarity": 1,
		"ability": {
			"id": 1,
			"name": "Cuerpo elemental",
			"description": "...",
			"isPassive": true
		},
		"expansion": "Fantasticas",
		"mana": 1,
		"type": 1,
		"imageUrl": "aixec-card-images.s3.eu-north-1.amazonaws.com/card002.jpg"
    },
]
```

---

## GET /my-cards

Obtiene todas las cartas que tiene el usuario en la colección.

### Request

No envía nada.

### Response

Una array con el siguiente formato:

```
[
    {
        "id": 19,
        "name": "Hombre de Accion de edicion limitada",
        "description": "...",
        "type": 1,
        "attack": 2,
        "defense": 3,
        "rarity": 2,
        "mana": 3,
        "expansion": "Juguetes",
        "ability": {
            "name": "Paciencia"
        },
        "quantity": 2
    },
    {
        "id": 17,
        "name": "Canon comicamente grande",
        "description": "...",
        "type": 1,
        "attack": 2,
        "defense": 4,
        "rarity": 2,
        "mana": 3,
        "expansion": "Juguetes",
        "ability": {
            "name": "Precoz"
        },
        "quantity": 2
    },
    {...}
]
```

---

## GET /give

Añade una carta a la tabla `PlayerCards` del jugador o aumenta la cantidad en uno si ya la tiene.

### Request

```
{ "userId": 1, "cardId": 5}
```

### Response

```
200 OK
```

### Errores posibles

- `401`: No hay token o es inválido

---

## GET /open/:expansion

Añade a la colección del jugador tres cartas de una expansión concreta y descuenta 100 monedas al jugador.

### Request

Envía el nombre de la expansión en la URL.

### Response

```
[
    {
        "id": 21,
        "name": "Robot",
		"description": "...",
        "attack": 2,
        "defense": 1,
        "rarity": 1,
        "ability": {
            "id": 32,
            "name": "Existencia",
            "description": "No hace nada.",
            "isPassive": true
        },
        "expansion": "Futuristico",
        "mana": 1,
        "type": 1,
        "imageUrl": "aixec-card-images.s3.eu-north-1.amazonaws.com/card021.jpg"
    },
    {
        "id": 26,
        "name": "Canon de plasma",
        "description": "...",
        "attack": 3,
        "defense": 1,
        "rarity": 2,
        "ability": {
            "id": 20,
            "name": "Troyan",
            "description": "Hace 2 de dano a un enemigo aleatorio.",
            "isPassive": false
        },
        "expansion": "Futuristico",
        "mana": 3,
        "type": 1,
        "imageUrl": "aixec-card-images.s3.eu-north-1.amazonaws.com/card026.jpg"
    },
    {
        "id": 26,
        "name": "Canon de plasma",
        "description": "...",
        "attack": 3,
        "defense": 1,
        "rarity": 2,
        "ability": {
            "id": 20,
            "name": "Troyan",
            "description": "Hace 2 de dano a un enemigo aleatorio.",
            "isPassive": false
        },
        "expansion": "Futuristico",
        "mana": 3,
        "type": 1,
        "imageUrl": "aixec-card-images.s3.eu-north-1.amazonaws.com/card026.jpg"
    }
]
```

---

# ChatController

**Ruta:** `/api/chat`

## GET /history

Devuelve los últimos 100 mensajes del chat global.

### Request

No envía nada.

### Response

```
{  
	"id": 1,  
	"userId": 3,  
	"message": "hola familia",  
	"createdAt": "2026-04-28T10:00:00"}
```

---

# DeckController

**Ruta:** `/api/deck`

## GET /

Devuelve todos los mazos del usuario con un resumen de las cartas.

### Request

No envía nada.

### Response

```
{  
	"id": 1,  
	"name": "Mi mazo",  
	"cardCount": 20,  
	"cards": 
		[    
			{      
			"id": 1,      
			"name": "MagmaSlime",      
			"type": "Monstruo",      
			"rarity": 1    
			}  
		]
}
```

### Errores posibles

- `401`: No hay token o es inválido

---

## GET /:id

Devuelve el mazo especificado con todas las cartas al completo.

### Request

Envía el id en la URL.

---

## POST /

Crea un mazo con las cartas especificadas en el array.

### Request

```
{ "name": "Mi mazo personalizado", "cardIds": [1, 2, 5, 6, 9, 15]}
```

### Response

```
{ "id": 1, "name": "Mi mazo personalizado", "cardCount": 6}
```

### Errores posibles

- `400`: El mazo debe tener al menos una carta
- `401`: No hay token o no es válido

---

## PUT /:id

Reemplaza el nombre y las cartas del mazo especificado por el id.

### Request

```
{ "name": "Nuevo nombre", "cardIds": [1, 2, 5, 6, 9, 15]}
```

### Response

```
{ "id": 1, "name": "Nuevo nombre", "cardCount": 6}
```

### Errores posibles

- `401`: No hay token o es inválido
- `403`: El mazo no pertenece al usuario
- `404`: Mazo no encontrado

---

## DELETE /:id

Elimina el mazo especificado.

### Request

Envía el id en la URL.

### Response

```
{  "message": "Deck eliminado correctamente"}
```

---

# GameController

**Ruta:** `/api/game`

## GET /:id

Devuelve el estado actual de la partida.

### Request

Envía el id de la partida en la URL.

### Response

```
{  "id": 1, 
	"status": "playing", 
	"currentTurn": 2,  
	"players": [ 
		{
		 "userId": 1,      
		 "username": "user1",      
		 "score": 0,      
		 "level": 1,      
		 "isCurrentTurn": true   
		 },    
		 {      
		  "userId": 2    
		  }  
	]
}
```

### Errores posibles

- `401`: No hay token o es inválido
- `404`: Partida no encontrada

---

## POST /create

Crea una nueva partida y añade al usuario.

### Request

No envía nada.

### Response

```
{ "gameId": 1}
```

### Errores posibles

- `401`: No hay token o es inválido

---

## POST /join/:id

Une al usuario a la partida especificada en la URL y cambia el estado de la partida a `playing`.

### Request

Envía el id por la URL.

### Response

```
{ "message": "Te has unido a la partida"}
```

### Errores posibles

- `400`: La partida ya ha comenzado
- `400`: Ya estás en esta partida
- `401`: No hay token o es inválido
- `404`: Partida no encontrada

---

## POST /:id/turn

Pasa el turno al siguiente jugador.

### Request

Envía el id por la URL.

### Response

```
{ "nextUserId": 2}
```

### Errores posibles

- `400`: No es tu turno
- `401`: No hay token o es inválido
- `404`: Partida no encontrada

---

## POST /finish

Finaliza una partida, suma las victorias y las partidas jugadas y reparte las monedas según el resultado.

### Request

```
{  "winnerUserId": 1,  "loserUserId": 2}
```

### Response

```
{
	"winner": 
	{    
		"id": 1,    
		"username": "user1",    
		"wonMatches": 5,    
		"playedMatches": 8,    
		"money": 550  
	},  "loser": 
	{    
		"id": 2,    
		"username": "user2",    
		"playedMatches": 6,    
		"money": 275  
	}
}
```

### Errores posibles

- `400`: No es tu turno
- `401`: No hay token o es inválido
- `404`: Partida no encontrada

---

# RankingController
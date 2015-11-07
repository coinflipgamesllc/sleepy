# Sleepy
## A REST Client for Unity games

### Usage:

1. In the Editor, open `Coin Flip Games » Sleepy REST Client` from the toolbar.
2. Set the HTTP Endpoint to the URL of the server you're working with including the "http://" or "https://". You can change this in development and production (more on this later). Leave any trailing slashes *off*.
3. Set an API Key and Secret. These are used for signing requests that should be secure. The signature allows the request to succeed only once and can't be spoofed. Make sure these are sufficiently long and kept secret.
4. Start defining routes!

### Routes:

A `Route` consists of a name, method, path and extra settings for if it should be signed and if it should return immediately or block execution.

#### Some notes:
- The name *must* be unique among your routes and can be anything. You'll be referencing it everytime you want to make a request though.
- The path should be the route to your resource that you want to access with the leading slash.
- Async requests return immediately and the callback methods you define are run when the response has been returned or an error occurs.
- Sync requests block on the main thread and should only be used if you absolutely cannot do something without some server information. This includes *all* interaction in the game so it will feel as though the game has frozen.

#### Example:
- HTTP Endpoint: localhost:3000
- API Key: 4bde19f1-cf34-40c7-8d82-5af3972f685d
- API Secret: 711da9ba-1f79-49e7-b8ee-ea3d2536019cf3ac29de-e520-4457-a078-6aa2871eb79f
- Routes
  - Name: GetScores
	- Method: GET
	- Path: /games/:game/leaderboards/:leaderboard/scores
	- IsSigned: false
	- IsAsync: true
  - Name: SubmitScore
    - Method: POST
    - Path: /games/:game/leaderboards/:leaderboard/scores
    - IsSigned: true
    - IsAsync: false
  - ...

```C#
using CoinFlipGames.Sleepy;

...

RestClient client = new RestClient();

Dictionary<string, string> replacements = new Dictionary<string, string> ();
replacements.Add (":game", "My Awesome Game");
replacements.Add (":leaderboard", "Level 1");

// This will make the request -> GET "localhost:3000/games/My%20Awesome%20Game/leaderboards/Level%201/scores"
client.Request ("GetScores", replacements, (Hashtable response) => {
	// Success callback.
	Debug.LogError ("Awesome! " + response ["message"].toString ());
}, (string error) => {
	// Error callback.
	Debug.LogError ("Something bad happened! " + error);
});

// This will be called immediately, not after the response comes back
SomeOtherFunction ();
```

```C#
using CoinFlipGames.Sleepy;

...

RestClient client = new RestClient();

Dictionary<string, string> replacements = new Dictionary<string, string> ();
replacements.Add (":game", "My Awesome Game");
replacements.Add (":leaderboard", "Level 1");

WWWForm data = new WWWForm ();
data.AddField ("player", "321");
data.AddField ("value", "20000");

// This will make the request -> POST "localhost:3000/games/My%20Awesome%20Game/leaderboards/Level%201/scores?signature=XXXXXX&t=123" --data {"player": "321", "value": "20000"}
client.Request ("GetScores", replacements, data, (Hashtable response) => {
	// Success callback.
	Debug.LogError ("Awesome! " + response ["message"].toString ());
}, (string error) => {
	// Error callback.
	Debug.LogError ("Something bad happened! " + error);
});

// This will be called after the response comes back
SomeOtherFunction ();
```
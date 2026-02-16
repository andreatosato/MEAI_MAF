using Microsoft.Extensions.AI;

namespace Project3.SimpleAgent;

public static class TheSportsDbFunctions
{
    public static IEnumerable<AITool> GetFunctions(IHttpClientFactory httpClientFactory, string apiKey)
    {
        yield return AIFunctionFactory.Create(
            async (string teamName) =>
            {
                var httpClient = httpClientFactory.CreateClient();
                var url = $"https://www.thesportsdb.com/api/v1/json/{apiKey}/searchteams.php?t={Uri.EscapeDataString(teamName)}";
                var response = await httpClient.GetStringAsync(url);
                return response;
            },
            name: "search_team",
            description: "Cerca informazioni su una squadra di calcio per nome. Restituisce dettagli come ID, nome completo, stadio, anno di fondazione, logo, ecc.");

        yield return AIFunctionFactory.Create(
            async (string leagueId) =>
            {
                var httpClient = httpClientFactory.CreateClient();
                var url = $"https://www.thesportsdb.com/api/v1/json/{apiKey}/eventsnextleague.php?id={leagueId}";
                var response = await httpClient.GetStringAsync(url);
                return response;
            },
            name: "get_league_events",
            description: "Ottiene i prossimi eventi/partite di un campionato. Per Serie A italiana usa leagueId=4332");

        yield return AIFunctionFactory.Create(
            async (string teamId) =>
            {
                var httpClient = httpClientFactory.CreateClient();
                var url = $"https://www.thesportsdb.com/api/v1/json/{apiKey}/lookup_all_players.php?id={teamId}";
                var response = await httpClient.GetStringAsync(url);
                return response;
            },
            name: "get_team_players",
            description: "Ottiene la lista di tutti i giocatori di una squadra dato il suo ID.");

        yield return AIFunctionFactory.Create(
            async (string teamId) =>
            {
                var httpClient = httpClientFactory.CreateClient();
                var url = $"https://www.thesportsdb.com/api/v1/json/{apiKey}/eventslast.php?id={teamId}";
                var response = await httpClient.GetStringAsync(url);
                return response;
            },
            name: "get_last_events",
            description: "Ottiene gli ultimi 5 eventi/partite giocate da una squadra dato il suo ID.");
    }
}

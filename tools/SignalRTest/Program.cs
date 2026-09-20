using Microsoft.AspNetCore.SignalR.Client;
using webServer.EntitiesEF;
using webServer.Models.Enums;

namespace SignalRTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var conn = new HubConnectionBuilder()
                .WithUrl("ws://localhost:5119/hubs/chat?token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwOGRlMzQ1YS0wOTliLTQ1YzgtOGNlMy01MmU2NDNmYTU2NGEiLCJlbWFpbCI6ImRwODR4eEBnbWFpbC5jb20iLCJQcm9maWxlc0lkIjoiMDhkZTM0NWEtMDk5Yi00YjYzLThkNjQtNTljZTgwN2Q3OGVmIiwianRpIjoiMDE5ZGI0M2MtNmNlYS00YzAzLWE3Y2EtNTlkMzZjMDFmNThjIiwiZXhwIjoxNzY1MDk5MjQ3LCJpc3MiOiJJbmZpbml0ZU1hcEFQSSIsImF1ZCI6IkluZmluaXRlTWFwQ2xpZW50cyJ9.0XTU_kaArPWYS6q9XsqwJWHb1IiigP3a1HA3dXqK5W0")
                .Build();

            await conn.StartAsync();
            var senderPlayerId = Guid.Empty;
            await conn.InvokeAsync("SendMessage", "hi", ChatChannel.General, senderPlayerId, null);


            //add here possibility to read all chats
            var ax = await conn.InvokeAsync<List<ChatGlobalMessage>>("GetGeneralMessages");

            Console.Write(ax);
        }
    }
}

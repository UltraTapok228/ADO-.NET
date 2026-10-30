using System.Data;
using Dapper;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
string cs = Environment.GetEnvironmentVariable("CONN_STRING")!;

app.MapPost("/tickets", async (TicketReq t) =>
{
    return Results.Ok();
});

app.MapGet("/sessions/dapper", async () =>
{
    return Results.Ok();
});

app.MapGet("/sessions/datatable", async () =>
{
    return Results.Ok();
});

app.MapGet("/tickets/dataset", () =>
{
    return Results.Ok();
});

app.Run();
record TicketReq(int SessionId, int[] Seats);

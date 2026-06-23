var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "TaskApi running");

app.Run();

public partial class Program { }

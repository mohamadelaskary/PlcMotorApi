using Microsoft.EntityFrameworkCore;
using PlcMotorApi.Data;
using PlcMotorApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<IPlcService, PlcService>();
builder.Services.AddSingleton<ISseNotifier, SseNotifier>();
builder.Services.AddSingleton<PlcMonitorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PlcMonitorService>());

var app = builder.Build();

// Auto-create/update the database on startup so you don't need to run
// "dotnet ef" commands manually the first time.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseAuthorization();
app.MapControllers();

app.Run();

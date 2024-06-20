using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<ICacheService>(provider => new CacheService(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHostedService<CacheCleanupService>();
builder.Services.AddControllers();

var app = builder.Build();

if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseRouting();

app.UseMiddleware<CacheMiddleware>();

app.Run();

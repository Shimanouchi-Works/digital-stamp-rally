using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using DigitalStampRally.Services;
using DigitalStampRally.Database;

var builder = WebApplication.CreateBuilder(args);

/// dotnet ef dbcontext scaffold "server=127.0.0.1;port=33060;database=DigitalStampRally;uid=root;password=yourpassword" Pomelo.EntityFrameworkCore.MySql -o Database/App -f -n DigitalStampRally.Database --no-onconfiguring

//
// --------------------
// Services
// --------------------
//
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IProjectDraftStore, MemoryProjectDraftStore>();
// builder.Services.AddSingleton<IProjectStore, FileProjectStore>();
// builder.Services.AddSingleton<IStampLogStore, FileStampLogStore>();
// builder.Services.AddSingleton<IAchievementStore, FileAchievementStore>();
// builder.Services.AddSingleton<IGoalStore, FileGoalStore>();
builder.Services.AddScoped<DbEventService>();
builder.Services.AddScoped<DbStampService>();

// Razor Pages
builder.Services.AddRazorPages(options =>
{
    // 管理画面はログイン必須、など後でまとめて指定できる
    // options.Conventions.AuthorizeFolder("/Admin");
});

// ★ これが必須
builder.Services.AddDbContext<DigitalStampRallyContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("Default");
    options.UseMySql(
        cs,
        ServerVersion.AutoDetect(cs)
    );
});

// API Controllers
builder.Services.AddControllers();

// Cookie 認証（管理者用）
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// Rate Limiting（公開API対策）
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("public-api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,              // 30回
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// Anti-forgery（Razor Pages用）
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

var app = builder.Build();

//
// --------------------
// Middleware
// --------------------
//

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// HTTPS（本番では推奨）
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// 認証・認可
app.UseAuthentication();
app.UseAuthorization();

// Rate limit
app.UseRateLimiter();

//
// --------------------
// Endpoints
// --------------------
//

// Razor Pages
app.MapRazorPages();

// API Controllers
app.MapControllers()
   .RequireRateLimiting("public-api"); // 公開APIは一律制限

app.Run();



// var builder = WebApplication.CreateBuilder(args);

// // Add services to the container.
// builder.Services.AddRazorPages();

// var app = builder.Build();

// // Configure the HTTP request pipeline.
// if (!app.Environment.IsDevelopment())
// {
//     app.UseExceptionHandler("/Error");
//     // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//     app.UseHsts();
// }

// app.UseHttpsRedirection();
// app.UseStaticFiles();

// app.UseRouting();

// app.UseAuthorization();

// app.MapRazorPages();

// app.Run();

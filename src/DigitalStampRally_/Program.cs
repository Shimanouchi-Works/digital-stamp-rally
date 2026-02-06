using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

//
// --------------------
// Services
// --------------------
//

// Razor Pages
builder.Services.AddRazorPages(options =>
{
    // 管理画面はログイン必須、など後でまとめて指定できる
    // options.Conventions.AuthorizeFolder("/Admin");
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
// var app = builder.Build();

// app.MapGet("/", () => "Hello World!");

// app.Run();

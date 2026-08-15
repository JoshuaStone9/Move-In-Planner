using Microsoft.EntityFrameworkCore;
using MoveInPlanner.Data;
using MoveInPlanner.Services.ProductMetadata;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IRetailerProductMetadataProvider, AmazonProductMetadataProvider>();
builder.Services.AddSingleton<IRetailerProductMetadataProvider, TikTokProductMetadataProvider>();
builder.Services.AddSingleton<ProductImageRequestPolicy>();
builder.Services.AddHttpClient<IProductMetadataService, ProductMetadataService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false,
    AutomaticDecompression = System.Net.DecompressionMethods.All
});
builder.Services.AddHttpClient("ProductImageProxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false,
    AutomaticDecompression = System.Net.DecompressionMethods.All
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

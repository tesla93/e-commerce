using ECommerce.Server;
using Microsoft.EntityFrameworkCore;
using ECommerce.Data.SQL.Context;
using Microsoft.Extensions.Configuration;
using ECommerce.Services.Payments;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddNewtonsoftJson(o =>
                o.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);

builder.Services.AddDbContext<DataBaseContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IPaymentsGateway>(x =>
{
    var logger = x.GetRequiredService<ILogger<StripePaymentsGatewayService>>();
    string stripeSecretKey = builder.Configuration.GetSection("Stripe").GetValue<string>("secretKey");
    string stripePublicKey = builder.Configuration.GetSection("Stripe").GetValue<string>("publicKey");

    if (string.IsNullOrEmpty(stripeSecretKey) || string.IsNullOrEmpty(stripePublicKey))
        logger.LogError("Stripe keys are missing.");
    return new StripePaymentsGatewayService(logger, stripeSecretKey);
});

builder.Services.AddCors(o =>
{
    o.AddPolicy("PolicyCorsAllowAll", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.ConfigureExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("PolicyCorsAllowAll");

app.UseRouting();

app.UseAuthorization();

app.UseHttpsRedirection();

app.MapControllers();

using (var serviceScope = app.Services.CreateScope())
{
    var context = serviceScope.ServiceProvider.GetRequiredService<DataBaseContext>();
    if (context.Database.GetPendingMigrations().Any())
    {
        context.Database.Migrate();
    }
}

app.Run();
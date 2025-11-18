using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.Dashboard;
using Saf_alu_ci_Api.Controllers.Devis;
using Saf_alu_ci_Api.Controllers.Dqe;
using Saf_alu_ci_Api.Controllers.Factures;
using Saf_alu_ci_Api.Controllers.ObjectifFinancier;
using Saf_alu_ci_Api.Controllers.Projets;
using Saf_alu_ci_Api.Controllers.SousTraitants;
using Saf_alu_ci_Api.Controllers.Tresorerie;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using Saf_alu_ci_Api.Services.Jw;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configuration des services
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Enregistrement des services métier
builder.Services.AddScoped(provider => new UtilisateurService(connectionString));
builder.Services.AddScoped(provider => new ClientService(connectionString));
builder.Services.AddScoped(provider => new DevisService(connectionString));
builder.Services.AddScoped(provider => new SousTraitantService(connectionString));
builder.Services.AddScoped(provider => new DashboardService(connectionString));
builder.Services.AddScoped(provider => new FactureService(connectionString));
builder.Services.AddScoped(provider => new TresorerieService(connectionString));
builder.Services.AddScoped(provider => new ProjetService(connectionString));
builder.Services.AddScoped(provider => new ObjectifFinacierService(connectionString));
builder.Services.AddScoped(provider => new DQEService(connectionString));
builder.Services.AddScoped<ConversionService>(sp =>
{
    var dqeService = sp.GetRequiredService<DQEService>();
    var projetService = sp.GetRequiredService<ProjetService>();
    return new ConversionService(connectionString, dqeService, projetService);
});


// Services utilitaires
builder.Services.AddScoped<JwtService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SafAlu API", Version = "v1" });

    // 🔑 Définition de la sécurité JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Entrez: Bearer {votre_token_jwt}"
    });

    // 🔒 Obligation d'utiliser le token pour accéder aux endpoints protégés
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Configuration CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

// Configuration JWT - CORRIGÉE pour correspondre à appsettings.json
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],           // ✅ Correct
            ValidAudience = builder.Configuration["Jwt:Audience"],       // ✅ Correct  
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])) // ✅ "Key" pas "SecretKey"
        };
    });

var app = builder.Build();

// Configuration du pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
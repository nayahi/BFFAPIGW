using BFFAPIGW.Services;
using ECommerceGRPC.OrderService;
using ECommerceGRPC.ProductService;
using ECommerceGRPC.UserService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using Grpc.Net.ClientFactory;

using System.Text;
using BFFAPIGW.Services;
using ECommerceGRPC.OrderService;
using ECommerceGRPC.ProductService;
using ECommerceGRPC.UserService;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// Configuración de Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Iniciando API Gateway BFF");

    var builder = WebApplication.CreateBuilder(args);

    // Configurar Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // Agregar servicios al contenedor
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Configurar Swagger con soporte para JWT
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "E-Commerce API Gateway",
            Version = "v1",
            Description = "API Gateway (BFF) para arquitectura de microservicios gRPC - Semana 2",
            Contact = new OpenApiContact
            {
                Name = "Curso Microservicios Nivel 2",
                Email = "soporte@curso.com"
            }
        });

        // Configurar autenticación JWT en Swagger
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Ingrese el token JWT en el formato: Bearer {token}"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                Array.Empty<string>()
            }
        });
    });

    // Configurar autenticación JWT
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSettings["SecretKey"]
        ?? throw new InvalidOperationException("JWT SecretKey no configurado");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("Autenticación JWT fallida: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Information("Token JWT validado exitosamente para usuario: {User}",
                    context.Principal?.Identity?.Name ?? "Unknown");
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();

    // Configurar CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Registrar servicios de la aplicación
    builder.Services.AddScoped<IJwtService, JwtService>();

    // Configurar clientes gRPC
    var productServiceUrl = builder.Configuration["GrpcServices:ProductService"]
        ?? "http://localhost:7001";
    var userServiceUrl = builder.Configuration["GrpcServices:UserService"]
        ?? "http://localhost:7002";
    var orderServiceUrl = builder.Configuration["GrpcServices:OrderService"]
        ?? "http://localhost:7003";

    Log.Information("Configurando clientes gRPC:");
    Log.Information("  - ProductService: {Url}", productServiceUrl);
    Log.Information("  - UserService: {Url}", userServiceUrl);
    Log.Information("  - OrderService: {Url}", orderServiceUrl);

    // Cliente gRPC para ProductService
    builder.Services.AddGrpcClient<ProductService.ProductServiceClient>(options =>
    {
        options.Address = new Uri(productServiceUrl);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    });

    // Cliente gRPC para UserService
    builder.Services.AddGrpcClient<UserService.UserServiceClient>(options =>
    {
        options.Address = new Uri(userServiceUrl);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    });

    // Cliente gRPC para OrderService
    builder.Services.AddGrpcClient<OrderService.OrderServiceClient>(options =>
    {
        options.Address = new Uri(orderServiceUrl);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    });

    // Construir la aplicación
    var app = builder.Build();

    // Configurar el pipeline HTTP
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "E-Commerce API Gateway v1");
            options.RoutePrefix = string.Empty; // Swagger en la raíz
        });
    }

    app.UseSerilogRequestLogging();

    app.UseCors("AllowAll");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Endpoint de health check
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        service = "API Gateway BFF",
        version = "1.0.0"
    }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .AllowAnonymous();

    Log.Information("API Gateway BFF configurado exitosamente");
    Log.Information("Escuchando en puerto: {Port}",
        builder.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://localhost:5000");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar");
}
finally
{
    Log.CloseAndFlush();
}

//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.
//builder.Services.AddControllersWithViews();

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    app.UseHsts();
//}

//app.UseHttpsRedirection();
//app.UseStaticFiles();

//app.UseRouting();

//app.UseAuthorization();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

//app.Run();

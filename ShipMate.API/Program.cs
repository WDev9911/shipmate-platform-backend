using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShipMate.API.Filters;
using ShipMate.API.Hubs;
using ShipMate.API.Middlewares;
using ShipMate.Application;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    // A body that can't be read (wrong type, unknown enum, broken JSON) is rejected before ValidationFilter runs.
    // Answer it in the same shape as a FluentValidation failure, without the serializer's internal details.
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
    {
        // When the body is missing or can't be read, ASP.NET also adds "The request field is required." for the
        // body parameter itself; that line is misleading next to the real body errors, so drop it.
        var hasBodyErrors = context.ModelState.Keys.Any(IsBodyKey);
        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Status = StatusCodes.Status400BadRequest,
            Title = GlobalExceptionHandler.ValidationTitle,
            Detail = GlobalExceptionHandler.ValidationDetail,
            Instance = context.HttpContext.Request.Path
        };
        problemDetails.Extensions["errorCode"] = GlobalExceptionHandler.ValidationFailedCode;
        problemDetails.Extensions["errors"] = context.ModelState
            .Where(entry => entry.Value!.Errors.Count > 0 && (!hasBodyErrors || IsBodyKey(entry.Key)))
            .GroupBy(entry => ToFieldName(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(entry => entry.Key.StartsWith('$')
                        ? ["The value is not valid."]
                        : entry.Value!.Errors.Select(e => e.ErrorMessage))
                    .Distinct()
                    .ToArray());

        return new BadRequestObjectResult(problemDetails) { ContentTypes = { "application/problem+json" } };
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token here (no need to type 'Bearer ' — Swagger adds it automatically)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

const string WorkspaceHubPath = "/hubs/workspace";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim types as issued (e.g. "sub") instead of ASP.NET Core's legacy remapping
        // to long XML-namespace URIs — ICurrentUserService reads JwtRegisteredClaimNames.Sub directly.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };

        // A browser can't put an Authorization header on a WebSocket, so SignalR sends the token in the query string.
        // Only the hub reads it from there; everything else still needs the header.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(WorkspaceHubPath))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(builder.Configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // required so the browser sends/receives the httpOnly refresh token cookie
    });
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<IWorkspaceNotifier, SignalRWorkspaceNotifier>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<WorkspaceHub>(WorkspaceHubPath);

app.Run();

// "" is a missing body; "$..." is a JSON path inside a body that could not be read.
static bool IsBodyKey(string modelStateKey) => modelStateKey.Length == 0 || modelStateKey.StartsWith('$');

// "$.items[0].complexity" -> "Items[0].Complexity", the same key FluentValidation uses for that field.
static string ToFieldName(string modelStateKey)
{
    if (modelStateKey is "" or "$")
    {
        return "Body";
    }

    var path = modelStateKey.StartsWith("$.") ? modelStateKey[2..] : modelStateKey;
    return string.Join('.', path.Split('.').Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
}

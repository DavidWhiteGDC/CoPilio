
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    var kernel = kernelBuilder
        .AddAzureOpenAIChatCompletion("gpt-4o", "https://gdc-ai-dev.openai.azure.com/", "2c96e37338d34051964ae11cbfdc554c")
        .Build();




    return kernel;
});
builder.Services.AddLogging(ConfigureAwaitOptions => ConfigureAwaitOptions.AddConsole());
builder.Services.AddLogging(ConfigureAwaitOptions => ConfigureAwaitOptions.SetMinimumLevel(LogLevel.Trace));
builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var kernel = sp.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<IChatCompletionService>();
});

builder.Services.AddControllers();
builder.Services.AddDbContext<ConversationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using Microsoft.EntityFrameworkCore;
using GDC.Copilio.Schema;
using GDC.Copilio.Business;
using GDC.Copilio.Business.Abstractions;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddScoped<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    var kernel = kernelBuilder
        .AddAzureOpenAIChatCompletion("gpt-4o", "https://gdc-ai-dev.openai.azure.com/", "2c96e37338d34051964ae11cbfdc554c")
        .Build();




    return kernel;
});

builder.Services.AddScoped<IChatCompletionService>(sp =>
{
    var kernel = sp.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<IChatCompletionService>();
});

builder.Services.AddControllers();
builder.Services.AddDbContext<ConversationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,                           // retry up to 5 times
            maxRetryDelay: TimeSpan.FromSeconds(30),    // wait up to 30 s between retries
            errorNumbersToAdd: null                     // or a list of additional SQL error codes
        )
    )
);

builder.Services.AddScoped<IConversationService, ConversationService>();
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

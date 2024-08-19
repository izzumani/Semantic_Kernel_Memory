using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Connectors.OpenAI;
#pragma warning disable SKEXP0003, SKEXP0011, SKEXP0052, SKEXP0001, SKEXP0050 , SKEXP0010
public class Program
{
    private static Kernel _kernel;
    private static SecretClient keyVaultClient;

    public async static Task Main(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
                     .AddUserSecrets<Program>()
                     .Build();

        string? appTenant = config["appTenant"];
        string? appId = config["appId"] ?? null;
        string? appPassword = config["appPassword"] ?? null;
        string? keyVaultName = config["KeyVault"] ?? null;

        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        ClientSecretCredential credential = new ClientSecretCredential(appTenant, appId, appPassword);
        keyVaultClient = new SecretClient(keyVaultUri, credential);
        string? apiKey = keyVaultClient.GetSecret("OpenAIapiKey").Value.Value;
        string? orgId = keyVaultClient.GetSecret("OpenAIorgId").Value.Value;

        var _builder = Kernel.CreateBuilder()
           .AddOpenAIChatCompletion("gpt-3.5-turbo", apiKey, orgId, serviceId: "gpt35")
            .AddOpenAIChatCompletion("gpt-4", apiKey, orgId, serviceId: "gpt4");
        
        _kernel = _builder.Build();

        var memoryBuilder = new MemoryBuilder();
        memoryBuilder.WithMemoryStore(new VolatileMemoryStore());
        memoryBuilder.WithOpenAITextEmbeddingGeneration("text-embedding-3-small", apiKey);
        var memory = memoryBuilder.Build();

        const string MemoryCollectionName = "default";
        await memory.SaveInformationAsync(MemoryCollectionName, id: "1", text: "My favorite city is Paris");
        await memory.SaveInformationAsync(MemoryCollectionName, id: "2", text: "My favorite activity is visiting museums");

        _kernel.ImportPluginFromObject(new TextMemoryPlugin(memory));
        const string prompt = @"
                                Information about me, from previous conversations:
                                    - {{$city}} {{recall $city}}
                                        - {{$activity}} {{recall $activity}}
                               Generate a personalized tour of activities for me to do when I have a free day in my favorite city. I just want to do my favorite activity.
";
        var f = _kernel.CreateFunctionFromPrompt(prompt, new OpenAIPromptExecutionSettings { MaxTokens = 2000, Temperature = 0.8 });
        var context = new KernelArguments();
        context["city"] = "What is my favorite city?";
        context["activity"] = "What is my favorite activity?";
        context[TextMemoryPlugin.CollectionParam] = MemoryCollectionName;
        var result = await f.InvokeAsync(_kernel, context);
        Console.WriteLine(result);
        Console.ReadLine();

    }

}
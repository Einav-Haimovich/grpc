using Basics;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using static Grpc.Core.Metadata;

// --- JWT token generation (same secret as server) ---
const string jwtKey = "super-secret-key-that-is-long-enough-for-hmac256";
const string jwtIssuer = "grpc-server";
const string jwtAudience = "grpc-client";

string GenerateToken()
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims: new[] { new Claim(ClaimTypes.Name, "console-client") },
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: creds);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

// --- gRPC channel with CallCredentials (attaches Bearer token to every call) ---
var callCredentials = CallCredentials.FromInterceptor((context, metadata) =>
{
    metadata.Add("Authorization", $"Bearer {GenerateToken()}");
    return Task.CompletedTask;
});

var hedgingPolicy = new MethodConfig
{
    Names = { MethodName.Default },
    HedgingPolicy = new HedgingPolicy
    {
        MaxAttempts = 5,
        NonFatalStatusCodes = { StatusCode.Internal },
        HedgingDelay = TimeSpan.FromSeconds(1)
    }
};

// CallCredentials require either TLS or UnsafeUseInsecureChannelCallCredentials for plain HTTP.
using var channel = GrpcChannel.ForAddress("http://localhost:5210", new GrpcChannelOptions
{
    UnsafeUseInsecureChannelCallCredentials = true,
    Credentials = ChannelCredentials.Create(ChannelCredentials.Insecure, callCredentials),
    ServiceConfig = new ServiceConfig { MethodConfigs = { hedgingPolicy } }
});

var client = new FirstServiceDefinition.FirstServiceDefinitionClient(channel);
//Unary(client);
//await ClientStreamingAsync(client);
//await ServerStreamingAsync(client);
await BiDirectionalStreamingAsync(client);

Console.ReadLine();

void Unary(FirstServiceDefinition.FirstServiceDefinitionClient client)
{
    var req = new Request
    {
        Content = "John Doe"
    };

    var result = client.Unary(req, deadline: DateTime.UtcNow.AddMilliseconds(3));
}

async Task ClientStreamingAsync(FirstServiceDefinition.FirstServiceDefinitionClient client)
{
    using var call = client.ClientStreaming();
    for (int i = 0; i < 5; i++)
    {
        var req = new Request
        {
            Content = $"Message {i}"
        };
        await call.RequestStream.WriteAsync(req);
    }
    await call.RequestStream.CompleteAsync();
    var result = await call.ResponseAsync;
    Console.WriteLine(result.Message);
}

async Task ServerStreamingAsync(FirstServiceDefinition.FirstServiceDefinitionClient client)
{
    try
    {
        var cts = new CancellationTokenSource();
        var metaData = new Metadata();
        metaData.Add(new Entry("Einav", "Haimovich"));

        var req = new Request
        {
            Content = "Einav Haimovich"
        };
        using var call = client.ServerStreaming(req, headers: metaData);
        await foreach (var res in call.ResponseStream.ReadAllAsync(cts.Token))
        {
            Console.WriteLine(res.Message);
            if (res.Message.Contains("2"))
            {
                cts.Cancel();
            }
        }

        var serverTrailers = call.GetTrailers();
        var trailerValue = serverTrailers.Get("Trailer");
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
    {

    }
}

async Task BiDirectionalStreamingAsync(FirstServiceDefinition.FirstServiceDefinitionClient client)
{
    using var call = client.BidirectionalStreaming();
    for (int i = 0; i < 5; i++)
    {
        var req = new Request
        {
            Content = $"Message {i}"
        };
        await call.RequestStream.WriteAsync(req);
    }

    while (await call.ResponseStream.MoveNext())
    {
        var message = call.ResponseStream.Current;
        Console.WriteLine(message);
    }

    await call.RequestStream.CompleteAsync();
}

using Basics;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace gRPC_In_Dotnet.Services
{
    [Authorize]
    public class FirstService : FirstServiceDefinition.FirstServiceDefinitionBase
    {
        private readonly ILogger<FirstService> _logger;

        public FirstService(ILogger<FirstService> logger)
        {
            _logger = logger;
        }

        public override Task<Response> Unary(Request request, ServerCallContext context)
        {
            if (!context.RequestHeaders.Where(h => h.Key == "not-existing-ke").Any())
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Missing required header: not-existing-ke"));
            }

            _logger.LogInformation($"Received unary request: {request.Content}");

            var response = new Response
            {
                Message = $"Hello {request.Content} from server"
            };

            return Task.FromResult(response);
        }

        public override async Task<Response> ClientStreaming(IAsyncStreamReader<Request> requestStream, ServerCallContext context)
        {
            var response = new Response
            {
                Message = "Server got client stream"
            };

            while (await requestStream.MoveNext())
            {
                var request = requestStream.Current;
                _logger.LogInformation($"Received message: {request.Content}");
                response.Message += $"\nReceived message: {request.Content}";
            }

            return response;
        }

        public override async Task ServerStreaming(Request request, IServerStreamWriter<Response> responseStream, ServerCallContext context)
        {
            var headers = context.RequestHeaders.Get("Einav");
            var trailer = new Metadata.Entry("Trailer", "Trailer value");
            context.ResponseTrailers.Add(trailer);

            for (int i = 0; i < 100; i++)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Client cancelled the request.");
                    return;
                }
                var response = new Response
                {
                    Message = $"Hello {request.Content} from server stream {i + 1}"
                };
                await responseStream.WriteAsync(response);
            }
        }

        public override async Task BidirectionalStreaming(IAsyncStreamReader<Request> requestStream, IServerStreamWriter<Response> responseStream, ServerCallContext context)
        {
            var response = new Response
            {
                Message = "Server got bidirectional stream"
            };
            while (await requestStream.MoveNext())
            {
                var request = requestStream.Current;
                _logger.LogInformation($"Received message: {request.Content}");
                response.Message += $"\nReceived message: {request.Content}";
                await responseStream.WriteAsync(response);
            }
        }
    }
}

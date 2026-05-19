using System;
using System.Collections.Generic;
using System.Text;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace GRPCClient.Interceptors
{
    public class ClientLoggerInterceptor : Interceptor
    {
        private readonly ILogger<ClientLoggerInterceptor> _logger;

        public ClientLoggerInterceptor(ILogger<ClientLoggerInterceptor> logger)
        {
            _logger = logger;
        }


        public override TResponse BlockingUnaryCall<TRequest, TResponse>(
            TRequest request, 
            ClientInterceptorContext<TRequest, TResponse> context, 
            BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            try
            {
                _logger.LogInformation($"Starting call of type: {context.Method.FullName}, {context.Method.Type}");
                return continuation( request, context );
            }
            catch (Exception ex)
            {

                throw;
            }
        }
    }
}

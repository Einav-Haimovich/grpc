# gRPC

gRPC is a high-performance RPC framework built on HTTP/2 and Protocol Buffers. Unlike REST, where the communication style is always request/response, gRPC defines four distinct communication patterns and generates type-safe client/server code from a shared `.proto` contract. This repo explores those patterns — from defining a service contract to running a client inside a full web application.

---

## [server](gRPC/server/)

Implements two proto services: a simple Greeter and a FirstService that exposes all four streaming patterns. The server reads `.proto` definitions at build time and generates the base classes that service implementations inherit from.

_Learned: the `.proto` file is the single source of truth for the entire API — both the server stubs and client proxies are generated from the same contract, so the interface between them can never drift out of sync._

---

## [streaming](gRPC/streaming/)

Client that exercises all four gRPC communication patterns against FirstService: a single request/response, a stream of requests with one response, one request with a stream of responses, and full bidirectional streaming. Also demonstrates the hedging policy (automatic retry across multiple attempts in parallel) and interceptors for cross-cutting concerns.

_Learned: the four patterns are not just API styles — they map to fundamentally different channel behaviors. Bidirectional streaming keeps a single long-lived connection open for both directions simultaneously, which changes how cancellation, backpressure, and error propagation work compared to unary calls._

---

## [client-integration](gRPC/client-integration/)

Shows how to wire a gRPC client into a larger application using the client factory pattern, which manages channel lifetime and reuse automatically. The typed client is registered once and injected wherever it's needed.

_Learned: gRPC channels are expensive to create and meant to be long-lived — the client factory pattern solves this by pooling and reusing channels behind the scenes, the same way HTTP connection pooling works for HTTP clients._

---

## Proto definitions

Both clients share the proto files defined in `gRPC/server/Protos/` — they reference them directly at build time with no duplication.

`greet.proto` — Greeter service with a single unary RPC: send a name, get a greeting back.

`first.proto` — FirstServiceDefinition with all four patterns:
- `Unary` — single request, single response
- `ClientStreaming` — stream of requests, single response
- `ServerStreaming` — single request, stream of responses
- `BidirectionalStreaming` — stream of requests, stream of responses

---

## How to Run

```bash
# Start the server (port 5210)
cd gRPC/server
dotnet run

# In a second terminal: run the streaming client
cd gRPC/streaming
dotnet run

# Or run the web client (port 5121)
cd gRPC/client-integration
dotnet run
```

To open the full solution in Visual Studio or Rider, open `gRPC/gRPC.slnx`.

---

## Folder Structure

```
gRPC/
├── gRPC.slnx                    # Solution file
├── server/                      # gRPC server: service definitions + proto contracts
│   └── Protos/
│       ├── greet.proto
│       └── first.proto
├── streaming/                   # Client: all four streaming patterns, hedging, interceptors
└── client-integration/          # Client: gRPC client factory wired into a web application
```

---

Thanks to Irina Scurtu for the course — [From Zero to Hero: gRPC in .NET](https://dometrain.com/course/from-zero-to-hero-grpc-in-dotnet/).

[Certificate of completion](<certificate/gRPC In Dotnet - Einav Haimovich.pdf>)

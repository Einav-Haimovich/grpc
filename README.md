# gRPC in .NET

A hands-on implementation of gRPC in C#/.NET, covering all four communication patterns (unary, client streaming, server streaming, and bidirectional streaming), Protocol Buffers, advanced features such as interceptors and authentication, transient fault handling with retry and hedging policies, and integration testing. The project is structured as a server, a console streaming client, and an ASP.NET Core MVC web client — all driven by a shared `.proto` contract.

---

### Introduction

This section establishes the conceptual foundation for gRPC in .NET — what gRPC is, where it came from, and when it is the right choice. Rather than writing any code, the author traces gRPC's origins from Google's internal Stubby framework (2001) through its open-sourcing and eventual first-class support in .NET (2019), then situates it within the broader API ecosystem by comparing it to REST and WCF. The goal is to give developers a clear mental model for how gRPC works and the architectural trade-offs to consider before adopting it.

**What I learned:**
- gRPC uses binary serialization over HTTP/2, which produces smaller payloads and more efficient connections than REST with JSON, and it requires HTTP/2 — services still on HTTP/1 cannot use it.
- Protocol Buffers serve as the single shared contract between a client and a server: both sides derive their types and operation signatures from the same language-agnostic `.proto` file, enabling communication across polyglot systems without any shared runtime.
- gRPC is contract-first by design — a `.proto` file is mandatory to consume a gRPC service, unlike REST where a contract such as OpenAPI is optional, and gRPC generates strongly-typed base classes and client stubs out of the box.
- gRPC supports four communication patterns — unary, client streaming, server streaming, and bidirectional streaming — all over a single TCP connection, a capability that REST's request/response model cannot replicate natively.
- gRPC is best suited for service-to-service communication, particularly in microservice and cloud-native architectures, polyglot environments, and scenarios with bandwidth constraints; it is not a universal replacement for REST but a complementary tool with distinct trade-offs.
- Because gRPC only uses POST methods under the hood, HTTP-level caching is not available and must be handled separately at the server level.
- gRPC can serve as a migration path away from WCF: it lifts the Windows-only hosting constraint, supports cross-platform deployment, and offers a code-first approach that allows existing WCF operations and types to be reused during the transition.

---

### Protocol Buffers

Protocol Buffers (protobuf) is Google's language- and platform-neutral data serialization format, and it serves as the contract layer on which gRPC is built. This section explains how a `.proto` file defines both the message types a service accepts and returns and the RPC operations the service exposes, and how the proto compiler turns that definition into ready-to-use C# classes. The section works through the full type system: scalar types, enums, well-known types (including `Empty`, `Timestamp`, and wrapper types for nullability), the `any` type for fields whose concrete type is determined at runtime, and the `oneof` construct. Collection support via the `repeated` keyword and message nesting round out the picture.

**What I learned:**
- A `.proto` file is the explicit contract between client and server. Because it compiles to typed code in any supported language, both sides share the same field names, field numbers, and operation signatures by construction, eliminating an entire class of integration bugs.
- Proto3 uses field numbers, not field names, for binary encoding. Adding new fields to an existing message is safe without breaking existing clients or servers — older code simply ignores unknown field numbers, enabling backward and forward compatibility in systems where components are deployed independently.
- Scalar types in protobuf do not carry null — every field has a language-default value (e.g., `0` for integers, `""` for strings). To make a field genuinely nullable in the generated C# code, you must use the well-known wrapper type (e.g., `google.protobuf.Int32Value` instead of `int32`).
- gRPC requires every RPC to have both a request and a response message. When a method logically needs no input or output, the idiomatic solution is `google.protobuf.Empty` rather than inventing a throwaway empty message.
- The `google.protobuf.Any` type lets a single field hold any protobuf message, but the receiver must use reflection and call `Unpack<T>()` explicitly, making `any` fields more complex to consume than strongly typed alternatives.
- The `repeated` keyword produces a collection field (mapped to `RepeatedField<T>` in C#), and message types can nest other message types, enabling the same kind of compositional modeling used in object-oriented domain design.

---

### Getting Started

This section walks through creating a gRPC server in .NET from scratch using the built-in Visual Studio template. It explains the anatomy of a gRPC project — the `.proto` file as the contract, the auto-generated base classes that the compiler produces from that contract, and the concrete service class that implements it. The section also covers how the ASP.NET Core pipeline is configured to host gRPC services, including the requirement for HTTP/2 and service registration via middleware.

**What I learned:**
- The `.proto` file is the source of truth for a gRPC service: it defines the service name, method signatures, and message types, and everything else — including the C# base classes — is generated from it at build time. You never manually write the wire contract.
- gRPC service methods in .NET are implemented by inheriting from a compiler-generated base class and overriding its methods. The shape of those overrides (parameter types, return types) is dictated entirely by the proto definition.
- Every gRPC method must have an explicit request and response message type — there is no equivalent of a void parameter or return value in the protocol.
- The `ServerCallContext` parameter is injected into every service method automatically and carries call-level metadata including headers, trailers, and the underlying HTTP context.
- gRPC service implementations participate fully in ASP.NET Core's dependency injection system, so loggers, repositories, and other services can be constructor-injected exactly as in any other hosted service.
- gRPC requires HTTP/2 at the transport level, and the Kestrel endpoint must be explicitly configured for it; the protocol does not fall back to HTTP/1.1.

---

### gRPC Types

This section introduces the four communication patterns that gRPC supports, explaining how each one differs in terms of who sends data, how much, and when. Starting from the familiar request-response model of REST, the section builds up to more complex streaming arrangements where either the client, the server, or both sides push multiple messages over a single persistent TCP connection. Each pattern is defined in a `.proto` file using the `stream` keyword, compiled into a C# base class, and then implemented by overriding the generated methods in a service class.

**What I learned:**
- gRPC's four method types — unary, client streaming, server streaming, and bidirectional streaming — map to four combinations of single vs. multiple messages on each side of a connection, giving you the right tool for very different communication needs.
- All four patterns share one constraint: it is always the client that opens the TCP connection to the server. Unlike technologies such as SignalR, the server cannot initiate contact; it can only respond or push once a channel is established.
- Streaming patterns in gRPC reuse a single TCP connection for all messages in a call, avoiding the overhead of opening a new connection per message.
- The `.proto` file is the single source of truth: declaring `stream` on the input or output of an RPC method is sufficient to change the communication pattern, and the compiler generates the corresponding C# signatures (`IAsyncStreamReader<T>` for incoming streams, `IServerStreamWriter<T>` for outgoing streams).
- Server streaming is well-suited for real-time feeds or progress reporting, where one client request triggers a continuous flow of responses — the implementation simply loops and writes each chunk to the response stream, with cancellation token checks to handle early client disconnect.
- Bidirectional streaming requires the server to both read from the incoming request stream and write to the outgoing response stream within the same method, enabling a full-duplex conversation over a single call.

---

### Consuming a gRPC Service

This section covers how to build a gRPC client in .NET, walking through everything from project setup to calling all four communication patterns. It also addresses production concerns including call deadlines, cancellation tokens, and integrating a gRPC client into an ASP.NET MVC application using dependency injection rather than a standalone console app.

**What I learned:**
- A gRPC channel sits beneath the HTTP/2 connection and multiplexes multiple calls over a single TCP connection; creating one channel and reusing it across clients is intentional, not an optimization afterthought.
- The proto file is not copied into the client project — it is linked via a virtual reference in the `.csproj` with `GrpcServices="Client"`, which controls what code the Protobuf toolchain generates (client stubs only, not server scaffolding).
- Call deadlines should be set on every gRPC call by default; omitting them leaves the server without a signal to stop processing when the caller no longer cares, which can exhaust server resources under load.
- Cancellation tokens give the client fine-grained control to abort a streaming call mid-flight based on business logic, and the server must explicitly check `context.CancellationToken.IsCancellationRequested` to honour that signal.
- In ASP.NET applications, gRPC clients are registered via `AddGrpcClient<T>()` in the DI container and injected as constructor dependencies, which centralises channel configuration and avoids manually managing channel lifetimes in application code.

---

### gRPC Internals

This section examines what happens beneath the gRPC API surface: how gRPC requests and responses are structured within HTTP, how auxiliary data travels alongside messages, how errors are represented, and how exceptions are handled on the client side. Rather than treating gRPC as a black box, the section builds a mental model of the layered anatomy of a gRPC call — from metadata sent before the message body, to trailers delivered only after all response messages are complete, to a dedicated set of status codes that operate independently of HTTP status codes.

**What I learned:**
- A single HTTP request or response can carry multiple independent gRPC messages, which is what makes streaming patterns possible. Metadata (key-value pairs) is transmitted before the message body, while trailers are key-value pairs sent after all response messages are flushed — making them the gRPC equivalent of response headers for end-of-stream information.
- gRPC metadata and trailers use the same underlying `Metadata.Entry` type but serve opposite roles: metadata travels with the request (client to server) and is readable via `ServerCallContext.RequestHeaders`, while trailers travel with the response and are readable via `GetTrailers()` only after the stream completes.
- gRPC defines its own set of 16 status codes entirely separate from HTTP status codes; a gRPC call cannot directly surface an HTTP 409 — it must map its domain conditions onto gRPC status codes such as `NotFound`, `PermissionDenied`, `Unauthenticated`, or `Internal`.
- Exception handling in gRPC is unified around a single exception type, `RpcException`, which carries a `StatusCode` property; callers filter on `StatusCode` values to branch their error-handling logic, keeping the exception model consistent across all gRPC communication patterns.

---

### Advanced Features

This section covers the mechanisms gRPC provides for augmenting and controlling RPC behavior beyond the core request-response cycle. Topics include interceptors on both the client and server side, the distinction between interceptors and ASP.NET Core middleware, response compression, deadlines, metadata, JWT-based authentication, and client-side load balancing. Together these features address the concerns that arise when moving from a working prototype to a production-grade service: observability, security, performance tuning, and resilience.

**What I learned:**
- Interceptors are the gRPC-native equivalent of middleware: they sit inside the gRPC pipeline and can be attached at either the client or server, giving you a single place to enforce cross-cutting logic such as logging, tracing, or authentication without touching individual service methods.
- Interceptor execution order is reversed from registration order — the last interceptor registered executes first, so the order in which you attach interceptors directly affects behavior.
- Interceptors and ASP.NET Core middleware solve similar problems but at different layers: interceptors act within the gRPC protocol layer and have access to gRPC-specific context, while middleware acts at the HTTP level and can manipulate raw HTTP headers before a request reaches any gRPC handler.
- Response compression is opt-in on both sides: the server must declare a compression algorithm and the client must advertise what it accepts via the `grpc-accept-encoding` metadata header; compression can be disabled per method using `WriteOptions` with the `NoCompress` flag.
- Authentication is implemented by attaching `CallCredentials` to the channel, which injects a Bearer token into every outgoing call's metadata automatically — the server then validates it through standard ASP.NET Core JWT middleware before the request reaches the service.
- Client-side load balancing is built into the gRPC channel via configurable policies: `RoundRobin` distributes calls across known server addresses in rotation, while `PickFirst` maintains connection affinity to the first available instance.

---

### Transient Fault Handling

This section covers how gRPC in .NET handles temporary failures — such as brief network glitches or server overloads — without requiring third-party libraries. The gRPC .NET client provides built-in support for two distinct fault-tolerance strategies: retry policies and hedging policies. Both are configured declaratively through a `MethodConfig` and `ServiceConfig` on the channel, making the configuration reusable across every client created from that channel.

**What I learned:**
- Retry policies instruct the client library to silently re-send a failed request up to a configured maximum number of attempts, making the failure transparent to application code — the caller receives either a successful response or a final error, never the intermediate failures.
- Hedging fires multiple copies of the same request in parallel after a configurable delay between each; the first successful response wins and all remaining in-flight requests receive a cancellation — trading extra bandwidth and server load for reduced tail latency.
- Retry and hedging policies are mutually exclusive on a single `MethodConfig` — you must choose one strategy per method.
- Status code filtering is a critical part of both policies — only errors matching the explicitly declared retryable status codes trigger a retry or hedge, preventing unintended retries on non-transient errors such as bad input.
- Backoff parameters (initial delay, maximum delay, and multiplier) on retry policies control how aggressively the client backs off between attempts, which matters for avoiding thundering-herd pressure on an already-overloaded server.
- The gRPC .NET client automatically injects a `grpc-previous-rpc-attempts` header on retry attempts, incrementing its value with each attempt; server code can read this header to detect and distinguish retried calls from original ones.

---

### Testing a gRPC Service

This section covers how to write both unit tests and integration tests for a gRPC service in .NET. Unit tests call service methods directly by constructing a fake `ServerCallContext`, bypassing the network entirely. Integration tests spin up the full application in memory using `WebApplicationFactory` and connect to it through a real gRPC channel backed by an in-process HTTP client. The section walks through the setup ceremony required for each approach — from extracting interfaces and creating test helpers, to configuring a test host that allows dependency substitution — and demonstrates writing a passing test for a unary RPC call using the Arrange-Act-Assert pattern with xUnit and FluentAssertions.

**What I learned:**
- Unit testing a gRPC service requires creating a concrete stub of `ServerCallContext`, because every service method signature includes it as a required parameter that cannot be omitted or easily faked without a custom implementation.
- Extracting an interface from the service class before writing unit tests enables proper isolation and makes the subject under test substitutable.
- Integration tests use `WebApplicationFactory<TProgram>` with a test server to boot the full application in memory; the factory's `ConfigureWebHost` override is the correct place to swap connection strings or inject test-specific dependencies.
- A gRPC integration test client is constructed by passing a `GrpcChannel` backed by the `HttpClient` from the factory rather than an external network address, keeping the test self-contained without a running server.
- Making the `Program` class a `partial class` is required to expose it as a generic type argument to `WebApplicationFactory` in integration test projects.
- The same Arrange-Act-Assert pattern applies to both unit and integration tests; the only structural difference is how the client or service instance is obtained.

---

## How to Run

Start the server first, then run either client.

```bash
# 1. gRPC server (port 5210)
cd code/server
dotnet run

# 2. Console streaming client
cd code/streaming
dotnet run

# 3. ASP.NET MVC web client (http://localhost:5121)
cd code/client-integration
dotnet run
```

To open the full solution in Visual Studio or Rider, open `code/gRPC.slnx`.

---

## Folder Structure

```
grpc/
├── code/
│   ├── gRPC.slnx
│   ├── server/              # gRPC server: service definitions and proto contracts
│   │   └── Protos/
│   │       ├── greet.proto
│   │       └── first.proto
│   ├── streaming/           # Console client: all four patterns, hedging, interceptors
│   └── client-integration/  # ASP.NET Core MVC app: gRPC client factory in a web app
├── certificate/
│   └── gRPC In Dotnet - Einav Haimovich.pdf
└── README.md
```

---

Thanks to Irina Scurtu for the course — [From Zero to Hero: gRPC in .NET](https://dometrain.com/course/from-zero-to-hero-grpc-in-dotnet/).

[Certificate of completion](<certificate/gRPC In Dotnet - Einav Haimovich.pdf>)

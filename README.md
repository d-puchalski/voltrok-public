# Voltrok

**Voltrok** is a map-driven multiplayer strategy and economy game. Players develop their territories, manage resources and military units, trade with other players, and participate in country-level politics, diplomacy, and conflict on an interactive world map.

This repository is presented as a recruitment portfolio project. It demonstrates the design and implementation of a stateful web application with domain-driven game rules, background processing, persistence, integrations, and an independently built marketing site.

## Highlights

- Interactive world map with player, country, transport, siege, and conflict overlays.
- Resource economy, inventory, production queues, and time-based growth.
- Military recruitment, transports, combat resolution, siege mechanics, and battle reports.
- Country governance: elections, taxes, trade policies, embargoes, and war declarations.
- General, country, and direct-message chat; notifications; badges; and player rankings.
- Recurring two-week seasons with current and archived leaderboards.
- Account registration and cookie authentication with BCrypt password hashing.
- Optional premium packages and Stripe Checkout/webhook handling.
- Localized game UI, user-selectable visual themes, and a standalone multilingual landing page.

## Architecture at a glance

```text
Browser
  |
  v
VoltrokWebApp  ───────>  VoltrokServices  ───────>  Voltrok.EF / PostgreSQL
(Blazor Server)             |                          (EF Core)
                            v
                       VoltrokUtils
                    (game-rule engines)

VoltrokWorker  ───────────>  scheduled game-state settlement and simulation
landingPage    ───────────>  standalone Svelte/Vite marketing site
```

The web application contains the interactive UI and composition root. Services own application use cases and database access, while reusable, deterministic game calculations live in `VoltrokUtils`. A separate worker keeps time-based state changes out of the HTTP/UI process.

## Tech stack

| Area | Technologies |
| --- | --- |
| Application | C#, .NET 10, ASP.NET Core, Blazor Server interactive components |
| Data | PostgreSQL, Entity Framework Core 10, Npgsql |
| Background processing | .NET hosted services, `BackgroundService`, `PeriodicTimer` |
| Authentication | ASP.NET Core cookie authentication, BCrypt.Net |
| Payments | Stripe Checkout and webhook processing via Stripe.net |
| Observability | Serilog console and rolling-file logging |
| Mapping and browser interop | MapLibre GL JS, JavaScript interop, GeoJSON |
| UI | Razor components, CSS, Bootstrap |
| Marketing site | Svelte 5, Vite 6, Tabler Icons |
| Testing | xUnit, Microsoft.NET.Test.Sdk, Coverlet collector |

## Repository structure

```text
.
├── VoltrokGame.sln        # Main .NET solution
├── VoltrokWebApp/         # Blazor Server game client and HTTP application
│   ├── Components/        # Layout, map, authentication, tabs, dialogs
│   ├── Pages/             # Application shell and dashboard
│   └── wwwroot/           # JavaScript interop, themes, map assets, translations
├── VoltrokWorker/         # Long-running scheduled game workers
│   └── BackgroundWorker/  # Resources, transports, battles, NPCs, seasons, badges
├── VoltrokServices/       # Application/domain services
│   └── Services/          # Auth, players, trade, military, chat, countries, seasons
├── VoltrokUtils/          # Shared domain models, enums, and rule engines
│   └── Engines/           # Combat, balance, building, and military calculations
├── Voltrok.EF/            # EF Core context and persistence entities
├── VoltrokTest/           # Rule-engine unit tests
├── VoltrokTests/          # Service and integration-oriented tests
├── db/                    # PostgreSQL schema, seed data, and migration notes
├── landingPage/           # Standalone Svelte/Vite landing page
└── tools/                 # Data migration, GeoJSON, and diagnostic utilities
```

## Domain and engineering notes

### Separation of responsibilities

- **`VoltrokWebApp`** keeps presentation and interaction concerns in Razor components. It registers the application services and exposes the Stripe webhook endpoint.
- **`VoltrokServices`** implements use cases such as sign-up, country policy management, trade, transports, notifications, chat, seasons, and payments. It uses `IDbContextFactory<AppDbContext>` for scoped database work.
- **`VoltrokUtils`** contains framework-independent calculation logic. For example, the battle simulation normalizes unit stacks, applies counter-based combat power, calculates casualties, and returns a structured outcome for reports.
- **`VoltrokWorker`** runs independent timers for resource growth, military production, transport settlement, combat settlement, NPC simulation, daily statistics, election processing, badges, and season rollover.

### State and consistency

The project deliberately separates request-driven actions from time-driven settlement. A player may create a trade or military transport in the web app, while the worker periodically resolves its completion. This makes the gameplay model explicit and keeps expensive or scheduled operations away from the interactive request path.

The data model uses PostgreSQL foreign keys and unique constraints for core invariants. Services also account for concurrent operations where appropriate—for example, country-war creation handles a unique-constraint race and returns the persisted conflict when another request creates it first.

### Test coverage

The repository includes focused xUnit tests for combat calculations, player need effects, translations, and account sign-up behavior. The rule-engine test project is intended to validate deterministic mechanics independently from the UI.

## Running locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 18 or later (the schema uses `uuidv7()`)
- Node.js 20+ and npm, only for the landing page

### 1. Create a local database

Create a PostgreSQL database and apply the included schema and seed data:

```powershell
createdb voltrok
psql -U postgres -d voltrok -f db/create.sql
```

Set the connection string outside source control. The web app and worker both read the standard .NET configuration key:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Database=voltrok;Username=postgres;Password=your-password"
```

> The supplied `appsettings.json` files are development-oriented. Before sharing or deploying a fork, replace any credentials and keep production secrets in environment variables or a secret manager.

### 2. Restore, build, and run the game

From the repository root:

```powershell
dotnet restore VoltrokGame.sln
dotnet build VoltrokGame.sln
dotnet run --project VoltrokWebApp/VoltrokWebApp.csproj
```

The default development profile serves the application at `https://localhost:7273` (and `http://localhost:5083`). Run the worker in another terminal when testing time-based mechanics:

```powershell
dotnet run --project VoltrokWorker/VoltrokWorker.csproj
```

Stripe configuration is required only for premium checkout flows. Provide the `Stripe__SecretKey`, `Stripe__WebhookSecret`, and relevant price-ID settings through user secrets, environment variables, or your deployment secret store.

### 3. Run tests

```powershell
dotnet test VoltrokTest/VoltrokTest.csproj
dotnet test VoltrokTests/VoltrokTests.csproj
```

Some service-level tests expect a reachable development PostgreSQL database seeded with the game schema.

### 4. Run the landing page (optional)

```powershell
Set-Location landingPage
npm install
npm run dev
```

For a production build, use `npm run build`.

## What this project demonstrates

- Designing a non-trivial domain model with players, countries, resources, transports, military units, battles, chat, seasons, and premium entitlements.
- Extracting reusable and testable game mechanics from delivery and persistence concerns.
- Building a responsive, stateful server-rendered UI with JavaScript mapping integration.
- Coordinating asynchronous gameplay workflows through database-backed state and hosted workers.
- Applying practical application concerns: password hashing, authentication cookies, request logging, error handling, database retries, payment-webhook verification, and configuration separation.
- Working across an end-to-end product surface: gameplay UX, supporting services, persistence schema, operations, tests, and a public-facing landing page.

## Portfolio scope

Voltrok is an active portfolio codebase rather than a packaged commercial product. The repository is best reviewed as an example of architectural decisions, domain modeling, and full-stack implementation. Third-party assets and services remain subject to their respective licenses and terms.

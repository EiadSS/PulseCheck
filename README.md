# PulseCheck

PulseCheck is a full-stack uptime monitoring platform built as a production-style portfolio project. Users can register, add website/API monitors, collect scheduled health checks, track incidents, view reliability metrics, receive alerts, and share a public status page.

## Features

- JWT authentication with user-scoped monitors.
- Website/API monitor CRUD with interval, timeout, expected status, degraded-threshold, and required-keyword checks.
- Scheduled background health checks with persisted check history.
- Status classification: `Up`, `Degraded`, `Error`, `Down`, and `Paused`.
- Incident tracking with automatic open/recovery transitions.
- In-app and email alert support for failures, recoveries, and SSL certificate issues.
- SSL certificate expiry checks with warning and critical states.
- Reliability dashboard with uptime target and downtime-budget tracking.
- React dashboard with filters, status cards, response-time charts, check history, and incident history.
- Public workspace status page at `/status/{slug}`.
- Docker Compose local development and GitHub Actions CI.

## Tech Stack

- Frontend: React, TypeScript, Vite, Tailwind CSS, Recharts, SignalR client.
- Backend: ASP.NET Core Web API, EF Core, PostgreSQL, Identity, JWT, SignalR, hosted worker.
- Tests: xUnit for backend tests and Vitest/Testing Library for frontend tests.

## Quick Start

```bash
docker compose up --build
```

Then open:

- Frontend: [http://localhost:5173](http://localhost:5173)
- API health: [http://localhost:5080/health](http://localhost:5080/health)

Create an account from the landing page, then add your first monitor from the dashboard.

## Local Development

Start PostgreSQL locally and update `backend/PulseCheck.Api/appsettings.json` or use environment variables:

```bash
dotnet run --project backend/PulseCheck.Api
```

In another terminal:

```bash
cd frontend
npm install
npm run dev
```

The frontend expects the API at `http://localhost:5080` by default. Override with `VITE_API_URL`.

## Configuration

For Docker-based development, copy `.env.example` to `.env` and adjust values for your machine.

Common production settings:

- `Jwt__Secret`: long random JWT signing secret.
- `ConnectionStrings__DefaultConnection` or `DATABASE_URL`: PostgreSQL connection.
- `PulseCheck__AllowedOrigins`: allowed frontend origins.
- `PulseCheck__FrontendBaseUrl`: frontend URL used in alert links.
- Email provider settings: required only if email alerts should be sent.

The API can auto-create the local database schema when `PulseCheck__AutoMigrate=true`.

## Health Check Rules

- `Up`: expected status/keyword matched and response time is below the degraded threshold.
- `Degraded`: expected status/keyword matched, but response time is at or above the degraded threshold.
- `Error`: host responded, but status code or expected keyword did not match.
- `Down`: timeout, DNS failure, connection failure, SSL error, or unreachable host.

If a monitor changes from `Up`/`Degraded` to `Down`/`Error`, PulseCheck opens an incident. When it later returns to `Up`/`Degraded`, PulseCheck resolves the incident.

## Notifications And SSL

PulseCheck always creates in-app alerts for monitor failure/recovery and SSL certificate expiry warnings. Email alerts are sent to the user's account email when app-level email delivery is configured.

SSL states:

- `Valid`: certificate expires in more than 14 days.
- `ExpiringSoon`: certificate expires within 14 days.
- `Critical`: certificate expires within 7 days.
- `Expired`: certificate expiry has passed.
- `Unavailable`: PulseCheck could not read certificate details.
- `NotApplicable`: monitor URL is not HTTPS.

## Tests

```bash
dotnet test backend/PulseCheck.Tests/PulseCheck.Tests.csproj
cd frontend
npm test
npm run build
```

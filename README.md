# RaceDay

RaceDay is a platform for running, walking and cycling events.

- **RaceDay.Api** - ASP.NET Core Web API (EF Core Code-First, SQL Server, session authentication, Swagger, Azure Blob Storage).
- **RaceDay.Mvc** - ASP.NET Core MVC web front end. It only talks to the API (no database, no Azure access of its own).
- **RaceDay.Tests** - xUnit tests for the API.
- **GitHub Actions** - builds everything, runs the tests and checks that the Docker image builds.

## Video demonstration
YouTube (unlisted): https://youtu.be/OV_-RRt2OEw 

## Project structure
```
RaceDay.sln
RaceDay.Api/        Web API: Controllers, Models, Data, Dtos, Security, Services
RaceDay.Mvc/        MVC app: Controllers, Views, Models, Services (ApiClient), Dockerfile
RaceDay.Tests/      API unit/integration tests
.github/workflows/  CI pipeline
docker-compose.yml  Optional one-command start for the MVC container
```

## The two roles
| Role | What they can do |
|------|------------------|
| **Organiser** | Dashboard (events, enrolments per event, upcoming dates); create, edit and delete events with a banner image; add categories; view enrolments and change their status; capture and publish finish times and positions; manage own profile. |
| **Participant** | Browse and filter upcoming events; view event details and categories; enter an event by choosing a category; see own entries with colour-coded status; see own results ("47th out of 312 finishers"); edit profile and upload a profile picture. |

Organiser registration requires a secret code (`Registration:OrganiserCode` in the API settings, default `RACEDAY-ORG-2026`) so nobody can simply pick the Organiser role.

## Prerequisites
- Visual Studio 2022 or newer with the ASP.NET workload, .NET SDK matching the projects
- SQL Server LocalDB (installed with Visual Studio)
- Docker Desktop (for the container)
- An Azure Storage account (for image uploads)

## 1. Configure Azure Blob Storage (API)
1. Create a Storage account in the Azure portal. Under **Configuration** set *Allow Blob anonymous access* to **Enabled**.
2. Copy a **connection string** from *Access keys*.
3. Store it as a user secret - never commit it:
   - Visual Studio: right-click **RaceDay.Api** -> *Manage User Secrets*, then paste:
   ```json
   { "AzureBlob": { "ConnectionString": "<your connection string>" } }
   ```
   - or set the environment variable `AzureBlob__ConnectionString`.
4. Images go to the container `raceday-images` (created automatically with public blob access).

## 2. Create the database
In the Package Manager Console (default project **RaceDay.Api**):
```
Update-Database
```
The API also applies migrations on start-up.

## 3. Run locally (no Docker)
1. Right-click the solution -> *Configure Startup Projects* -> **Multiple startup projects**: set **RaceDay.Api** and **RaceDay.Mvc** to *Start*.
2. Press **F5**.
   - API + Swagger: http://localhost:5080/swagger
   - MVC app: http://localhost:5090

## 4. Run the MVC app in Docker
Start the API so that Docker can reach it (in a terminal at the repo root):
```
dotnet run --project RaceDay.Api --urls http://0.0.0.0:5080
```
Then build and run the MVC container with **one command**:
```
docker build -t raceday-mvc ./RaceDay.Mvc && docker run --rm -p 8080:8080 -e ApiBaseUrl=http://host.docker.internal:5080 raceday-mvc
```
Open http://localhost:8080. The container behaves the same as the local run (same culture, same API calls).

Alternative: `docker compose up --build`

## Authentication
- The MVC app logs in through the API (`POST /api/auth/login`). The API creates a server-side session holding the user id and role.
- The MVC app keeps its own server-side session with the user id, role and the API session, and sends that session to the API on every call.
- Menus differ per role; unauthenticated users are redirected to Login; logging out clears both sessions.
- Passwords are hashed (PBKDF2 + salt) in the API.

## Tests and CI/CD
- Run tests: `dotnet test` or Visual Studio Test Explorer.
- Every push runs the workflow in `.github/workflows/part2-ci.yml`: restore, build (API, MVC, tests), run tests, build the Docker image.

![Green build](docs/ci-green-build.png)

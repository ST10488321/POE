# PROG6212 Part 3 – RaceDay MVC, Azure Blob Storage, Docker (100 marks)

All code is in **RaceDay_Part3_All_Code.md**. This guide tells you what to do with it, in order.

---

## 0. Rubric → how it is covered

| Rubric | Marks | What earns full marks |
|---|---|---|
| Correct Submission | 5 | Clear folders, README with Docker run command, CI screenshot, video link |
| GitHub and CI/CD | 10 | 20+ meaningful commits, workflow builds MVC, **green build** |
| Authentication & Role Navigation | 10 | MVC register/login via API, session stores user ID + role, different menus per role, logout clears session, unauthenticated users redirected |
| Organiser Interface | 15 | Dashboard, events (create/edit/delete + banner), categories, enrolments, results entry |
| Participant Interface | 15 | Home with filters, event detail + enrol, My Events, My Results (finish time + position), Profile + picture |
| Azure Blob Storage | 10 | MVC → API → Azure; URL saved in DB; image displayed in MVC |
| Docker | 10 | Dockerfile, container runs, `docker run` command in README |
| UI Design | 10 | RaceDay theme, Bootstrap, colour-coded statuses, consistent layout, friendly errors |
| Video | 15 | Script in section 10 |

---

## 1. Corrections to Part 2 (do these first)

1. **Build failure cause #1 – missing `using Xunit`.** Add the new file `RaceDay.Tests/GlobalUsings.cs` (it contains `global using Xunit;`).
2. **Build failure cause #2 – .NET version in CI.** The new workflow installs .NET 8, 9 and 10 so it builds whatever your projects target.
3. **Test project packages.** Open `RaceDay.Tests.csproj`. It must reference: `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` and the project reference to `RaceDay.Api` (see `RaceDay.Tests.csproj.reference`). Keep your own `<TargetFramework>`.
4. **API additions Part 3 needs** (the files are in the code document):
   - Events get `BannerImageUrl`, users get `ProfileImageUrl`.
   - New endpoints: `POST /api/events/{id}/banner`, `POST /api/users/me/picture`, `GET /api/events/mine`.
   - Events list now filters by `search` and `upcoming`.
   - Results now return event date, category and `totalFinishers` (for "47th out of 312").
5. **Update your ERD / Part 1 plan:** add `BannerImageUrl` (Events table) and `ProfileImageUrl` (Users table), and list the new endpoints.
6. **README layout:** your repo is flat (no `Part2-RaceDay-API` folder). The new README matches this.

---

## 2. Azure Blob Storage setup (portal.azure.com)

1. **Create a resource → Storage account.** Name e.g. `racedaystorage123` (lowercase, unique). Region near you. Redundancy: **LRS**. Create.
2. Open the account → **Settings → Configuration** → set **Allow Blob anonymous access = Enabled** → Save. (Without this, images will not display.)
3. **Security + networking → Access keys → Show** → copy **Connection string** (key1).
4. In Visual Studio: right-click **RaceDay.Api → Manage User Secrets** and paste:
   ```json
   {
     "AzureBlob": { "ConnectionString": "PASTE_CONNECTION_STRING_HERE" }
   }
   ```
   This keeps the secret out of GitHub. **Never commit the connection string.**
5. In `RaceDay.Api/appsettings.json` add (no secret in it):
   ```json
   "AzureBlob": { "ConnectionString": "", "ContainerName": "raceday-images" },
   ```
   The container is created automatically on the first upload.

---

## 3. Update the API

1. Package Manager Console (default project **RaceDay.Api**):
   ```
   Install-Package Azure.Storage.Blobs
   ```
2. Replace or add these files (paths in the code document):
   - `RaceDay.Api/Models/Entities.cs` (replace)
   - `RaceDay.Api/Dtos/Dtos.cs` (replace)
   - `RaceDay.Api/Services/BlobStorageService.cs` (**new**)
   - `RaceDay.Api/Controllers/EventsController.cs` (replace)
   - `RaceDay.Api/Controllers/UsersController.cs` (replace)
   - `RaceDay.Api/Controllers/ResultsController.cs` (replace)
   - `RaceDay.Api/Program.cs` (replace – only one line added: the `IBlobStorageService` registration)
   - `RaceDay.Tests/TestSupport.cs` (replace), `RaceDay.Tests/ImageAndSummaryTests.cs` (**new**), `RaceDay.Tests/GlobalUsings.cs` (**new**)
3. Create the migration for the two new columns:
   ```
   Add-Migration AddImageUrls
   Update-Database
   ```
4. Build (**Ctrl+Shift+B**). Run tests in **Test Explorer → Run All** – all must pass.
5. Run the API (F5) and test in Swagger: login as Organiser → `POST /api/events/{id}/banner` with an image → open the returned URL in a browser to confirm it loads from Azure.

---

## 4. Create the MVC project

1. Right-click the solution → **Add → New Project → ASP.NET Core Web App (Model-View-Controller)**. Name: **RaceDay.Mvc**. Authentication: **None**. Untick *Configure for HTTPS*. Tick *Enable container support*: **No** (we use our own Dockerfile).
2. **Delete** everything the template created in `Controllers`, `Models`, `Views` and `wwwroot` (keep the project file and `appsettings.json`).
3. Create all files from the code document under `RaceDay.Mvc/` with the exact folder names (Controllers, Models, Services, Security, Properties, Views/..., wwwroot/css, Dockerfile, .dockerignore). Replace `Program.cs`, `appsettings.json`, `Properties/launchSettings.json`.
4. Keep **your** `RaceDay.Mvc.csproj` (it has the right `<TargetFramework>`). It needs no extra packages.
5. Build. Fix any red errors (send me a photo of the Error List if something fails).

**Set both projects to start:** right-click the solution → **Configure Startup Projects → Multiple startup projects** → set RaceDay.Api and RaceDay.Mvc to **Start**. F5 opens Swagger (5080) and the MVC app (5090).

---

## 5. How the MVC app works (know this for the video)

- **No database, no Azure in MVC.** Everything goes through `Services/ApiClient.cs`, which calls the API over HTTP.
- **Login:** MVC posts email/password to `POST /api/auth/login`. The API creates its session and returns a cookie. `ApiClient` stores that cookie **server-side in the MVC session** together with UserId and Role. On every later call it sends that cookie to the API.
- **Role navigation:** `_Layout.cshtml` shows different menus for Organiser and Participant. `[RoleAuthorize("Organiser")]` / `("Participant")` protect controllers. Not logged in → redirect to Login. Wrong role → Access Denied. The API checks the role again (defence in depth).
- **Logout:** calls `POST /api/auth/logout` and clears the MVC session.
- **Uploads:** the browser sends the file to MVC → MVC forwards it to the API (`multipart`) → the API validates it and uploads it to Azure Blob Storage → the API saves the URL in SQL Server → MVC shows that URL in an `<img>`.
- **Friendly errors:** API errors are turned into short messages; unexpected errors go to `/Home/Error`.
- **Same culture everywhere** (en-US) so the app behaves the same locally and in Docker.

---

## 6. Test everything locally (do this before recording)

1. Register an **Organiser** (code `RACEDAY-ORG-2026`) and a **Participant** (use two browsers or a private window).
2. Organiser: create an event with a banner → add categories ("5km", "10km") → open Dashboard.
3. Participant: Home → filter → open event → enter with a category → **My Events** (status colour) → **Profile** (upload picture).
4. Organiser: Enrolments → change a status to Pending/Confirmed → Results → enter `00:45:30` and position `1` → Save & publish.
5. Participant: **My Results** shows "1st out of 1 finishers".
6. Try bad input (wrong time format, empty fields, wrong file type) – you should see friendly messages.
7. Logout, then type a protected URL (e.g. `/Organiser/Dashboard`) → redirected to Login.

---

## 7. Docker

1. Install **Docker Desktop** (WSL2 option on Windows) and start it.
2. Start the API so the container can reach it (terminal in the repo root):
   ```
   dotnet run --project RaceDay.Api --urls http://0.0.0.0:5080
   ```
   Allow the Windows Firewall prompt.
3. Build and run (one line):
   ```
   docker build -t raceday-mvc ./RaceDay.Mvc && docker run --rm -p 8080:8080 -e ApiBaseUrl=http://host.docker.internal:5080 raceday-mvc
   ```
4. Open http://localhost:8080 and repeat a quick login test. It must behave the same as the local run.
5. If your project targets .NET 9/10, change `ARG DOTNET_VERSION=8.0` in the Dockerfile (and `--build-arg DOTNET_VERSION=...` in the workflow) to `9.0` or `10.0`.

**Dockerfile best practices used (mention in the video):** multi-stage build, restore layer cached, small `aspnet` runtime image, non-root `USER app`, `.dockerignore`, port set by `ASPNETCORE_URLS`.

---

## 8. CI/CD

1. Replace `.github/workflows/part2-ci.yml` with the new version (installs .NET 8/9/10, builds the solution, runs tests, builds the MVC project, builds the Docker image).
2. Make sure **RaceDay.Mvc is in `RaceDay.sln`** (it is if you added it from Visual Studio).
3. Commit and push → GitHub **Actions** tab → wait for the green tick.
4. Screenshot the green run → save as `docs/ci-green-build.png` → commit.
5. If red: open the run → the red step → send me a photo of the error.

---

## 9. GitHub commits (20+ meaningful)

Work on a branch called **`part3-mvc`** (`Git → New Branch`), push after every few commits, then merge to master at the end.

1. Add RaceDay.Mvc project to solution
2. Configure session, HttpClient and localisation in MVC Program
3. Add ApiClient service with session cookie forwarding
4. Add API response and view models
5. Add role authorization filter for MVC pages
6. Add base controller with error handling helpers
7. Add shared layout with role-based navigation
8. Add RaceDay theme stylesheet
9. Add account controller and login/register views
10. Add profile page with picture upload
11. Add Azure Blob storage service to API
12. Add banner and profile picture upload endpoints
13. Add database migration for image URLs
14. Add Organiser dashboard
15. Add event management pages
16. Add category management page
17. Add enrolment management page
18. Add results entry page
19. Add participant home with filters
20. Add event details and enrolment
21. Add My Events and My Results pages
22. Add API tests for uploads and results totals
23. Add Dockerfile and docker-compose
24. Update CI workflow to build MVC and Docker image
25. Update README with Docker run command and CI screenshot

Tips: commit with your own account; real fixes you make count as extra commits; never commit secrets or `bin/obj`.

---

## 10. Video script (6–9 minutes, unlisted on YouTube)

1. **Intro (30 s):** what RaceDay is; MVC + API + Azure + Docker.
2. **MVC structure (1.5 min):** Controllers (Account, Organiser, Participant), Views, `ApiClient`, `RoleAuthorize`, why MVC has no database.
3. **Authentication (1 min):** login through the API, session stores UserId + Role, menus differ per role, logout, redirect when not logged in.
4. **Organiser demo (2 min):** dashboard, create event + banner, categories, enrolments, results.
5. **Participant demo (1.5 min):** home + filters, enrol, My Events (colour statuses), My Results, profile picture.
6. **Blob Storage (1 min):** show the container in the Azure portal, the blob, the URL in the DB, the image in MVC; explain MVC → API → Azure.
7. **Docker (1 min):** Dockerfile walk-through, run the command, show the app in the container.
8. **GitHub (30 s):** commits and the green Actions build.

---

## 11. Possible viva questions

- *Why can't the MVC app talk to the database or Azure directly?* Separation of concerns: all business rules and storage live in the API, so any client sees the same rules.
- *How is the role kept between pages?* Server-side MVC session (UserId, Role, API cookie). The browser only holds a session id cookie.
- *Why `UseCookies = false` on the HttpClient?* A shared cookie jar would mix up different users' API sessions.
- *Why is the connection string in user secrets?* Secrets must never be in Git.
- *What does the Dockerfile's multi-stage build give you?* Small final image without the SDK.
- *Why does the Docker app use `host.docker.internal`?* The container needs a name for the host machine where the API is running.

---

## 12. Common problems

| Problem | Fix |
|---|---|
| `Add-Migration` not recognised | `Install-Package Microsoft.EntityFrameworkCore.Tools` in RaceDay.Api |
| Upload says "could not be uploaded" | Connection string missing/wrong; *Allow Blob anonymous access* not enabled |
| Image uploads but does not show | Anonymous access disabled on the storage account |
| MVC says "service is not reachable" | API not running, wrong `ApiBaseUrl`, or (Docker) API not started with `--urls http://0.0.0.0:5080` |
| Docker build fails on image tag | Match `DOTNET_VERSION` to your `<TargetFramework>` |
| CI fails on `NETSDK1045` | Workflow must install your SDK version (the new one installs 8, 9 and 10) |
| `The type or namespace 'Fact' could not be found` | Add `RaceDay.Tests/GlobalUsings.cs` |
| Logged in but every page sends me back to Login | API session expired (30 min idle). Log in again |

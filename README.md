# Inventory App — Backend

A .NET 10 ASP.NET Core Web API for warehouse and inventory management with secure authentication, role-based authorization, and audit trails.

> **Frontend**: an Angular 22 client for this API lives in [FRONTEND-INVENTORY-WEB-APP](https://github.com/AlonLyubarov/FRONTEND-INVENTORY-WEB-APP) — warehouse owner dashboard, inventory screens, and team management built on this contract. CORS is preconfigured for its dev origin (`http://localhost:4200`).

## Features

- **Warehouse Management**: Multi-warehouse support with owner-based authorization
- **Inventory System**: Item tracking with soft deletes and audit logging
- **Authentication & Authorization**: JWT-based auth with role-based access control (RBAC)
- **Audit Trail**: Transaction history for all item operations
- **Database Transactions**: Atomic operations for data consistency

## Getting Started

### Prerequisites

- .NET 10 SDK
- SQL Server
- A code editor (Visual Studio, VS Code, etc.)

### Setup

1. Clone the repository
2. Configure environment variables (copy `.env.example` to `.env` for local development)
3. Run migrations:
   ```bash
   dotnet ef database update --project AlonProject.Infrastructure
   ```
4. Start the application:
   ```bash
   dotnet run --project AlonProject.Api
   ```

### Environment Configuration

Copy `.env.example` and set your values:
- `Jwt__Key`: A strong secret key for JWT signing (64+ characters)
- `ConnectionStrings__SqlServer`: Your SQL Server connection string
- `ASPNETCORE_ENVIRONMENT`: Development or Production

### Test Credentials (Local Development)

A test user is automatically seeded into the database:
- **Username**: `testuser`
- **Password**: `password123`
- **Role**: Employee

Use these credentials to test the Login endpoint during development.

## Architecture

The project follows a layered architecture:

- **Domain**: Core entities and business logic
- **Application**: Services, DTOs, and interfaces
- **Infrastructure**: EF Core repositories, migrations, and data access
- **Api**: REST controllers and endpoints

## Security

- JWT tokens include warehouse isolation claims
- Admin operations require warehouse ownership verification
- Soft deletes preserve audit history
- Database operations use transactions for atomicity
- Sensitive configuration via environment variables

## Database

Uses Entity Framework Core with SQL Server. Migrations are located in `AlonProject.Infrastructure/Migrations`.



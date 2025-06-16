# Clean Architecture Database Sharding Solution

This solution demonstrates a production-ready database sharding implementation using Clean Architecture principles with .NET 9, PostgreSQL, and consistent hashing.

## 🚀 Quick Start

### 1. Prerequisites
- .NET 9 SDK
- Docker & Docker Compose
- Git

### 2. Clone and Setup
```bash
git clone <repository-url>
cd DatabaseSharding/docker/
```

### 3. Start PostgreSQL Shards
```bash
docker-compose up -d
```

This starts:
- 3 PostgreSQL instances (ports 5432, 5433, 5434)
- Each with its own database and credentials
- Automatic health checks

### 4. Run the Application
```bash
cd src/Presentation/DatabaseSharding.Api
dotnet restore
dotnet run
```

### 5. Test the Setup

**Check Health:**
```bash
curl https://localhost:7000/health
```

**Test Sharding Distribution:**
```bash
curl https://localhost:7000/api/sharding/test-sharding
```

**Access Swagger UI:**
Navigate to `https://localhost:7000/swagger`

## 🧪 Testing Examples

### Create Users (Automatically Sharded)
```bash
# User 1
curl -X POST https://localhost:7000/api/users \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@example.com","firstName":"Alice","lastName":"Smith"}'

# User 2  
curl -X POST https://localhost:7000/api/users \
  -H "Content-Type: application/json" \
  -d '{"email":"bob@example.com","firstName":"Bob","lastName":"Johnson"}'
```

### Check Which Shard a Key Maps To
```bash
curl https://localhost:7000/api/sharding/shard-for-key/alice@example.com
```

### Retrieve a User (Searches All Shards)
```bash
curl https://localhost:7000/api/users/{user-id}
```

## 🏗️ Architecture Overview

### **Clean Architecture Layers**

1. **Domain Layer** (`DatabaseSharding.Domain`)
   - Pure business logic with no external dependencies  
   - Contains entities, value objects, domain services interfaces
   - Defines repository contracts and domain exceptions

2. **Application Layer** (`DatabaseSharding.Application`)
   - Contains application business rules
   - Implements CQRS with MediatR
   - Handles validation and logging via pipeline behaviors
   - Defines DTOs and application service contracts

3. **Infrastructure Layer** (`DatabaseSharding.Infrastructure`)
   - Implements data access and external concerns
   - Contains Entity Framework configurations
   - Implements repository patterns and sharding services
   - Handles database connections and health monitoring

4. **Presentation Layer** (`DatabaseSharding.Api`)
   - Web API controllers and HTTP concerns
   - Dependency injection configuration
   - Middleware and filters
   - API documentation with Swagger

### **Project Structure**
```
src/
├── Core/
│   ├── DatabaseSharding.Domain/        # Domain entities, value objects, interfaces
│   └── DatabaseSharding.Application/   # Use cases, DTOs, application services
├── Infrastructure/
│   └── DatabaseSharding.Infrastructure/ # Data access, external services
└── Presentation/
    └── DatabaseSharding.Api/           # Web API, controllers, configuration
```

## 🔧 Key Features

### **Sharding Features**
- **Consistent Hashing**: SHA-1 based with 150 virtual nodes per shard
- **Automatic Database Initialization**: Creates tables on startup
- **Health Monitoring**: Background service checks shard health every 5 minutes  
- **Even Distribution**: Virtual nodes ensure balanced data distribution
- **Fault Tolerance**: Continues operating with degraded shards

### **Clean Architecture Benefits**
- **CQRS + MediatR**: Clear separation of commands and queries
- **Domain-Driven Design**: Rich domain models with encapsulated business logic
- **Repository Pattern**: Clean data access abstractions
- **Validation Pipeline**: Automatic request validation using FluentValidation
- **Logging Pipeline**: Comprehensive request/response logging
- **Result Pattern**: Consistent error handling across layers

## 📊 How Sharding Works

### **Data Distribution**
1. **Shard Key**: User's email address
2. **Hash Function**: SHA-1 generates consistent hash values
3. **Virtual Nodes**: 150 virtual nodes per shard for even distribution
4. **Shard Selection**: Hash ring determines which shard stores the data

### **Read/Write Operations**
- **Create/Update/Delete by Email**: Direct shard lookup using consistent hashing
- **Read by ID**: Fan-out search across all active shards (less efficient)
- **List Operations**: Parallel queries across all shards with result aggregation

### **Sample Distribution Test**
The `/api/sharding/test-sharding` endpoint shows how 8 test email addresses distribute across your 3 shards:

```json
{
  "message": "Sharding test completed",
  "distribution": {
    "shard_0": ["alice@example.com", "charlie@example.com"],
    "shard_1": ["bob@example.com", "eve@example.com", "frank@example.com"],
    "shard_2": ["diana@example.com", "grace@example.com", "henry@example.com"]
  },
  "totalEmails": 8,
  "shardsUsed": 3
}
```

## ⚙️ Configuration

Update `appsettings.json` to configure sharding:

```json
{
  "Sharding": {
    "ShardConnectionStrings": [
      "Host=localhost;Port=5432;Database=database1;Username=admin1;Password=password123",
      "Host=localhost;Port=5433;Database=database2;Username=admin2;Password=password456", 
      "Host=localhost;Port=5434;Database=database3;Username=admin3;Password=password789"
    ],
    "VirtualNodesPerShard": 150,
    "HealthCheckInterval": "00:05:00"
  }
}
```

## 🔍 Monitoring & Debugging

### **Health Checks**
- **Endpoint**: `GET /health`
- **Background Service**: Runs every 5 minutes
- **Status**: Healthy/Degraded/Unhealthy based on shard availability

### **Logging**
- **Application Logs**: Request/response logging via MediatR behaviors
- **Infrastructure Logs**: Database operations and health checks
- **Shard Operations**: Which shard handles each operation

### **Database Inspection**
Each shard gets seeded with a test user on first initialization:
- `test-shard_0@example.com` → Shard 0
- `test-shard_1@example.com` → Shard 1  
- `test-shard_2@example.com` → Shard 2

## 🛠️ Troubleshooting

### **"relation 'Users' does not exist" Error**

This error occurs when the PostgreSQL database exists but the `Users` table hasn't been created. Here's how the new initialization system fixes this:

#### **🔧 Enhanced Database Initialization**

The `DatabaseInitializationService` now uses a **3-tier approach** to ensure tables are created:

1. **Method 1: EnsureCreated**
   ```csharp
   await context.Database.EnsureCreatedAsync(cancellationToken);
   ```
   - Standard Entity Framework approach
   - Creates database and all tables from DbContext configuration

2. **Method 2: Delete and Recreate**
   ```csharp
   await context.Database.EnsureDeletedAsync(cancellationToken);
   await context.Database.EnsureCreatedAsync(cancellationToken);
   ```
   - If Method 1 fails, completely recreate the database
   - Ensures clean state

3. **Method 3: Manual Table Creation**
   ```sql
   CREATE TABLE IF NOT EXISTS "Users" (
       "Id" uuid PRIMARY KEY,
       "Email" varchar(255) NOT NULL UNIQUE,
       "FirstName" varchar(100) NOT NULL,
       "LastName" varchar(100) NOT NULL,
       "CreatedAt" timestamp with time zone NOT NULL,
       "UpdatedAt" timestamp with time zone NOT NULL
   );
   ```
   - Raw SQL execution as last resort
   - Guarantees table creation

#### **🧪 Verification Process**

Each shard goes through rigorous verification:

```csharp
// 1. Check if table exists
var tableExists = await CheckIfUsersTableExistsAsync(context, cancellationToken);

// 2. Test with actual query
var userCount = await context.Users.CountAsync(cancellationToken);

// 3. Seed test data for verification
await SeedTestDataAsync(context, shardId, cancellationToken);
```

#### **⚙️ Configuration Options**

Control initialization behavior in `appsettings.json`:

```json
{
  "Sharding": {
    "InitializeDatabasesOnStartup": true,        // Enable/disable auto-init
    "SeedTestDataOnInitialization": true,       // Add test users
    "DatabaseInitializationTimeoutSeconds": 60, // Timeout per shard
    "MaxRetryAttempts": 3                       // Retry failed shards
  }
}
```

#### **🔍 Debugging Steps**

1. **Check Docker Containers:**
   ```bash
   docker-compose ps
   # Should show all 3 PostgreSQL containers as "Up"
   ```

2. **View Application Logs:**
   ```bash
   dotnet run
   # Look for initialization messages:
   # "Initializing database for shard_0"
   # "Successfully verified shard_0: Users table exists with 1 records"
   ```

3. **Test Database Connection:**
   ```bash
   # Connect to each shard manually
   docker exec -it postgres_shard_1 psql -U admin1 -d database1
   \dt  # List tables - should show "Users"
   SELECT * FROM "Users";  # Should show test data
   ```

4. **Check API Health:**
   ```bash
   curl https://localhost:7000/health
   # Should return "Healthy" status
   ```

#### **🚨 Common Issues and Solutions**

**Issue: "Connection refused"**
```bash
# Solution: Ensure Docker containers are running
docker-compose up -d
docker-compose ps
```

**Issue: "Authentication failed"**
```bash
# Solution: Check connection strings match Docker setup
# Default: admin1/password123, admin2/password456, admin3/password789
```

**Issue: "Timeout during initialization"**
```json
// Solution: Increase timeout in appsettings.json
{
  "Sharding": {
    "DatabaseInitializationTimeoutSeconds": 120
  }
}
```

**Issue: "Users table exists but empty"**
```json
// Solution: Enable test data seeding
{
  "Sharding": {
    "SeedTestDataOnInitialization": true
  }
}
```

#### **🏥 Manual Recovery**

If automatic initialization fails, you can manually create tables:

```sql
-- Connect to each database and run:
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "Email" varchar(255) NOT NULL UNIQUE,
    "FirstName" varchar(100) NOT NULL,
    "LastName" varchar(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");
```

#### **📊 Success Indicators**

When initialization works correctly, you'll see:

```
info: DatabaseSharding.Infrastructure.Services.DatabaseInitializationService[0]
      Starting database initialization for 3 shards
info: DatabaseSharding.Infrastructure.Services.DatabaseInitializationService[0]
      Successfully verified shard_0: Users table exists with 1 records
info: DatabaseSharding.Infrastructure.Services.DatabaseInitializationService[0]
      Successfully verified shard_1: Users table exists with 1 records
info: DatabaseSharding.Infrastructure.Services.DatabaseInitializationService[0]
      Successfully verified shard_2: Users table exists with 1 records
info: DatabaseSharding.Infrastructure.Services.DatabaseInitializationService[0]
      Database initialization completed successfully for all shards
```

The enhanced initialization system should resolve the "Users does not exist" error completely and provide detailed logging to help debug any remaining issues.

## 🧪 Testing

The solution includes comprehensive testing:

### **Manual Testing**
1. Use Swagger UI at `/swagger`
2. Test sharding distribution at `/api/sharding/test-sharding`
3. Monitor health at `/health`

### **Automated Testing** (Future)
- **Unit Tests**: Test domain logic and application handlers
- **Integration Tests**: Test API endpoints and database operations  
- **Architecture Tests**: Verify clean architecture principles

## 🚀 Production Considerations

### **Scalability**
- **Adding Shards**: Update configuration and trigger rebalancing
- **Load Balancing**: Use multiple API instances behind load balancer
- **Caching**: Add Redis for read optimization
- **Connection Pooling**: Configured automatically with Npgsql

### **Reliability**
- **Circuit Breakers**: Add Polly for resilience patterns
- **Monitoring**: Integrate with APM tools (Application Insights, Datadog)
- **Backup Strategy**: Implement per-shard backup and recovery
- **Zero-Downtime Deployment**: Use blue-green deployment strategies

### **Security**
- **Authentication**: Add JWT or similar authentication
- **Authorization**: Implement role-based access control
- **Connection Security**: Use SSL/TLS for database connections
- **Secrets Management**: Use Azure Key Vault or similar

This Clean Architecture implementation provides a solid foundation for a scalable, maintainable database sharding solution that follows industry best practices and can grow with your application needs.
*/

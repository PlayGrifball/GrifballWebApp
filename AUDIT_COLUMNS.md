# Audit Columns Implementation

This document explains the audit columns feature that has been implemented to track who created and modified records in the database.

## Overview

The audit system automatically populates audit columns (`CreatedByID`, `ModifiedByID`, `CreatedAt`, `ModifiedAt`) on all auditable entities using Entity Framework Core's SaveChanges interceptor.

## Components

### 1. IAuditable Interface
Located in: `Models/IAuditable.cs`

Defines the contract for auditable entities:
- `CreatedByID` - ID of the user who created the record
- `ModifiedByID` - ID of the user who last modified the record  
- `CreatedAt` - Timestamp when the record was created
- `ModifiedAt` - Timestamp when the record was last modified

### 2. AuditableEntity Base Class
Located in: `Models/AuditableEntity.cs`

Abstract base class that implements `IAuditable` and provides:
- All audit properties
- Navigation properties to `User` entity for `CreatedBy` and `ModifiedBy`

### 3. AuditInterceptor
Located in: `Interceptors/AuditInterceptor.cs`

SaveChanges interceptor that:
- Automatically sets audit fields on entity creation and modification
- Uses `ICurrentUserService` to get the current authenticated user
- Sets timestamps using `DateTime.UtcNow`
- Handles both synchronous and asynchronous save operations

### 4. CurrentUserService
Located in: `Services/CurrentUserService.cs`

Service that extracts the current user ID from:
- `HttpContext.User` claims
- `ClaimTypes.NameIdentifier` claim
- Returns `null` if no authenticated user is found

### 5. AuditableEntityConfiguration
Located in: `Configuration/AuditableEntityConfiguration.cs`

Helper class that configures:
- Audit field database mappings
- Foreign key relationships to User entity
- RESTRICT delete behavior for audit references

## Usage

### Making an Entity Auditable

1. Inherit from `AuditableEntity`:
```csharp
public partial class Team : AuditableEntity
{
    // existing properties...
}
```

2. Update entity configuration:
```csharp
public void Configure(EntityTypeBuilder<Team> entity)
{
    // existing configuration...
    
    // Configure audit fields
    AuditableEntityConfiguration<Team>.ConfigureAuditFields(entity);
}
```

### Example Models Updated
The following models have been updated to include audit functionality:
- `Team` - Event teams
- `Season` - Seasons/tournaments
- `Match` - Game matches

## Database Schema

The migration `20250809174243_AddAuditColumns` adds:

### Columns Added to Each Auditable Table:
- `CreatedAt` (datetime2, required)
- `ModifiedAt` (datetime2, required) 
- `CreatedByID` (int, nullable)
- `ModifiedByID` (int, nullable)

### Indexes Created:
- `IX_{Table}_CreatedByID`
- `IX_{Table}_ModifiedByID`

### Foreign Key Constraints:
- `FK_{Table}_Users_CreatedByID` with RESTRICT delete
- `FK_{Table}_Users_ModifiedByID` with RESTRICT delete

## Configuration

### Program.cs Registration
```csharp
// Register services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register interceptor
builder.Services.AddDbContextFactory<GrifballContext>((services, options) =>
    options.UseSqlServer(connectionString)
           .AddInterceptors(new AuditInterceptor(services)));
```

## Temporal Table Support

The audit columns are fully compatible with SQL Server temporal tables:
- All audit columns include temporal annotations
- History tables automatically track audit field changes
- Maintains existing temporal functionality

## Testing

Test suite located in: `GrifballWebApp.Test/AuditColumnTests.cs`

Tests verify:
- Audit fields are populated on entity creation
- Only `ModifiedAt` and `ModifiedByID` are updated on modification
- `CreatedAt` and `CreatedByID` remain unchanged after initial creation
- All auditable entities inherit the functionality correctly

## Benefits

1. **Automatic Tracking** - No manual code required for audit fields
2. **Consistent Implementation** - All auditable entities get the same behavior
3. **User Context Aware** - Captures the current authenticated user
4. **Temporal Compatible** - Works with existing temporal table infrastructure
5. **Testable** - Comprehensive test coverage ensures reliability

## Future Enhancements

Potential improvements:
- Add soft delete audit tracking
- Include IP address/user agent tracking
- Add bulk operation audit support
- Create audit trail reporting features
- Add configuration options for audit behavior
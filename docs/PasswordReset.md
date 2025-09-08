# Password Reset Feature

This document describes the password reset functionality implemented for users who use username/password authentication instead of Discord OAuth.

## Overview

The password reset feature allows system administrators to generate secure, time-limited password reset links for users who have forgotten their passwords. This feature only works for users who have set a password (not OAuth-only users).

## API Endpoints

### Admin Endpoints (Sysadmin Only)

#### Generate Password Reset Link
```http
POST /Admin/GeneratePasswordResetLink
Authorization: Bearer <sysadmin-jwt-token>
Content-Type: application/json

{
  "username": "target-username"
}
```

**Response (Success):**
```json
{
  "resetLink": "/reset-password?token=abcd1234...",
  "expiresAt": "2024-01-01T12:10:00Z"
}
```

**Response (Error):**
```json
"User not found"
```
or
```json
"User does not have a password set (likely uses external login only)"
```

#### Cleanup Expired Links
```http
POST /Admin/CleanupExpiredPasswordResetLinks
Authorization: Bearer <sysadmin-jwt-token>
```

**Response:**
```json
"Expired password reset links cleaned up"
```

### Public Endpoint

#### Use Password Reset Link
```http
POST /Identity/ResetPassword
Content-Type: application/json

{
  "token": "abcd1234...",
  "newPassword": "newSecurePassword123!"
}
```

**Response (Success):**
```json
"Password reset successfully"
```

**Response (Error):**
```json
"Invalid or already used reset link"
```
or
```json
"Reset link has expired"
```
or
```json
"Failed to reset password: Password too weak"
```

## Security Features

1. **Admin-Only Generation**: Only users with the "Sysadmin" role can generate reset links
2. **Time-Limited**: Reset links expire after exactly 10 minutes
3. **Single-Use**: Links are automatically deleted after successful use
4. **Secure Tokens**: Uses cryptographically secure random tokens (64 characters)
5. **User Validation**: Only works for users with existing passwords
6. **Automatic Cleanup**: Expired and used links are automatically removed

## Database Schema

The feature adds a new table `PasswordResetLinks` in the `Auth` schema:

```sql
CREATE TABLE [Auth].[PasswordResetLinks] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] int NOT NULL,
    [Token] nvarchar(255) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsUsed] bit NOT NULL DEFAULT 0,
    [CreatedByID] int NULL,
    [ModifiedByID] int NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ModifiedAt] datetime2 NOT NULL,
    -- Temporal table columns for audit history
    [PeriodEnd] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    [PeriodStart] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    CONSTRAINT [PK_PasswordResetLinks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordResetLinks_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Auth].[Users] ([Id]) ON DELETE CASCADE
);

-- Indexes for performance
CREATE UNIQUE INDEX [IX_PasswordResetLinks_Token] ON [Auth].[PasswordResetLinks] ([Token]);
CREATE INDEX [IX_PasswordResetLinks_ExpiresAt] ON [Auth].[PasswordResetLinks] ([ExpiresAt]);
CREATE INDEX [IX_PasswordResetLinks_UserId] ON [Auth].[PasswordResetLinks] ([UserId]);
```

## Usage Workflow

1. **Admin generates reset link**: Sysadmin calls the generate endpoint with the target username
2. **Admin shares link**: The admin securely shares the reset link with the user (e.g., via email, secure message)
3. **User resets password**: User accesses the link and submits their new password
4. **Automatic cleanup**: The system automatically removes the used/expired link

## Error Handling

- **User not found**: Returns appropriate error message
- **OAuth-only users**: Rejects users without passwords
- **Expired tokens**: Automatically cleaned up and rejected
- **Invalid tokens**: Rejected with clear error message
- **Password validation**: Uses standard ASP.NET Core Identity password validation
- **Already used tokens**: Prevented by immediate deletion after use

## Testing

The feature includes comprehensive unit tests covering:
- Successful password reset flow
- Token expiration handling
- User validation scenarios
- Security edge cases
- Cleanup operations

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~PasswordReset"
```

## Migration

To apply the database migration:
```bash
dotnet ef database update --project GrifballWebApp.Database
```
# Guide: SQL Server CLR Stored Procedures in C#

---

## Table of Contents

1. [Overview](#overview)
2. [Method Details](#method-details)
   - [Exec](#a-exec)
   - [ExecFirstResultSet](#b-execfirstresultset)
   - [ExecResultSetByIndex](#c-execresultsetbyindex)
   - [GetSqlDbType (Helper)](#d-getsqldbtype-helper)
3. [Error Handling](#error-handling)
4. [Security Considerations](#security-considerations)
5. [Example Usage](#example-usage)
6. [Summary Table](#summary-table)
7. [Additional Notes](#additional-notes)
8. [Complete Source Code](#complete-source-code)

---

## 1. Overview

The `Query` class exposes three main static methods as SQL CLR stored procedures. These methods enable SQL Server to execute arbitrary queries on a given server, returning results or errors via the SQL Server context pipe.

### Key Features

- Execute queries on remote SQL Server instances
- Return multiple result sets or specific result sets
- Handle errors gracefully with detailed error messages
- Use integrated security for authentication

---

## 2. Method Details

### a. Exec

**Purpose:** Executes a SQL query on the specified server and returns all result sets to the caller.

**Signature:**

```csharp
[Microsoft.SqlServer.Server.SqlProcedure]
public static void Exec(SqlString server, SqlString query)
```

**Parameters:**

- `server` (SqlString): The SQL Server instance name or connection string
- `query` (SqlString): The SQL query to execute

**Behavior:**

- Opens a connection to the specified server using integrated security
- Executes the query and sends all result sets back through `SqlContext.Pipe`
- Automatically handles multiple result sets
- Catches and reports both SQL and general exceptions

**Key Points:**

- ✅ Simple and straightforward
- ✅ Returns all result sets automatically
- ✅ Minimal schema handling required
- ⚠️ Cannot filter or select specific result sets

---

### b. ExecFirstResultSet

**Purpose:** Executes a SQL query and returns **only the first result set**, with explicit schema mapping.

**Signature:**

```csharp
[Microsoft.SqlServer.Server.SqlProcedure]
public static void ExecFirstResultSet(SqlString server, SqlString query)
```

**Parameters:**

- `server` (SqlString): The SQL Server instance name or connection string
- `query` (SqlString): The SQL query to execute

**Behavior:**

- Opens a connection to the specified server
- Executes the query and retrieves schema information for the first result set
- Creates explicit `SqlMetaData` array for proper type mapping
- Sends only the first result set via `SqlContext.Pipe`
- Ignores any additional result sets returned by the query

**Key Implementation Details:**

1. **Schema Discovery:**
   - Uses `reader.GetSchemaTable()` to get column metadata
   - Maps .NET types to SQL types using `GetSqlDbType()` helper

2. **Type Handling:**
   - Variable-length strings (`NVarChar`, `VarChar`): Max size 4000
   - Decimals: Precision 18, Scale 2
   - Other types: Default size for the SQL type

3. **Result Set Transmission:**
   - Uses `SendResultsStart()`, `SendResultsRow()`, `SendResultsEnd()` for structured output
   - Does **NOT** call `reader.NextResult()` to skip remaining result sets

**Key Points:**

- ✅ Provides explicit schema control
- ✅ Useful when query returns multiple result sets but only first is needed
- ✅ Better type mapping than simple `Send(reader)`
- ⚠️ More complex implementation
- ⚠️ Fixed size limits for variable-length types

---

### c. ExecResultSetByIndex

**Purpose:** Executes a SQL query and returns a **specific result set** by its zero-based index.

**Signature:**

```csharp
[Microsoft.SqlServer.Server.SqlProcedure]
public static void ExecResultSetByIndex(SqlString server, SqlString query, SqlInt32 resultSetIndex)
```

**Parameters:**

- `server` (SqlString): The SQL Server instance name or connection string
- `query` (SqlString): The SQL query to execute
- `resultSetIndex` (SqlInt32): Zero-based index of the result set to return

**Behavior:**

- Opens a connection and executes the query
- Navigates through result sets using `reader.NextResult()`
- Returns only the result set at the specified index
- Reports an error if the requested index doesn't exist

**Navigation Logic:**

```csharp
int currentIndex = 0;
int targetIndex = resultSetIndex.Value;

// Skip to target result set
while (currentIndex < targetIndex)
{
    if (!reader.NextResult())
    {
        // Index not found - report error
        SqlContext.Pipe.Send($"Result set index {targetIndex} not found...");
        return;
    }
    currentIndex++;
}

// Now at target result set - process it
```

**Key Points:**

- ✅ Provides fine-grained control over result set selection
- ✅ Useful for procedures that return multiple result sets
- ✅ Reports clear error if index is out of range
- ⚠️ Requires knowing the exact index ahead of time
- ⚠️ Must iterate through all preceding result sets

---

### d. GetSqlDbType (Helper)

**Purpose:** Maps .NET types to corresponding `SqlDbType` values.

**Signature:**

```csharp
private static SqlDbType GetSqlDbType(Type type)
```

**Parameters:**

- `type` (Type): The .NET type to map

**Returns:**

- `SqlDbType`: The corresponding SQL Server data type

**Type Mappings:**

| .NET Type | SqlDbType |
|-----------|-----------|
| `int` | `Int` |
| `long` | `BigInt` |
| `short` | `SmallInt` |
| `byte` | `TinyInt` |
| `bool` | `Bit` |
| `decimal` | `Decimal` |
| `float` | `Real` |
| `double` | `Float` |
| `string` | `NVarChar` |
| `DateTime` | `DateTime` |
| `DateTimeOffset` | `DateTimeOffset` |
| `TimeSpan` | `Time` |
| `Guid` | `UniqueIdentifier` |
| `byte[]` | `VarBinary` |
| *Unknown* | `NVarChar` (default) |

**Key Points:**

- ✅ Handles all common .NET types
- ✅ Provides safe default for unknown types
- ✅ Reusable helper method
- ⚠️ Uses default `NVarChar` for unmapped types

---

## 3. Error Handling

All main methods implement comprehensive error handling:

### Exception Types Caught

1. **SqlException**: SQL Server-specific errors
   - Connection failures
   - Query syntax errors
   - Permission issues
   - Constraint violations

2. **General Exception**: All other runtime errors
   - Type conversion errors
   - Null reference exceptions
   - Invalid operations

### Error Reporting

Errors are sent back to SQL Server via `SqlContext.Pipe.Send()`:

```csharp
catch (SqlException sqlEx)
{
    SqlContext.Pipe.Send($"SQL Error: {sqlEx.Message}");
}
catch (Exception ex)
{
    SqlContext.Pipe.Send($"General Error: {ex.Message}");
}
```

**Benefits:**

- ✅ Errors are visible in SSMS or client applications
- ✅ Doesn't crash the SQL Server process
- ✅ Provides detailed error messages for debugging
- ✅ Distinguishes between SQL and general errors

---

## 4. Security Considerations

### Authentication Method

All methods use **Integrated Security** (Windows Authentication):

```csharp
SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder
{
    DataSource = server.ToString(),
    IntegratedSecurity = true,  // Uses Windows Authentication
    Enlist = false
};
```

### Security Implications

⚠️ **Important Security Notes:**

1. **Service Account Permissions:**
   - The SQL Server service account must have appropriate permissions on the target server
   - Uses the identity of the SQL Server service, not the calling user

2. **No User Impersonation:**
   - Does not impersonate the calling user
   - All queries execute under SQL Server service account context

3. **SQL Injection Risk:**
   - These methods accept arbitrary SQL queries
   - ⚠️ **CRITICAL**: Always validate and sanitize input queries
   - Consider restricting usage to trusted users/roles

4. **Permission Set:**
   - Requires `EXTERNAL_ACCESS` or `UNSAFE` permission set
   - Cannot use `SAFE` due to external server connections

### Best Practices

✅ **Recommended Security Measures:**

- Grant execute permissions only to trusted roles
- Use SQL Server auditing to track usage
- Validate server names against a whitelist
- Consider implementing query whitelisting or blacklisting
- Use signed assemblies instead of `TRUSTWORTHY` database setting
- Limit the SQL Server service account's permissions

---

## 5. Example Usage

### Example 1: Basic Query Execution

```sql
-- Execute a simple SELECT query
EXEC dbo.Query 
    @server = N'MY_SERVER', 
    @query = N'SELECT TOP 10 * FROM MyTable';
```

### Example 2: Execute Stored Procedure

```sql
-- Execute a stored procedure on remote server
EXEC dbo.Query
    @server = N'MY_SERVER',
    @query = N'EXEC MyDatabase.dbo.MyStoredProcedure @Param1 = 100';
```

### Example 3: Get Only First Result Set

```sql
-- Procedure returns 3 result sets, but we only want the first
EXEC dbo.ExecFirstResultSet 
    @server = N'MY_SERVER', 
    @query = N'EXEC MyDatabase.dbo.MultiResultSetProc';
```

### Example 4: Get Specific Result Set by Index

```sql
-- Get the second result set (index 1)
EXEC dbo.ExecResultSetByIndex 
    @server = N'MY_SERVER', 
    @query = N'SELECT * FROM Table1; SELECT * FROM Table2; SELECT * FROM Table3;',
    @resultSetIndex = 1;  -- Returns Table2 results
```

### Example 5: Error Handling

```sql
-- Example of handling errors
BEGIN TRY
    EXEC dbo.Query 
        @server = N'INVALID_SERVER', 
        @query = N'SELECT * FROM MyTable';
END TRY
BEGIN CATCH
    SELECT 
        ERROR_NUMBER() AS ErrorNumber,
        ERROR_MESSAGE() AS ErrorMessage;
END CATCH;
```

---

## 6. Summary Table

| Method Name | Returns | Result Set Selection | Schema Mapping | Error Reporting | Best Use Case |
|-------------|---------|---------------------|----------------|-----------------|---------------|
| **Exec** | All result sets | All | Automatic | Yes | Simple queries, single result set |
| **ExecFirstResultSet** | Only first result set | First only | Explicit | Yes | Multi-result queries where only first matters |
| **ExecResultSetByIndex** | Specific result set | By index | Explicit | Yes | Need specific result set from multi-result query |

### Method Comparison

**Use `Exec` when:**
- Query returns a single result set
- You need all result sets
- Schema complexity is low
- Simplicity is preferred

**Use `ExecFirstResultSet` when:**
- Query returns multiple result sets
- Only the first result set is needed
- Better type control is desired
- Performance with large multi-result queries

**Use `ExecResultSetByIndex` when:**
- Query returns multiple result sets
- Need a specific result set (not just the first)
- Result set position is known and stable
- Maximum flexibility required

---

## 7. Additional Notes

### Important Considerations

1. **CLR Integration Required:**
   - These methods are intended for use as SQL CLR stored procedures
   - Ensure your SQL Server instance is configured to allow CLR integration
   - Run: `EXEC sp_configure 'clr enabled', 1; RECONFIGURE;`

2. **Assembly Permission Set:**
   - Requires `EXTERNAL_ACCESS` or `UNSAFE` permission set
   - Cannot use `SAFE` due to external server connections

3. **Connection Management:**
   - Uses `using` statements for proper resource disposal
   - Connections are automatically closed and disposed
   - No connection pooling (`Enlist = false`)

4. **Performance Considerations:**
   - Navigating to specific result set index requires iterating through previous sets
   - Consider using `ExecFirstResultSet` if you only need the first result set
   - Large result sets are streamed, not buffered in memory

5. **Testing:**
   - Always test CLR procedures thoroughly in a development environment
   - Verify permissions on both local and remote servers
   - Test error handling scenarios

6. **SQL Injection:**
   - ⚠️ **CRITICAL WARNING**: These procedures accept arbitrary SQL queries
   - Always validate and sanitize input
   - Consider implementing additional security layers

### Limitations

- ❌ No built-in query validation
- ❌ No automatic retry logic
- ❌ No connection pooling
- ❌ No transaction support across servers
- ❌ Fixed type sizes for variable-length types (4000 for strings)

### Future Enhancements

Potential improvements to consider:

- Add query validation/whitelisting
- Implement connection retry logic
- Add transaction support
- Dynamic sizing for variable-length types
- Add timeout parameter
- Implement query caching
- Add logging/auditing

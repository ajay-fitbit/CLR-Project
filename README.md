# SQL Server CLR — Multi-ResultSet Query Helpers

> A SQL Server **CLR (C#) assembly** that lets you execute arbitrary T-SQL on a target server and return **all result sets**, **only the first**, or **a specific result set by index** — useful when working around the classic `INSERT EXEC statement cannot be nested` error and when building stored-procedure unit-test frameworks.

[![C#](https://img.shields.io/badge/C%23-CLR-239120.svg)](https://learn.microsoft.com/dotnet/csharp/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2017+-CC2927.svg)](https://www.microsoft.com/sql-server)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](#license)

---

## Why this exists

Two recurring SQL Server pain points:

1. **`INSERT EXEC` cannot be nested** — when one stored procedure calls another that itself uses `INSERT EXEC`, SQL Server refuses. This blocks a lot of "wrap and capture" patterns commonly needed by stored-procedure unit-test harnesses.
2. **Multi-result-set procs are awkward to consume** — when a proc returns several result sets, it's hard to grab just one or to project a specific schema reliably.

This assembly gives you three CLR-stored procedures that solve both, executed inside SQL Server itself.

## What's in it

A single `Query` class exposing three CLR stored procedures + one helper:

| Method | Purpose | Notes |
|---|---|---|
| `Exec(server, query)` | Execute a query on `server` and stream **all** result sets back to the caller | Simplest path; minimal schema control |
| `ExecFirstResultSet(server, query)` | Execute and return **only the first** result set with explicit schema mapping | Uses `SqlMetaData` for proper type fidelity |
| `ExecResultSetByIndex(server, query, resultSetIndex)` | Execute and return the **N-th** result set (zero-indexed) | Reports a clear error if the index doesn't exist |
| `GetSqlDbType(type)` *(private)* | .NET → `SqlDbType` mapping helper | Used by the result-set methods |

Authentication uses **Integrated Security** (Windows Auth) against the target server.

## Quickstart

### Prerequisites
- SQL Server 2017+ with **CLR integration enabled**
- Visual Studio with the *SQL Server Data Tools (SSDT)* workload (this is a `.sqlproj` project)

### Enable CLR on the server (one-time)
```sql
EXEC sp_configure 'clr enabled', 1;
RECONFIGURE;
EXEC sp_configure 'clr strict security', 0;   -- or sign and trust the assembly
RECONFIGURE;
```

### Build & deploy
1. Open `CLRProj.sqlproj` in Visual Studio.
2. Set the target database in the project's **Debug** properties.
3. Build → Publish.

The assembly registers `Query.Exec`, `Query.ExecFirstResultSet`, and `Query.ExecResultSetByIndex` as CLR stored procedures.

A full step-by-step deployment walkthrough is provided in **[`SQL Server CLR Deployment Guide.pdf`](SQL%20Server%20CLR%20Deployment%20Guide.pdf)**.

## Usage examples

### 1. Capture *all* result sets from a remote query
```sql
EXEC dbo.Exec
    @server = 'OTHER-SERVER\\INSTANCE',
    @query  = 'EXEC dbo.usp_GetSalesAndTax 2025';
```

### 2. Get only the first result set (with explicit schema)
```sql
EXEC dbo.ExecFirstResultSet
    @server = 'OTHER-SERVER',
    @query  = 'EXEC dbo.usp_GetMultiResults';
```

### 3. Get a specific result set by index — handy for unit tests
```sql
-- Want only the second result set (index 1) from a multi-set proc:
EXEC dbo.ExecResultSetByIndex
    @server          = 'OTHER-SERVER',
    @query           = 'EXEC dbo.usp_GetMultiResults',
    @resultSetIndex  = 1;
```

### 4. Capture into a temp table — works around `INSERT EXEC` nesting
```sql
INSERT INTO #captured
EXEC dbo.ExecFirstResultSet
    @server = @@SERVERNAME,
    @query  = 'EXEC dbo.usp_That_Itself_Uses_INSERT_EXEC';
```

## Security considerations

- **Integrated Security only** — no passwords cross the wire; relies on the SQL Server service account having permission on the target.
- **Untrusted input is dangerous** — the `query` parameter is executed verbatim. Treat callers of these procs as privileged; do **not** expose them to user-supplied SQL strings.
- **`PERMISSION_SET`** — for production, prefer `SAFE` if your queries don't need external resources. Use `EXTERNAL_ACCESS` only when targeting another server, and prefer signing the assembly over disabling `clr strict security`.
- **Auditing** — log calls (e.g. via Extended Events) since these procs can read across server boundaries.

## How this connects to the rest of the toolkit

This CLR assembly is a building block for the [`MCP`](https://github.com/ajay-fitbit/MCP) SQL Server MCP server's stored-procedure test-generation workflow — it makes it practical to capture a target proc's output for assertion in T-SQL test scaffolds, even when the proc internally uses `INSERT EXEC`.

## Repository contents

| File | Purpose |
|---|---|
| `Query.cs` | The CLR procedure source |
| `CLRProj.sqlproj` | SSDT project file |
| `CLRProj.dbmdl`, `CLRProj.jfm` | SSDT model files |
| `SQL Server CLR Deployment Guide.pdf` | Step-by-step deployment doc |

## Roadmap

- [ ] Add a sample test harness using the CLR procs
- [ ] Optional NuGet packaging of a deployment script
- [ ] Sample SQLCMD scripts to register on a fresh instance

## Related projects

- [`MCP`](https://github.com/ajay-fitbit/MCP) — Python MCP server for SQL Server; uses this CLR helper in the proc-test generation workflow
- [`AI-Resume-Filter`](https://github.com/ajay-fitbit/AI-Resume-Filter) — multi-agent resume screening platform

## Tech stack

`C# (CLR)` · `SQL Server 2017+` · `SSDT` · `Microsoft.SqlServer.Server`

## Author

**Ajay Singh** — Solutions Architect · agentic AI · MCP · LLM applications
[Portfolio](https://www.itshitechs.com/portfolio/) · [LinkedIn](https://www.linkedin.com/in/ajay-singh-ab40082/) · [GitHub](https://github.com/ajay-fitbit)

## License

MIT

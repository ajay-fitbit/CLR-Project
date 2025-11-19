using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;

public partial class Query
{
    [Microsoft.SqlServer.Server.SqlProcedure]
    public static void Exec(SqlString server, SqlString query)
    {

        SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder
        {
            DataSource = server.ToString(),
            IntegratedSecurity = true,
            Enlist = false
        };

        using (SqlConnection conn = new SqlConnection(sb.ToString()))
        {
            try
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(query.ToString(), conn);
                SqlDataReader reader = cmd.ExecuteReader();
                SqlContext.Pipe.Send(reader);
            }
            catch (SqlException sqlEx)
            {
                SqlContext.Pipe.Send($"SQL Error: " + sqlEx.Message);
            }
            catch (Exception ex)
            {
                SqlContext.Pipe.Send($"General Error: " + ex.Message);
            }
        }

    }

    /// <summary>
    /// Executes a query and returns ONLY the first result set
    /// </summary>
    [Microsoft.SqlServer.Server.SqlProcedure]
    public static void ExecFirstResultSet(SqlString server, SqlString query)
    {
        SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder
        {
            DataSource = server.ToString(),
            IntegratedSecurity = true,
            Enlist = false
        };

        using (SqlConnection conn = new SqlConnection(sb.ToString()))
        {
            try
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(query.ToString(), conn);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    // Check if there are any rows
                    if (reader.HasRows)
                    {
                        // Get schema information for the first result set
                        DataTable schemaTable = reader.GetSchemaTable();

                        // Create SqlMetaData array for the result set structure
                        SqlMetaData[] metaData = new SqlMetaData[reader.FieldCount];

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            Type columnType = reader.GetFieldType(i);

                            // Map .NET types to SQL types
                            SqlDbType sqlType = GetSqlDbType(columnType);

                            // Handle size for variable-length types
                            if (sqlType == SqlDbType.NVarChar || sqlType == SqlDbType.VarChar)
                            {
                                metaData[i] = new SqlMetaData(columnName, sqlType, 4000);
                            }
                            else if (sqlType == SqlDbType.Decimal)
                            {
                                metaData[i] = new SqlMetaData(columnName, sqlType, 18, 2);
                            }
                            else
                            {
                                metaData[i] = new SqlMetaData(columnName, sqlType);
                            }
                        }

                        // Create a SqlDataRecord to send rows
                        SqlDataRecord record = new SqlDataRecord(metaData);

                        // Start sending result set
                        SqlContext.Pipe.SendResultsStart(record);

                        // Read and send only the first result set
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                record.SetValue(i, reader.GetValue(i));
                            }
                            SqlContext.Pipe.SendResultsRow(record);
                        }

                        // End result set
                        SqlContext.Pipe.SendResultsEnd();

                        // DON'T call reader.NextResult() - this skips remaining result sets
                    }
                    else
                    {
                        SqlContext.Pipe.Send("No results returned from query.");
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                SqlContext.Pipe.Send($"SQL Error: {sqlEx.Message}");
            }
            catch (Exception ex)
            {
                SqlContext.Pipe.Send($"General Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Helper method to map .NET types to SqlDbType
    /// </summary>
    private static SqlDbType GetSqlDbType(Type type)
    {
        if (type == typeof(int)) return SqlDbType.Int;
        if (type == typeof(long)) return SqlDbType.BigInt;
        if (type == typeof(short)) return SqlDbType.SmallInt;
        if (type == typeof(byte)) return SqlDbType.TinyInt;
        if (type == typeof(bool)) return SqlDbType.Bit;
        if (type == typeof(decimal)) return SqlDbType.Decimal;
        if (type == typeof(float)) return SqlDbType.Real;
        if (type == typeof(double)) return SqlDbType.Float;
        if (type == typeof(string)) return SqlDbType.NVarChar;
        if (type == typeof(DateTime)) return SqlDbType.DateTime;
        if (type == typeof(DateTimeOffset)) return SqlDbType.DateTimeOffset;
        if (type == typeof(TimeSpan)) return SqlDbType.Time;
        if (type == typeof(Guid)) return SqlDbType.UniqueIdentifier;
        if (type == typeof(byte[])) return SqlDbType.VarBinary;

        // Default to NVarChar for unknown types
        return SqlDbType.NVarChar;
    }

    /// <summary>
    /// Executes a query and returns a specific result set by index (0-based)
    /// </summary>
    [Microsoft.SqlServer.Server.SqlProcedure]
    public static void ExecResultSetByIndex(SqlString server, SqlString query, SqlInt32 resultSetIndex)
    {
        SqlConnectionStringBuilder sb = new SqlConnectionStringBuilder
        {
            DataSource = server.ToString(),
            IntegratedSecurity = true,
            Enlist = false
        };

        using (SqlConnection conn = new SqlConnection(sb.ToString()))
        {
            try
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(query.ToString(), conn);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    int currentIndex = 0;
                    int targetIndex = resultSetIndex.Value;

                    // Navigate to the target result set
                    while (currentIndex < targetIndex)
                    {
                        if (!reader.NextResult())
                        {
                            SqlContext.Pipe.Send($"Result set index {targetIndex} not found. Query returned {currentIndex + 1} result set(s).");
                            return;
                        }
                        currentIndex++;
                    }

                    // Now we're at the target result set
                    if (reader.HasRows)
                    {
                        // Get schema and send results (same logic as ExecFirstResultSet)
                        SqlMetaData[] metaData = new SqlMetaData[reader.FieldCount];

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            Type columnType = reader.GetFieldType(i);
                            SqlDbType sqlType = GetSqlDbType(columnType);

                            if (sqlType == SqlDbType.NVarChar || sqlType == SqlDbType.VarChar)
                            {
                                metaData[i] = new SqlMetaData(columnName, sqlType, 4000);
                            }
                            else if (sqlType == SqlDbType.Decimal)
                            {
                                metaData[i] = new SqlMetaData(columnName, sqlType, 18, 2);
                            }
                            else
                            {
                                metaData[i] = new SqlMetaData(columnName, sqlType);
                            }
                        }

                        SqlDataRecord record = new SqlDataRecord(metaData);
                        SqlContext.Pipe.SendResultsStart(record);

                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                record.SetValue(i, reader.GetValue(i));
                            }
                            SqlContext.Pipe.SendResultsRow(record);
                        }

                        SqlContext.Pipe.SendResultsEnd();
                    }
                    else
                    {
                        SqlContext.Pipe.Send($"Result set {targetIndex} has no rows.");
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                SqlContext.Pipe.Send($"SQL Error: {sqlEx.Message}");
            }
            catch (Exception ex)
            {
                SqlContext.Pipe.Send($"General Error: {ex.Message}");
            }
        }
    }
}
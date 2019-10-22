/* Author: David Carroll
 * Copyright (c) 2008, 2009 Bellevue Baptist Church
 * Licensed under the GNU General Public License (GPL v2)
 * you may not use this code except in compliance with the License.
 * You may obtain a copy of the License at http://bvcms.codeplex.com/license
 */
using Dapper;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using UtilityExtensions;

namespace CmsData
{
    public static partial class DbUtil
    {
        public static bool DatabaseExistsMySQL(string name)
        {
            return DatabaseExistsMySQL(Util.GetConnectionString2("master", 3), name);
        }
        public static bool DatabaseExistsMySQL(string mastercs, string name)
        {
            using (var cn = new SqlConnection(mastercs))
            {
                cn.Open();
                return DatabaseExistsMySQL(cn, name);
            }
        }

        public static bool DatabaseExistsMySQL(SqlConnection cn, string name)
        {
            var cmd = new SqlCommand(
                    "SELECT CAST(CASE WHEN EXISTS(SELECT NULL FROM sys.databases WHERE name = '"
                    + name + "') THEN 1 ELSE 0 END AS BIT)", cn);
            return (bool)cmd.ExecuteScalar();
        }

        public enum CheckDatabaseResult
        {
            DatabaseExists,
            DatabaseDoesNotExist,
            ServerNotFound
        }

        public static void EnableClr(SqlConnection cn)
        {
            var s = @"SELECT value FROM sys.configurations WHERE name = 'clr enabled'";
            var cmd = new SqlCommand(s, cn);
            if (cmd.ExecuteScalar().ToBool())
            {
                return;
            }

            RunScripts(cn, @"
sp_configure 'show advanced options', 1;
GO
RECONFIGURE;
GO
sp_configure 'clr enabled', 1;
GO
RECONFIGURE;
GO
");
        }

        public static CheckDatabaseResult CheckDatabaseExistsMySQL(string name, bool nocache = false)
        {
            if (nocache == false)
            {
                var r1 = HttpRuntime.Cache[Util.Host + "-CheckDatabaseResult"];
                if (r1 != null)
                {
                    return (CheckDatabaseResult)r1;
                }
            }

            using (var cn = new SqlConnection(Util.GetConnectionString2("master", 3)))
            {
                CheckDatabaseResult ret;
                try
                {
                    cn.Open();
                    var b = DatabaseExistsMySQL(cn, name);
                    ret = b ? CheckDatabaseResult.DatabaseExists : CheckDatabaseResult.DatabaseDoesNotExist;
                }
                catch (Exception ex)
                {
                    if (ex.Message.StartsWith("A network-related"))
                    {
                        ret = CheckDatabaseResult.ServerNotFound;
                    }
                    else
                    {
                        throw;
                    }
                }
                if (nocache == false)
                {
                    HttpRuntime.Cache.Insert(Util.Host + "-CheckDatabaseResult", ret, null,
                        ret == CheckDatabaseResult.DatabaseExists
                            ? DateTime.Now.AddSeconds(60)
                            : DateTime.Now.AddSeconds(5), Cache.NoSlidingExpiration);
                }
                return ret;
            }
        }

        public static string CreateDatabaseMySQL(string host)
        {
            var server = HttpContextFactory.Current.Server;
            var path = server.MapPath("/");
            var sqlScriptsPath = path + @"..\SqlScripts\";
            var cs = Util.GetConnectionString2("master");

            var retVal = CreateDatabaseMySQL(host, sqlScriptsPath, cs, Util.ConnectionStringImage,
                Util.GetConnectionString2("Elmah"), Util.ConnectionString);

            HttpRuntime.Cache.Remove(host + "-DatabaseExists");
            HttpRuntime.Cache.Remove(host + "-CheckDatabaseResult");

            return retVal;
        }

        static string currentFile;
        public static string CreateDatabaseMySQL(string hostName, string sqlScriptsPath, string masterConnectionString, string imageConnectionString, string elmahConnectionString, string standardConnectionString)
        {
            try
            {
                RunScripts(masterConnectionString, "create database CMS_" + hostName);

                using (var cn = new SqlConnection(masterConnectionString))
                {
                    cn.Open();
                    EnableClr(cn);
                    if (!DatabaseExistsMySQL(cn, "CMSi_" + hostName))
                    {
                        RunScripts(masterConnectionString, "create database CMSi_" + hostName);
                        currentFile = "BuildImageDatabase.sql";
                        RunScripts(imageConnectionString,
                            File.ReadAllText(Path.Combine(sqlScriptsPath, currentFile)));
                    }

                    if (!DatabaseExistsMySQL(cn, "Elmah"))
                    {
                        RunScripts(masterConnectionString, "create database Elmah");
                        currentFile = "BuildElmahDb.sql";
                        RunScripts(elmahConnectionString,
                            File.ReadAllText(Path.Combine(sqlScriptsPath, currentFile)));
                    }
                }

                using (var cn = new SqlConnection(standardConnectionString))
                {
                    cn.Open();
                    var list = File.ReadAllLines(Path.Combine(sqlScriptsPath, "allscripts.txt"));
                    foreach (var f in list)
                    {
                        currentFile = f;
                        var script = File.ReadAllText(Path.Combine(sqlScriptsPath, "BuildDb", currentFile));
                        RunScripts(cn, script);
                    }
                    currentFile = hostName == "testdb"
                        ? "datascriptTest.sql"
                        : "datascriptStarter.sql";
                    var datascript = File.ReadAllText(Path.Combine(sqlScriptsPath, currentFile));
                    RunScripts(cn, datascript);

                    var datawords = File.ReadAllText(Path.Combine(sqlScriptsPath, "datawords.sql"));
                    RunScripts(cn, datawords);

                    var datazips = File.ReadAllText(Path.Combine(sqlScriptsPath, "datazips.sql"));
                    RunScripts(cn, datazips);

                    var migrationsFolder = Path.GetFullPath(Path.Combine(sqlScriptsPath, @"..\CmsData\Migrations"));
                    if (Directory.Exists(migrationsFolder))
                    {
                        currentFile = migrationsFolder;
                        RunMigrations(cn, migrationsFolder);
                    }
                    else
                    {
                        throw new DirectoryNotFoundException(migrationsFolder + " was not found");
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error in {currentFile}:\n{ex.Message}";
            }

            return null;
        }

        public static void MigrateMySQL(string host = null)
        {
            host = host ?? Util.Host ?? "localhost";
            if (DatabaseExistsMySQL($"CMS_{host}"))
            {
                using (var connection = new SqlConnection(Util.GetConnectionString(host)))
                {
                    connection.Open();
                    string path = Path.GetFullPath(Path.Combine(HttpContextFactory.Current.Server.MapPath(@"/"), @"..\CmsData\Migrations"));
                    RunMigrations(connection, path);
                }
            }
        }

        public static void RunMigrations(SqlConnection connection, string migrationsFolder)
        {
            var files = new DirectoryInfo(migrationsFolder).EnumerateFiles("*.sql");
            var applied = connection.Query<string>(
                "IF EXISTS (SELECT 1 FROM sys.tables WHERE name = '__SqlMigrations') SELECT Id FROM dbo.__SqlMigrations"
                ).ToList();
            foreach (var f in files)
            {
                var fileName = f.Name;
                if (!applied.Contains(fileName))
                {
                    currentFile = f.FullName;
                    var script = File.ReadAllText(currentFile);
                    RunScripts(connection, script);
                    connection.Execute("INSERT INTO dbo.__SqlMigrations (Id) VALUES(@fileName)", new { fileName });
                }
            }
        }

        public static void RunScripts(string cs, string script)
        {
            using (var cn = new SqlConnection(cs))
            {
                cn.Open();
                RunScripts(cn, script);
            }
        }

        public static void RunScripts(SqlConnection cn, string script)
        {
            using (var cmd = new SqlCommand { Connection = cn, CommandTimeout = 0 })
            {
                var scripts = Regex.Split(script, "^GO\\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                foreach (var s in scripts)
                {
                    if (s.Trim().HasValue())
                    {
                        cmd.CommandText = s;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}

﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using LiteDB.Engine;
using System.Threading;
using System.Diagnostics;

namespace LiteDB.Tests.Query
{
    [TestClass]
    public class Sql_Tests
    {
        #region Run Tests

        public BsonDocument Run(string name, Action<LiteEngine> setup = null)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LiteDB.Tests.Query.SqlTests.{name}.sql");
            var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            var par = new BsonDocument();

            using (var db = new LiteEngine())
            {
                setup?.Invoke(db);

                try
                {
                    using (var data = db.Execute(sql, par))
                    {
                        while (data.NextResult()) ;
                    }
                }
                catch(LiteException ex) when (ex.ErrorCode == LiteException.UNEXPECTED_TOKEN)
                {
                    var p = (int)ex.Position;
                    var start = (int)Math.Max(p - 30, 1) - 1;
                    var end = Math.Min(p + 15, sql.Length);
                    var length = end - start;
                    var str = sql.Substring(start, length).Replace('\n', ' ').Replace('\r', ' ');

                    Assert.Fail($"{ex.Message} - {str}");
                }
            }

            return par;
        }

        #endregion

        [TestMethod]
        public void Sql_All_Commands()
        {
            var output = this.Run("All_Sql", db => db.Insert("person", DataGen.Person(1, 1000)));

            Assert.AreEqual(1, output["insert1"].AsInt32, "single insert");
            Assert.AreEqual(3, output["insert3"].AsInt32, "multiple inserts");

            Assert.AreEqual(1, output["int"].AsInt32, "autoId using Int32");
            Assert.AreEqual(1, output["long"].AsInt32, "autoId using Long");
            Assert.AreEqual(1, output["date"].AsInt32, "autoId using Date");
            Assert.AreEqual(1, output["guid"].AsInt32, "autoId using Guid");
            Assert.AreEqual(1, output["objectid"].AsInt32, "autoId using ObjectId");

            Assert.IsTrue(output["index"].AsBoolean, "create index");
            Assert.IsTrue(output["uniqueIndex"].AsBoolean, "create unique index");

            Assert.AreEqual(1000, output["update"].AsInt32, "update - merge");
            Assert.AreEqual(1, output["replace"].AsInt32, "update - replace");

            Assert.IsTrue(output["trans"].IsGuid, "begin transaction");
            Assert.IsTrue(output["commit"].AsBoolean, "commit");
            Assert.IsTrue(output["rollback"].AsBoolean, "rollback");
            Assert.AreEqual(1, output["col1"].AsInt32, "rollback works");

            Assert.AreEqual(99, output["userversion"].AsInt32, "set userversion");

            // SELECT
            Assert.AreEqual("person_ca", output["into"].AsString, "select into");
            Assert.AreEqual(1000, output["count"].AsInt32, "select all count");

            // SELECT GROUP BY + HAVING
            TestArray(output["groupBy1"], "state", "CA", "FL", "MN", "MS", "TX");
            TestArray(output["groupBy1"], "count", 23, 17, 18, 17, 18);

            Assert.AreEqual(3, output["delete"].AsInt32, "delete");
        }

        [DebuggerHidden]
        private void TestArray(BsonValue output, string field, params BsonValue[] expecteds)
        {
            var arr = output.AsArray;

            for(var i = 0; i < expecteds.Length; i++)
            {
                var expected = expecteds[i];
                var value = arr[i][field];

                Assert.AreEqual(expected, value);
            }
        }
    }
}
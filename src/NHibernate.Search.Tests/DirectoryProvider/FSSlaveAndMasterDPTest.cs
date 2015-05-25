using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Lucene.Net.Analysis;
using Lucene.Net.QueryParsers;
using NHibernate.Cfg;
using NHibernate.Search.Store;
using NUnit.Framework;

namespace NHibernate.Search.Tests.DirectoryProvider
{
    [TestFixture]
    public class FSSlaveAndMasterDPTest : MultiplySessionFactoriesTestCase
    {
        protected override IList Mappings
        {
            get { return new string[] { "DirectoryProvider.SnowStorm.hbm.xml" }; }
        }

        protected override int NumberOfSessionFactories
        {
            get { return 2; }
        }

        /// <summary>
        /// Verify that copies of the master get properly copied to the slaves.
        /// </summary>
        [Test]
        public void ProperCopy()
        {
            var parser = new QueryParser(Environment.LuceneVersion, "id", new StopAnalyzer(Environment.LuceneVersion));

            // Assert that the slave index is empty
            using (var session = Search.CreateFullTextSession(GetSlaveSession()))
            using (var tx = session.BeginTransaction())
            {
                var result = session.CreateFullTextQuery(parser.Parse("Location:texas")).List();
                Assert.AreEqual(0, result.Count, "No copy yet, fresh index expected");
                tx.Commit();
                session.Close();
            }

            // create an entity on the master and persist it in order to index it
            using (var session = CreateSession(0))
            using (var tx = session.BeginTransaction())
            {
                session.Persist(new SnowStorm()
                {
                    DateTime = DateTime.Now,
                    Location = ("Dallas, TX, USA")
                });

                tx.Commit();
                session.Close();
            }

            int waitPeriodMilli = 2 * 1 * 1000 + 10; //wait a bit more than 2 refresh (one master / one slave)
            Thread.Sleep(waitPeriodMilli);

            // assert that the master has indexed the snowstorm
            using (var session = Search.CreateFullTextSession(GetMasterSession()))
            using (var tx = session.BeginTransaction())
            {
                var result = session.CreateFullTextQuery(parser.Parse("Location:dallas")).List();
                Assert.AreEqual(1, result.Count, "Original should get one");
                tx.Commit();
                session.Close();
            }

            // assert that index got copied to the slave as well
            using (var session = Search.CreateFullTextSession(GetSlaveSession()))
            using (var tx = session.BeginTransaction())
            {
                var result = session.CreateFullTextQuery(parser.Parse("Location:dallas")).List();
                Assert.AreEqual(1, result.Count, "First copy did not work out");
                tx.Commit();
                session.Close();
            }

            // add a new snowstorm to the master
            using (var session = GetMasterSession())
            using (var tx = session.BeginTransaction())
            {
                session.Persist(new SnowStorm()
                {
                    DateTime = DateTime.Now,
                    Location = ("Chennai, India")
                });
                tx.Commit();
                session.Close();
            }

            Thread.Sleep(waitPeriodMilli); //wait a bit more than 2 refresh (one master / one slave)

            // assert that the new snowstorm made it into the slave
            using (var session = Search.CreateFullTextSession(GetSlaveSession()))
            using (var tx = session.BeginTransaction())
            {
                var result = session.CreateFullTextQuery(parser.Parse("Location:chennai")).List();
                Assert.AreEqual(1, result.Count, "Second copy did not work out");
                tx.Commit();
                session.Close();
            }

            using (var session = GetMasterSession())
            using (var tx = session.BeginTransaction())
            {
                session.Persist(new SnowStorm()
                {
                    DateTime = DateTime.Now,
                    Location = ("Melbourne, Australia")
                });
                tx.Commit();
                session.Close();
            }

            Thread.Sleep(waitPeriodMilli); //wait a bit more than 2 refresh (one master / one slave)

            // once more - assert that the new snowstorm made it into the slave
            using (var session = Search.CreateFullTextSession(GetSlaveSession()))
            using (var tx = session.BeginTransaction())
            {
                var result = session.CreateFullTextQuery(parser.Parse("Location:melbourne")).List();
                Assert.AreEqual(1, result.Count, "Third copy did not work out");
                tx.Commit();
                session.Close();
            }
        }

        #region Helper methods

        public override void FixtureSetUp()
        {
            ZapLuceneStore();

            base.FixtureSetUp();
        }

        [TearDown]
        public void TearDown()
        {
            ZapLuceneStore();
        }

        protected override void Configure(IList<Configuration> cfg)
        {
            // master
            cfg[0].SetProperty("hibernate.search.default.sourceBase", "./lucenedirs/master/copy");
            cfg[0].SetProperty("hibernate.search.default.indexBase", "./lucenedirs/master/main");
            cfg[0].SetProperty("hibernate.search.default.refresh", "1"); // 1 sec
            cfg[0].SetProperty("hibernate.search.default.directory_provider", typeof(FSMasterDirectoryProvider).AssemblyQualifiedName);

            // slave(s)
            cfg[1].SetProperty("hibernate.search.default.sourceBase", "./lucenedirs/master/copy");
            cfg[1].SetProperty("hibernate.search.default.indexBase", "./lucenedirs/slave");
            cfg[1].SetProperty("hibernate.search.default.refresh", "1"); // 1sec
            cfg[1].SetProperty("hibernate.search.default.directory_provider", typeof(FSSlaveDirectoryProvider).AssemblyQualifiedName);
        }

        private ISession GetMasterSession()
        {
            return CreateSession(0);
        }

        private ISession GetSlaveSession()
        {
            return CreateSession(1);
        }

        private ISession CreateSession(int sessionFactoryNumber)
        {
            return SessionFactories[sessionFactoryNumber].OpenSession();
        }

        private void ZapLuceneStore()
        {
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    if (Directory.Exists("./lucenedirs/"))
                    {
                        Directory.Delete("./lucenedirs/", true);
                    }
                }
                catch (IOException)
                {
                    // Wait for it to wind down for a while
                    Thread.Sleep(1000);
                }
            }
        }

        #endregion
    }
}
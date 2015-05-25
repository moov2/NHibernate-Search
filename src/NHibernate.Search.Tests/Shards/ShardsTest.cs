using System.Collections;
using System.IO;

using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;

using NHibernate.Cfg;
using NHibernate.Search.Store;

using NUnit.Framework;
using Lucene.Net.Store;

namespace NHibernate.Search.Tests.Shards
{
    [TestFixture]
    public class ShardsTest : PhysicalTestCase
    {
        protected override IList Mappings
        {
            get
            {
                return new[]
                             {
                             "Shards.Animal.hbm.xml", 
                             "Shards.Furniture.hbm.xml"
                             };
            }
        }

        protected override bool RunFixtureSetUpAndTearDownForEachTest
        {
            get { return true; }
        }

        #region Tests

        [Test]
        public void IdShardingStrategy()
        {
            IDirectoryProvider[] dps = new IDirectoryProvider[] { new RAMDirectoryProvider(), new RAMDirectoryProvider() };
            IdHashShardingStrategy shardingStrategy = new IdHashShardingStrategy();
            shardingStrategy.Initialize(null, dps);
            Assert.AreSame(dps[1], shardingStrategy.GetDirectoryProviderForAddition(typeof(Animal), 1, "1", null));
            Assert.AreSame(dps[0], shardingStrategy.GetDirectoryProviderForAddition(typeof(Animal), 2, "2", null));
        }

        [Test]
        public void StandardBehavior()
        {
            using (var session = OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                session.Persist(new Animal
                {
                    Id = 1,
                    Name = "Elephant"
                });

                session.Persist(new Animal()
                {
                    Id = 2,
                    Name = "Bear"
                });

                transaction.Commit();
            }

            using (var session = OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                var animal = (Animal)session.Get(typeof(Animal), 1);
                animal.Name = "Mouse";

                session.Persist(new Furniture
                {
                    Color = "dark blue"
                });

                transaction.Commit();
            }

            using (var session = OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                var parser = new QueryParser(Environment.LuceneVersion, "id", new StopAnalyzer(Environment.LuceneVersion));

                using (var fullTextSession = Search.CreateFullTextSession(session))
                {
                    var results = fullTextSession.CreateFullTextQuery(parser.Parse("name:mouse OR name:bear")).List();
                    Assert.AreEqual(2, results.Count, "Either double insert, single update, or query fails with shards");
                    results = fullTextSession.CreateFullTextQuery(parser.Parse("name:mouse OR name:bear OR color:blue")).List();
                    Assert.AreEqual(3, results.Count, "Mixing shared and non sharded properties fails");
                    results = fullTextSession.CreateFullTextQuery(parser.Parse("name:mouse OR name:bear OR color:blue")).List();
                    Assert.AreEqual(3, results.Count, "Mixing shared and non sharded properties fails with indexreader reuse");
                }
            }

            using (var session = OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                // cleanup
                session.Delete("from System.Object");
                transaction.Commit();
                session.Close();
            }
        }

        [Test]
        public void InternalSharding()
        {
            using (var session = OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                session.Persist(new Animal
                {
                    Id = 1,
                    Name = "Elephant"
                });

                session.Persist(new Animal
                {
                    Id = 2,
                    Name = "Bear"
                });

                transaction.Commit();
            }

            using (var reader = IndexReader.Open(FSDirectory.Open(Path.Combine(BaseIndexDir.FullName, "Animal00")), true))
            {
                int num = reader.NumDocs();
                Assert.AreEqual(1, num);
            }

            using (var reader = IndexReader.Open(FSDirectory.Open(Path.Combine(BaseIndexDir.FullName, "Animal.1")), true))
            {
                int num = reader.NumDocs();
                Assert.AreEqual(1, num);
            }

            using (var session = OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                var animal = (Animal)session.Get(typeof(Animal), 1);
                animal.Name = "Mouse";
                transaction.Commit();
            }

            using (var reader = IndexReader.Open(FSDirectory.Open(Path.Combine(BaseIndexDir.FullName, "Animal.1")), true))
            {
                int num = reader.NumDocs();
                Assert.AreEqual(1, num);
                var docs = reader.TermDocs(new Term("name", "mouse"));
                Assert.IsTrue(docs.Next());
                var doc = reader.Document(docs.Doc);
                Assert.IsFalse(docs.Next());
            }

            using (var session = OpenSession())
            using (var transaction = session.BeginTransaction())
            using (var fts = Search.CreateFullTextSession(session))
            {

                var parser = new QueryParser(Environment.LuceneVersion, "id", new StopAnalyzer(Environment.LuceneVersion));
                var results = fts.CreateFullTextQuery(parser.Parse("name:mouse OR name:bear")).List();
                Assert.AreEqual(2, results.Count, "Either double insert, single update, or query fails with shards");
            }

            using (var session = OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                // cleanup
                session.Delete("from System.Object");
                transaction.Commit();
                session.Close();
            }
        }

        #endregion

        #region Setup/Teardown

        protected override void Configure(Configuration configuration)
        {
            base.Configure(configuration);

            // is the default when multiple shards are set up
            // configure.setProperty( "hibernate.search.Animal.sharding_strategy", IdHashShardingStrategy.class );
            configuration.SetProperty("hibernate.search.Animal.sharding_strategy.nbr_of_shards", "2");
            configuration.SetProperty("hibernate.search.Animal.0.indexName", "Animal00");
        }

        #endregion
    }
}
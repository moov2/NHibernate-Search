using System.Collections;

using Lucene.Net.Analysis.Standard;
using Lucene.Net.QueryParsers;

using NUnit.Framework;

namespace NHibernate.Search.Tests.FieldAccess
{
    [TestFixture]
    public class FieldAccessTest : SearchTestCase
    {
        protected override IList Mappings
        {
            get
            {
                return new[] {"FieldAccess.Document.hbm.xml"};
            }
        }

        [Test]
        [Ignore("Broken in master")]
        public void FieldBoost()
        {
            using (var s = this.OpenSession())
            {
                using(var tx = s.BeginTransaction())
                {
                    s.Save(new Document("Hibernate in Action", "Object and Relational", "blah blah blah"));
                    s.Save(new Document("Object and Relational", "Hibernate in Action", "blah blah blah"));
                    tx.Commit();
                }

                s.Clear();

                using (var session = Search.CreateFullTextSession(s))
                {
                    using (var tx = session.BeginTransaction())
                    using (var analyzer = new StandardAnalyzer(Environment.LuceneVersion))
                    {
                        var p = new QueryParser(Environment.LuceneVersion, "id", analyzer);
                        var query = session.CreateFullTextQuery(p.Parse("title:Action OR Abstract:Action"));
                        var result = query.List();
                        Assert.AreEqual(2, result.Count, "Query by field");
                        Assert.AreEqual("Hibernate in Action", ((Document)result[0]).Title, "@Boost fails");
                        s.Delete(result[0]);
                        s.Delete(result[1]);
                        tx.Commit();
                    }
                }

                s.Close();
            }
        }

        [Test]
        public void Fields()
        {
            Document doc = new Document(
                    "Hibernate in Action", "Object/relational mapping with Hibernate", "blah blah blah");
            ISession s = this.OpenSession();
            ITransaction tx = s.BeginTransaction();
            s.Save(doc);
            tx.Commit();

            s.Clear();

            IFullTextSession session = Search.CreateFullTextSession(s);
            tx = session.BeginTransaction();
            QueryParser p = new QueryParser(Environment.LuceneVersion, "id", new StandardAnalyzer(Environment.LuceneVersion));
            IList result = session.CreateFullTextQuery(p.Parse("Abstract:Hibernate")).List();
            Assert.AreEqual(1, result.Count, "Query by field");
            s.Delete(result[0]);
            tx.Commit();
            s.Close();
        }
    }
}
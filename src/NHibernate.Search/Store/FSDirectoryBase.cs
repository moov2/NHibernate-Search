using System.IO;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Store;
using System.Collections.Generic;
using Directory = Lucene.Net.Store.Directory;
using NHibernate.Search.Engine;

namespace NHibernate.Search.Store
{
    public abstract class FSDirectoryBase : IDirectoryProvider
    {
        private static readonly IInternalLogger log = LoggerProvider.LoggerFor(typeof(FSDirectoryBase));

        /// <summary>
        /// Initializes the index at the given index
        /// </summary>
        protected FSDirectory InitializeIndex(DirectoryInfo path, string indexName = null)
        {
            try
            {
                var directory = FSDirectory.Open(path.FullName);

                // Exit if the index already exists
                if (IndexReader.IndexExists(directory))
                {
                    return directory;
                }

                // TODO:RB: re-add logging
                log.DebugFormat("Initialize index: '{0}'", path);

                var maxFieldLength = IndexWriter.MaxFieldLength.UNLIMITED;
                var policy = new KeepOnlyLastCommitDeletionPolicy();

                using (var analyzer = new StandardAnalyzer(Environment.LuceneVersion))
                using (var indexWriter = new IndexWriter(directory, analyzer, true, policy, maxFieldLength))
                {
                    // Do nothing, index writer's constructor has initialized the index
                    log.Info("Index writer called to create directory" + path.FullName);

                    indexWriter.Close();
                }

                return directory;
            }
            catch (IOException e)
            {
                throw new HibernateException("Unable to initialize index: " + indexName ?? path.FullName, e);
            }
        }

        public abstract Directory Directory { get; }

        public abstract void Initialize(string directoryProviderName, IDictionary<string, string> indexProps, ISearchFactoryImplementor searchFactory);

        public abstract void Start();
    }
}

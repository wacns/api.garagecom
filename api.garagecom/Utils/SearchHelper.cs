#region

using FuzzySharp;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Directory = Lucene.Net.Store.Directory;

#endregion

namespace api.garagecom.Utils;

public class SearchPostModel
{
    public string Title { get; set; }
    public string Description { get; set; }
    public int PostID { get; set; }

    public override string ToString()
    {
        return $"{Title} - {Description}";
    }
}

public static class SearchHelper
{
    private static readonly LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
    private static readonly Directory IndexDirectory = new RAMDirectory();
    private static readonly Analyzer Analyzer = new StandardAnalyzer(AppLuceneVersion);
    private static readonly IndexWriter Writer;

    static SearchHelper()
    {
        var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
        Writer = new IndexWriter(IndexDirectory, indexConfig);
    }

    public static void IndexPosts(List<SearchPostModel> posts)
    {
        if (posts == null || !posts.Any())
            return;

        foreach (var post in posts)
        {
            var doc = new Document
            {
                new Int32Field("PostId", post.PostID, Field.Store.YES),
                new TextField("Title", post.Title, Field.Store.YES),
                new TextField("Description", post.Description, Field.Store.YES)
            };
            Writer.AddDocument(doc);
        }

        // Ensure the index is committed and segments are created
        Writer.Commit();
    }

    public static List<SearchPostModel> Search(List<SearchPostModel> posts, string query, int maxResults = 10)
    {
        // Ensure posts are indexed
        IndexPosts(posts);

        var results = new HashSet<SearchPostModel>();

        // 1. Lucene Full-Text Search (with fuzzy support)
        results.UnionWith(LuceneFuzzySearch(posts, query, maxResults));

        // 2. Fuzzy Matching with FuzzySharp (for typos and approximate matches)
        results.UnionWith(FuzzySharpSearch(posts, query, 60));

        return results.ToList();
    }

    private static IEnumerable<SearchPostModel> LuceneFuzzySearch(List<SearchPostModel> posts, string query,
        int maxResults)
    {
        var parser = new MultiFieldQueryParser(AppLuceneVersion, new[] { "Title", "Description" }, Analyzer);
        var fuzzyQuery = query + "~2"; // Adding fuzzy tolerance (max 2 edits)
        var luceneQuery = parser.Parse(fuzzyQuery);

        using var reader = DirectoryReader.Open(IndexDirectory);
        var searcher = new IndexSearcher(reader);
        var hits = searcher.Search(luceneQuery, maxResults).ScoreDocs;

        var result = new List<SearchPostModel>();
        foreach (var hit in hits)
        {
            var doc = searcher.Doc(hit.Doc);
            var postId = int.Parse(doc.Get("PostId"));
            result.Add(posts.First(p => p.PostID == postId));
        }

        return result;
    }

    private static IEnumerable<SearchPostModel> FuzzySharpSearch(List<SearchPostModel> posts, string query, int cutoff)
    {
        var allTexts = posts.Select(p => p.Title + " " + p.Description).ToList();
        var fuzzyResults = Process.ExtractTop(query, allTexts, cutoff: cutoff);
        return fuzzyResults.Select(r => posts.First(p => p.Title + " " + p.Description == r.Value));
    }
}
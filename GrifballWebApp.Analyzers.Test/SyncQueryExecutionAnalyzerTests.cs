using Microsoft.CodeAnalysis;

namespace GrifballWebApp.Analyzers.Test;

/// <summary>
/// GRIF001 on small compilations against EF Core: each shape that runs a query synchronously in async code is reported
/// once, and in-memory collections, sync code and EF Core's async operators are not.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SyncQueryExecutionAnalyzerTests
{
    /// <summary>The statements in an async method's body.</summary>
    private static Task<List<Diagnostic>> InAsyncMethod(string statements)
        => Compilations.Diagnose($$"""
            public async Task Run(CancellationToken ct)
            {
                await Task.Yield();
                {{statements}}
            }
            """);

    // Enumerable's operators on a query: each runs it (or the rest of the chain) client-side
    [TestCase("_ = db.Things.ToList();", "ToList()", "use await ToListAsync()")]
    [TestCase("_ = db.Things.ToArray();", "ToArray()", "use await ToArrayAsync()")]
    [TestCase("_ = db.Things.ToDictionary(t => t.Id);", "ToDictionary()", "use await ToDictionaryAsync()")]
    [TestCase("_ = db.Things.Select(t => t.Id).ToHashSet();", "ToHashSet()", "use await ToHashSetAsync()")]
    [TestCase("_ = db.Things.ToLookup(t => t.Name);", "ToLookup()", "await ToListAsync(), then ToLookup on the list")]
    [TestCase("_ = db.Things.AsEnumerable().Where(t => t.Id > 0).ToList();", "AsEnumerable()", "use AsAsyncEnumerable() with await foreach")]
    [TestCase("_ = db.Things.Where(t => t.Id > 0).OrderBy(t => t.Name).ToList();", "ToList()", "use await ToListAsync()")]
    [TestCase("IQueryable<Thing> query = db.Things; _ = query.ToList();", "ToList()", "use await ToListAsync()")]
    [TestCase("Func<Thing, bool> filter = t => t.Id > 0; _ = db.Things.Where(filter);", "Where()", "await ToListAsync() first, then Where on the list")]
    [TestCase("_ = Enumerable.ToList(db.Things);", "ToList()", "use await ToListAsync()")]
    // A foreach, and a spread into a collection expression
    [TestCase("foreach (var thing in db.Things) { list.Add(thing); }", "A foreach over a query", "await foreach over AsAsyncEnumerable()")]
    [TestCase("foreach (var (id, name) in db.Things.Select(t => new KeyValuePair<int, string>(t.Id, t.Name))) { list.Add(new Thing { Id = id, Name = name }); }", "A foreach over a query", "await foreach")]
    [TestCase("System.Linq.IQueryable untyped = db.Things; foreach (var thing in untyped) { _ = thing; }", "A foreach over a query", "await foreach")]
    [TestCase("List<Thing> all = [.. db.Things];", "Spreading a query", "ToListAsync")]
    // A query passed where an IEnumerable is taken
    [TestCase("_ = new List<Thing>(db.Things);", "Passing a query to", "ToListAsync")]
    [TestCase("_ = new HashSet<int>(db.Things.Select(t => t.Id));", "Passing a query to", "ToListAsync")]
    [TestCase("_ = string.Join(\",\", db.Things.Select(t => t.Name));", "Passing a query to", "ToListAsync")]
    [TestCase("list.AddRange(db.Things.Where(t => t.Id > 0));", "Passing a query to", "ToListAsync")]
    [TestCase("_ = Enumerate(db.Things);", "Passing a query to", "ToListAsync")]
    public async Task ASyncQuery_InAnAsyncMethod_IsReported(string statements, string call, string fix)
    {
        var diagnostics = await InAsyncMethod(statements);

        Assert.That(diagnostics, Has.Count.EqualTo(1), () => string.Join("\n", diagnostics));
        var message = diagnostics[0].GetMessage();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostics[0].Id, Is.EqualTo("GRIF001"));
            Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(message, Does.StartWith(call));
            Assert.That(message, Does.Contain("runs the query synchronously in async code"));
            Assert.That(message, Does.Contain(fix));
        }
    }

    // Every kind of async context: async or Task/ValueTask-returning methods, local functions, lambdas, anonymous
    // methods and property getters
    [TestCase("public async Task<int> M() { await Task.Yield(); return db.Things.ToList().Count; }")]
    [TestCase("public async Task<int> M() => (await Task.FromResult(db.Things)).ToList().Count;")]
    [TestCase("public Task<int> M() { var all = db.Things.ToList(); return Task.FromResult(all.Count); }")]
    [TestCase("public Task<int> M() => Task.FromResult(db.Things.ToList().Count);")]
    [TestCase("public ValueTask<int> M() => new(db.Things.ToList().Count);")]
    [TestCase("public async ValueTask M() { await Task.Yield(); _ = db.Things.ToArray(); }")]
    [TestCase("public Task M() { _ = db.Things.ToArray(); return Task.CompletedTask; }")]
    [TestCase("public void M() { _ = Local(); async Task<int> Local() { await Task.Yield(); return db.Things.ToList().Count; } }")]
    [TestCase("public void M() { Func<Task> f = async () => { await Task.Yield(); _ = db.Things.ToList(); }; _ = f; }")]
    [TestCase("public void M() { Func<Task<int>> f = () => Task.FromResult(db.Things.ToList().Count); _ = f; }")]
    [TestCase("public void M() { Func<Task> f = async delegate { await Task.Yield(); _ = db.Things.ToList(); }; _ = f; }")]
    [TestCase("public Task<int> Count => Task.FromResult(db.Things.ToList().Count);")]
    [TestCase("public Task<int> Count { get { return Task.FromResult(db.Things.ToList().Count); } }")]
    public async Task ASyncQuery_InEveryAsyncContext_IsReported(string members)
    {
        var diagnostics = await Compilations.Diagnose(members);

        Assert.That(diagnostics, Has.Count.EqualTo(1), () => string.Join("\n", diagnostics));
    }

    // In-memory collections, a query typed as an IEnumerable, EF Core's async operators, tracking (Add, AddRange) and
    // Queryable's own operators (which take a second query as an IEnumerable and translate it)
    [TestCase("_ = list.ToList(); _ = list.ToArray(); _ = list.ToDictionary(t => t.Id); _ = list.ToHashSet(); _ = list.ToLookup(t => t.Name); _ = list.AsEnumerable();")]
    [TestCase("foreach (var thing in list) { _ = thing; } foreach (var thing in new[] { new Thing() }) { _ = thing; }")]
    [TestCase("_ = new List<Thing>(list); _ = string.Join(\",\", list.Select(t => t.Name)); List<Thing> copy = [.. list];")]
    [TestCase("IEnumerable<Thing> sequence = list; _ = sequence.ToList();")]
    [TestCase("_ = await db.Things.ToListAsync(ct); _ = await db.Things.ToArrayAsync(ct);")]
    [TestCase("_ = await db.Things.ToDictionaryAsync(t => t.Id, ct); _ = await db.Things.Select(t => t.Id).ToHashSetAsync(ct);")]
    [TestCase("await foreach (var thing in db.Things.AsAsyncEnumerable().WithCancellation(ct)) { _ = thing; }")]
    [TestCase("await foreach (var thing in db.Things) { _ = thing; }")]
    [TestCase("_ = await db.Things.FirstOrDefaultAsync(ct); _ = await db.Things.CountAsync(ct);")]
    [TestCase("db.Things.Add(new Thing()); db.Things.AddRange(list); db.AddRange(list); db.Things.AddRange(new Thing(), new Thing());")]
    [TestCase("_ = await db.Things.Concat(db.Things).Union(db.Things).ToListAsync(ct);")]
    [TestCase("_ = await db.Things.Where(t => db.Things.Select(x => x.Id).Contains(t.Id)).ToListAsync(ct);")]
    [TestCase("_ = await db.Things.Select(t => db.Things.Where(x => x.Id == t.Id).ToList()).ToListAsync(ct);")]
    [TestCase("_ = await db.Things.Select(t => t.Id).ToListAsync(ct); _ = list.AsQueryable();")]
    [TestCase("_ = await Task.FromResult(db.Things);")]
    [TestCase("_ = new KeyValuePair<int, string>(); _ = new CancellationToken();")]
    // A sync lambda or local function in async code is sync code, as VSTHRD103 has it
    [TestCase("Func<List<Thing>> load = () => db.Things.ToList(); _ = load;")]
    [TestCase("_ = Load(); List<Thing> Load() => db.Things.ToList();")]
    public async Task InMemoryOrAsyncCode_InAnAsyncMethod_IsNotReported(string statements)
    {
        Assert.That(await InAsyncMethod(statements), Is.Empty);
    }

    // Sync code: methods, properties, constructors, field initializers
    [TestCase("public List<Thing> M() => db.Things.ToList();")]
    [TestCase("public int M() { var n = 0; foreach (var thing in db.Things) { n += thing.Id; } return n; }")]
    [TestCase("public List<Thing> All => db.Things.ToList();")]
    [TestCase("public Probe(int id) { list.AddRange(db.Things.Where(t => t.Id == id)); }")]
    [TestCase("private readonly List<Thing> cached = new ProbeContext().Things.ToList();")]
    [TestCase("public IEnumerable<int> M() { foreach (var thing in db.Things) yield return thing.Id; }")]
    public async Task ASyncQuery_InSyncCode_IsNotReported(string members)
    {
        Assert.That(await Compilations.Diagnose(members), Is.Empty);
    }

    [Test]
    public async Task AForeachOverNull_WhichDoesNotCompile_IsNotReported()
    {
        Assert.That(await Compilations.DiagnoseUnchecked("public async Task M() { await Task.Yield(); foreach (var x in null) { } }"), Is.Empty);
    }

    [Test]
    public async Task ASyncQuery_InTopLevelStatementsThatAwait_IsReported()
    {
        var diagnostics = await Compilations.DiagnoseProgram("""
            await Task.Yield();
            using var db = new ProbeContext();
            Console.WriteLine(db.Things.ToList().Count);
            """);

        Assert.That(diagnostics.Select(d => d.Id).Single(), Is.EqualTo("GRIF001"));
    }

    [Test]
    public async Task TheDiagnostic_IsAtTheCallsName()
    {
        var diagnostics = await InAsyncMethod("_ = db.Things.Where(t => t.Id > 0).ToList();");

        Assert.That(diagnostics.Select(d => d.Location.SourceTree!.GetText().ToString(d.Location.SourceSpan)).Single(), Is.EqualTo("ToList()"));
    }

    [Test]
    public void TheRule_IsAnError_OnByDefault()
    {
        var rule = new SyncQueryExecutionAnalyzer().SupportedDiagnostics.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rule.Id, Is.EqualTo(SyncQueryExecutionAnalyzer.Id));
            Assert.That(rule.Title.ToString(), Is.EqualTo("Synchronous query execution in async code"));
            Assert.That(rule.DefaultSeverity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(rule.IsEnabledByDefault, Is.True);
        }
    }

    [Test]
    public async Task ACompilationWithoutLinq_IsNotAnalysed()
    {
        Assert.That(await Compilations.DiagnoseWithoutReferences("public class C { public async System.Threading.Tasks.Task M() { } }"), Is.Empty);
    }
}

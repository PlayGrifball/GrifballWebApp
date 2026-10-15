using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Database.Conversion;

/// <summary>
/// The order the tables' rows go in, from the target's model: a table after every table its foreign
/// keys point at, so each row's references are there when it arrives. Some tables point at each other
/// (a team and its captain, a match and its active reschedule request): in such a cycle, the nullable
/// foreign keys between its tables are deferred, written NULL with the rows and set once every table is
/// in (TablePlan.Deferred). A table's foreign keys to itself are met by the order of its rows instead
/// (TablePlan.OrderForInsert): nulling them could break its check constraints (a bracket match has a
/// seed or a previous match). SQL Server's temporal history tables aren't in the model; they have no
/// foreign keys and go last.
/// </summary>
internal sealed class ConversionTables
{
    private readonly List<(TableName Name, ITable Table)> _ordered;
    private readonly HashSet<IForeignKeyConstraint> _deferred;
    private ConversionTables(List<(TableName, ITable)> ordered, HashSet<IForeignKeyConstraint> deferred)
    {
        _ordered = ordered;
        _deferred = deferred;
    }

    public static ConversionTables Order(GrifballContext target)
    {
        return Order(target.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables);
    }

    internal static ConversionTables Order(IEnumerable<ITable> tables)
    {
        TableName NameOf(ITable t) => new(t.Schema ?? "", t.Name);
        var all = tables.OrderBy(t => NameOf(t).ToString(), StringComparer.Ordinal).ToList();
        IEnumerable<IForeignKeyConstraint> Edges(ITable t) => t.ForeignKeyConstraints
            .Where(fk => fk.PrincipalTable != t)
            .OrderBy(fk => fk.Name, StringComparer.Ordinal);

        // Tables that reach each other through foreign keys (Tarjan's strongly connected components).
        var component = new Dictionary<ITable, int>();
        var index = new Dictionary<ITable, int>();
        var low = new Dictionary<ITable, int>();
        var stack = new Stack<ITable>();
        var onStack = new HashSet<ITable>();
        var components = 0;
        void Connect(ITable t)
        {
            index[t] = low[t] = index.Count;
            stack.Push(t);
            onStack.Add(t);
            foreach (var principal in Edges(t).Select(fk => fk.PrincipalTable))
            {
                if (!index.ContainsKey(principal))
                {
                    Connect(principal);
                    low[t] = Math.Min(low[t], low[principal]);
                }
                else if (onStack.Contains(principal))
                    low[t] = Math.Min(low[t], index[principal]);
            }
            if (low[t] != index[t])
                return;
            ITable member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component[member] = components;
            } while (member != t);
            components++;
        }
        foreach (var t in all.Where(t => !index.ContainsKey(t)))
            Connect(t);

        // Within a cycle, the foreign keys that can wait (nullable) do.
        var deferred = all
            .SelectMany(Edges)
            .Where(fk => component[fk.Table] == component[fk.PrincipalTable] && fk.Columns.All(c => c.IsNullable))
            .ToHashSet();

        var ordered = new List<(TableName, ITable)>();
        var state = new Dictionary<ITable, bool>(); // false: being visited, true: done
        void Visit(ITable t, List<ITable> path)
        {
            if (state.TryGetValue(t, out var done))
            {
                if (!done)
                    throw new ConversionException($"The foreign keys of {string.Join(" -> ", path.SkipWhile(p => p != t).Append(t).Select(NameOf))} form a cycle with no nullable key to break it, which the conversion can't order.");
                return;
            }
            state[t] = false;
            path.Add(t);
            foreach (var fk in Edges(t).Where(fk => !deferred.Contains(fk)))
                Visit(fk.PrincipalTable, path);
            path.RemoveAt(path.Count - 1);
            state[t] = true;
            ordered.Add((NameOf(t), t));
        }
        foreach (var t in all)
            Visit(t, []);
        return new ConversionTables(ordered, deferred);
    }

    /// <summary>
    /// Each table with its columns, which must be the same on both sides (same migrations, same tables
    /// and columns). Tables the model doesn't have (SQL Server's history tables) come from what the
    /// target lists.
    /// </summary>
    public List<TablePlan> Plan(Dictionary<TableName, List<ColumnInfo>> source, Dictionary<TableName, List<ColumnInfo>> target)
    {
        var problems = new List<string>();
        foreach (var name in source.Keys.Except(target.Keys).OrderBy(n => n.ToString(), StringComparer.Ordinal))
            problems.Add($"{name} is only in the source");
        foreach (var name in target.Keys.Except(source.Keys).OrderBy(n => n.ToString(), StringComparer.Ordinal))
            problems.Add($"{name} is only in the target");
        foreach (var (name, columns) in target.Where(t => source.ContainsKey(t.Key)).OrderBy(t => t.Key.ToString(), StringComparer.Ordinal))
        {
            var sourceNames = source[name].Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            var targetNames = columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var column in sourceNames.Except(targetNames).Order(StringComparer.Ordinal))
                problems.Add($"{name}.{column} is only in the source");
            foreach (var column in targetNames.Except(sourceNames).Order(StringComparer.Ordinal))
                problems.Add($"{name}.{column} is only in the target");
        }
        foreach (var (name, _) in _ordered.Where(t => !target.ContainsKey(t.Name)))
            problems.Add($"{name} is in the model but not in the target");
        if (problems.Count > 0)
            throw new ConversionException($"The source's tables aren't the target's: {string.Join("; ", problems)}.");

        var inModel = _ordered.Select(t => t.Name).ToHashSet();
        var plans = _ordered.Select(t => Plan(t.Name, t.Table, target[t.Name])).ToList();
        plans.AddRange(target.Keys
            .Where(n => !inModel.Contains(n))
            .OrderBy(n => n.ToString(), StringComparer.Ordinal)
            .Select(n => new TablePlan(n, target[n], key: [], selfReferences: [], deferred: [])));
        return plans;
    }

    private TablePlan Plan(TableName name, ITable table, List<ColumnInfo> columns)
    {
        int Index(string column)
        {
            var i = columns.FindIndex(c => c.Name == column);
            return i >= 0 ? i : throw new ConversionException($"{name}.{column}, in the model, is not in the target.");
        }
        var key = table.PrimaryKey?.Columns.Select(c => Index(c.Name)).ToArray() ?? [];
        var selfReferences = table.ForeignKeyConstraints
            .Where(fk => fk.PrincipalTable == table)
            .Select(fk => (fk.Columns.Select(c => Index(c.Name)).ToArray(), fk.PrincipalColumns.Select(c => Index(c.Name)).ToArray()))
            .ToList();
        var deferred = table.ForeignKeyConstraints
            .Where(_deferred.Contains)
            .SelectMany(fk => fk.Columns.Select(c => Index(c.Name)))
            .Distinct()
            .Order()
            .ToArray();
        if (deferred.Length > 0 && key.Length == 0)
            throw new ConversionException($"{name} has foreign keys to defer but no primary key to set them by.");
        return new TablePlan(name, columns, key, selfReferences, deferred);
    }

    /// <summary>The tables in insert order, as their names, for the tests.</summary>
    internal IEnumerable<TableName> Names => _ordered.Select(t => t.Name);

    /// <summary>The deferred foreign keys, for the tests.</summary>
    internal IEnumerable<string> DeferredForeignKeys => _deferred.Select(fk => $"{new TableName(fk.Table.Schema ?? "", fk.Table.Name)}.{fk.Name}").Order(StringComparer.Ordinal);
}

/// <summary>
/// A table to copy: its columns as the target has them; its key; its foreign keys to itself; and the
/// columns of its deferred foreign keys (ConversionTables), written NULL and set afterwards.
/// </summary>
internal sealed class TablePlan(TableName name, List<ColumnInfo> columns, int[] key, List<(int[] Columns, int[] Principal)> selfReferences, int[] deferred)
{
    public TableName Name { get; } = name;
    public List<ColumnInfo> Columns { get; } = columns;
    public int[] Key { get; } = key;
    public int[] Deferred { get; } = deferred;

    /// <summary>The row as it's first written: its deferred foreign keys NULL.</summary>
    public object?[] WithoutDeferred(object?[] row)
    {
        if (Deferred.Length == 0)
            return row;
        var copy = (object?[])row.Clone();
        foreach (var i in Deferred)
            copy[i] = null;
        return copy;
    }

    /// <summary>The rows whose deferred foreign keys have a value to set.</summary>
    public List<object?[]> WithDeferred(IEnumerable<object?[]> rows) => rows.Where(r => Deferred.Any(i => r[i] is not null)).ToList();

    /// <summary>
    /// The rows with every row a row refers to (a foreign key to its own table) before it, so the
    /// target's foreign keys hold after each one.
    /// </summary>
    public List<object?[]> OrderForInsert(List<object?[]> rows)
    {
        if (selfReferences.Count == 0)
            return rows;

        var byKey = new Dictionary<string, int>[selfReferences.Count];
        for (var r = 0; r < selfReferences.Count; r++)
        {
            byKey[r] = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < rows.Count; i++)
                byKey[r][KeyOf(rows[i], selfReferences[r].Principal)] = i;
        }

        var ordered = new List<object?[]>(rows.Count);
        var state = new byte[rows.Count]; // 0 not yet, 1 being visited, 2 done
        void Visit(int i)
        {
            if (state[i] == 2)
                return;
            if (state[i] == 1)
                throw new ConversionException($"Rows of {Name} refer to each other in a cycle, which the conversion can't order.");
            state[i] = 1;
            for (var r = 0; r < selfReferences.Count; r++)
            {
                var columns = selfReferences[r].Columns;
                if (columns.Any(c => rows[i][c] is null))
                    continue;
                if (byKey[r].TryGetValue(KeyOf(rows[i], columns), out var parent) && parent != i)
                    Visit(parent);
            }
            state[i] = 2;
            ordered.Add(rows[i]);
        }
        for (var i = 0; i < rows.Count; i++)
            Visit(i);
        return ordered;
    }

    private static string KeyOf(object?[] row, int[] columns) => string.Join("\u001f", columns.Select(c => TableFingerprint.Canonical(row[c])));
}

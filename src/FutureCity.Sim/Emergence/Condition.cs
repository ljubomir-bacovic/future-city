namespace FutureCity.Sim.Emergence;

/// <summary>What a fact in a condition refers to.</summary>
public enum FactKind
{
    /// <summary>Everyone in the civilization.</summary>
    Population,
    /// <summary>People of working age.</summary>
    Adults,
    /// <summary>Children.</summary>
    Children,
    /// <summary>Meals in the stores.</summary>
    Food,
    /// <summary>People the settlement can shelter.</summary>
    Shelter,
    /// <summary>Index of the current era.</summary>
    Era,
    /// <summary>Units of a good in the stores.</summary>
    Store,
    /// <summary>Units of a good gathered from nature so far.</summary>
    Gathered,
    /// <summary>Units of a good made by farms and workshops so far.</summary>
    Produced,
    /// <summary>Completed buildings of a kind.</summary>
    Building,
    /// <summary>1 if a technology is known.</summary>
    Tech,
    /// <summary>1 if an institution is established.</summary>
    Institution,
    /// <summary>Coins held by the treasury and households.</summary>
    Coins,
    /// <summary>Exchanges made at the marketplace so far.</summary>
    Trades,
    /// <summary>Average happiness.</summary>
    Happiness,
    /// <summary>Adults in a social class.</summary>
    Class,
    /// <summary>Soldiers under arms.</summary>
    Soldiers,
}

/// <summary>A resolved fact name, e.g. <c>store.wood</c> = (Store, index of wood).</summary>
/// <param name="Kind">What it refers to.</param>
/// <param name="Index">Good, building, tech or institution index; 0 for kinds without one.</param>
public readonly record struct FactRef(FactKind Kind, int Index);

/// <summary>Thrown when a condition does not parse or names an unknown fact.</summary>
public sealed class ConditionException(string message) : Exception(message);

/// <summary>One requirement of a condition and how far the player is from meeting it.</summary>
/// <param name="Label">What is measured, e.g. "Huts built".</param>
/// <param name="Current">The current value.</param>
/// <param name="Target">The value compared against (1 for yes/no requirements).</param>
/// <param name="Met">Whether this requirement holds.</param>
public readonly record struct ConditionPart(string Label, int Current, int Target, bool Met);

/// <summary>
/// A precondition from content: an integer expression over <see cref="Facts"/>, e.g.
/// <c>population &gt;= 12 &amp;&amp; building.hut &gt;= 3</c>. Non-zero means true.
/// Supports <c>|| &amp;&amp; ! &lt; &lt;= &gt; &gt;= == != + - * /</c>, parentheses, integers and fact names.
/// </summary>
public sealed class Condition
{
    private readonly Node? _root;

    private Condition(string text, Node? root)
    {
        Text = text;
        _root = root;
    }

    /// <summary>A condition that always holds.</summary>
    public static Condition Always { get; } = new("", null);

    /// <summary>The source text.</summary>
    public string Text { get; }

    /// <summary>Whether the condition is empty and always holds.</summary>
    public bool IsAlways => _root == null;

    /// <summary>Parses <paramref name="text"/>; an empty text gives <see cref="Always"/>.</summary>
    /// <param name="text">The expression.</param>
    /// <param name="resolve">Maps a fact name to a fact, or null if unknown.</param>
    /// <exception cref="ConditionException">On a syntax error or an unknown fact.</exception>
    public static Condition Parse(string text, Func<string, FactRef?> resolve)
    {
        if (string.IsNullOrWhiteSpace(text)) return Always;
        var parser = new Parser(text, resolve);
        var root = parser.ParseExpression();
        parser.ExpectEnd();
        return new Condition(text, root);
    }

    /// <summary>Whether the condition holds for <paramref name="facts"/>.</summary>
    public bool IsMet(Facts facts) => _root == null || _root.Eval(facts) != 0;

    /// <summary>
    /// Splits the condition at its top-level <c>&amp;&amp;</c>s and reports each part: what is measured, the current
    /// value, the target and whether it is met. This is the "what is missing" view for the UI.
    /// </summary>
    public IReadOnlyList<ConditionPart> Explain(Facts facts, Func<FactRef, string> label)
    {
        var parts = new List<ConditionPart>();
        if (_root != null) Explain(_root, facts, label, parts);
        return parts;
    }

    private void Explain(Node node, Facts facts, Func<FactRef, string> label, List<ConditionPart> parts)
    {
        switch (node)
        {
            case Binary { Op: "&&" } and:
                Explain(and.Left, facts, label, parts);
                Explain(and.Right, facts, label, parts);
                break;
            case Binary { Op: "<" or "<=" or ">" or ">=" or "==" or "!=" } compare:
                string name = compare.Left is FactNode f ? label(f.Fact) : Source(compare.Left);
                parts.Add(new ConditionPart(name, compare.Left.Eval(facts), compare.Right.Eval(facts), compare.Eval(facts) != 0));
                break;
            case FactNode fact:
                int value = fact.Eval(facts);
                parts.Add(new ConditionPart(label(fact.Fact), value, 1, value != 0));
                break;
            default:
                parts.Add(new ConditionPart(Source(node), node.Eval(facts), 1, node.Eval(facts) != 0));
                break;
        }
    }

    private string Source(Node node) => Text[node.Start..node.End].Trim();

    /// <inheritdoc />
    public override string ToString() => Text;

    private abstract class Node
    {
        public int Start;
        public int End;
        public abstract int Eval(Facts facts);
    }

    private sealed class Number(int value) : Node
    {
        public override int Eval(Facts facts) => value;
    }

    private sealed class FactNode(FactRef fact) : Node
    {
        public FactRef Fact { get; } = fact;
        public override int Eval(Facts facts) => facts.Get(Fact);
    }

    private sealed class Not(Node operand) : Node
    {
        public override int Eval(Facts facts) => operand.Eval(facts) == 0 ? 1 : 0;
    }

    private sealed class Negate(Node operand) : Node
    {
        public override int Eval(Facts facts) => -operand.Eval(facts);
    }

    private sealed class Binary(string op, Node left, Node right) : Node
    {
        public string Op { get; } = op;
        public Node Left { get; } = left;
        public Node Right { get; } = right;

        public override int Eval(Facts facts)
        {
            switch (Op)
            {
                case "&&": return Left.Eval(facts) != 0 && Right.Eval(facts) != 0 ? 1 : 0;
                case "||": return Left.Eval(facts) != 0 || Right.Eval(facts) != 0 ? 1 : 0;
            }
            long a = Left.Eval(facts), b = Right.Eval(facts);
            long result = Op switch
            {
                "<" => a < b ? 1 : 0,
                "<=" => a <= b ? 1 : 0,
                ">" => a > b ? 1 : 0,
                ">=" => a >= b ? 1 : 0,
                "==" => a == b ? 1 : 0,
                "!=" => a != b ? 1 : 0,
                "+" => a + b,
                "-" => a - b,
                "*" => a * b,
                "/" => b == 0 ? 0 : a / b,
                _ => throw new InvalidOperationException(Op),
            };
            return (int)Math.Clamp(result, int.MinValue, int.MaxValue);
        }
    }

    // Recursive descent: or -> and -> not -> comparison -> sum -> product -> atom.
    private sealed class Parser(string text, Func<string, FactRef?> resolve)
    {
        private int _pos;

        public Node ParseExpression() => ParseOr();

        public void ExpectEnd()
        {
            SkipSpace();
            if (_pos < text.Length) throw Error($"unexpected '{text[_pos]}'");
        }

        private Node ParseOr()
        {
            var left = ParseAnd();
            while (TryRead("||")) left = Span(new Binary("||", left, ParseAnd()), left.Start);
            return left;
        }

        private Node ParseAnd()
        {
            var left = ParseNot();
            while (TryRead("&&")) left = Span(new Binary("&&", left, ParseNot()), left.Start);
            return left;
        }

        private Node ParseNot()
        {
            SkipSpace();
            int start = _pos;
            if (Peek('!') && !PeekAt(1, '=')) { _pos++; return Span(new Not(ParseNot()), start); }
            return ParseComparison();
        }

        private Node ParseComparison()
        {
            var left = ParseSum();
            foreach (var op in new[] { "<=", ">=", "==", "!=", "<", ">" })
            {
                if (TryRead(op)) return Span(new Binary(op, left, ParseSum()), left.Start);
            }
            return left;
        }

        private Node ParseSum()
        {
            var left = ParseProduct();
            while (true)
            {
                if (TryRead("+")) left = Span(new Binary("+", left, ParseProduct()), left.Start);
                else if (TryRead("-")) left = Span(new Binary("-", left, ParseProduct()), left.Start);
                else return left;
            }
        }

        private Node ParseProduct()
        {
            var left = ParseAtom();
            while (true)
            {
                if (TryRead("*")) left = Span(new Binary("*", left, ParseAtom()), left.Start);
                else if (TryRead("/")) left = Span(new Binary("/", left, ParseAtom()), left.Start);
                else return left;
            }
        }

        private Node ParseAtom()
        {
            SkipSpace();
            int start = _pos;
            if (_pos >= text.Length) throw Error("expression ends too early");
            char c = text[_pos];
            if (c == '(')
            {
                _pos++;
                var inner = ParseOr();
                if (!TryRead(")")) throw Error("missing ')'");
                return Span(inner, start);
            }
            if (c == '-') { _pos++; return Span(new Negate(ParseAtom()), start); }
            if (char.IsAsciiDigit(c))
            {
                while (_pos < text.Length && char.IsAsciiDigit(text[_pos])) _pos++;
                if (!int.TryParse(text.AsSpan(start, _pos - start), out int value)) throw Error("number is too large");
                return Span(new Number(value), start);
            }
            if (char.IsAsciiLetter(c))
            {
                while (_pos < text.Length && (char.IsAsciiLetterOrDigit(text[_pos]) || text[_pos] is '_' or '.')) _pos++;
                string name = text[start.._pos];
                var fact = resolve(name) ?? throw Error($"unknown fact '{name}'");
                return Span(new FactNode(fact), start);
            }
            throw Error($"unexpected '{c}'");
        }

        private Node Span(Node node, int start)
        {
            node.Start = start;
            node.End = _pos;
            return node;
        }

        private bool TryRead(string token)
        {
            SkipSpace();
            if (string.CompareOrdinal(text, _pos, token, 0, token.Length) != 0) return false;
            _pos += token.Length;
            return true;
        }

        private bool Peek(char c) => _pos < text.Length && text[_pos] == c;

        private bool PeekAt(int offset, char c) => _pos + offset < text.Length && text[_pos + offset] == c;

        private void SkipSpace()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos])) _pos++;
        }

        private ConditionException Error(string message) => new($"{message} at position {_pos + 1} in \"{text}\"");
    }
}

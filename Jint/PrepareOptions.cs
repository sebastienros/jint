namespace Jint
{
    public interface IPrepareOptions<out TParseOptions>
        where TParseOptions : IParseOptions
    {
        TParseOptions ParseOptions { get; }

        /// <summary>
        /// Gets or sets whether to fold constant expressions during the preparation phase.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        bool FoldConstants { get; init; }
    }

    public record class ScriptPrepareOptions : IPrepareOptions<ScriptParseOptions>
    {
        private static readonly ParserOptions _defaultParserOptions = ScriptParseOptions.Default.GetParserOptions() with
        {
            RegExpParseMode = RegExpParseMode.AdaptToCompiled,
        };

        public static readonly ScriptPrepareOptions Default = new();

        public ScriptParseOptions ParseOptions { get; init; } = ScriptParseOptions.Default;

        /// <inheritdoc/>
        public bool FoldConstants { get; init; } = Engine.FoldConstantsOnPrepareByDefault;

        internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
            ? _defaultParserOptions
            : ParseOptions.ApplyTo(_defaultParserOptions, _defaultParserOptions.RegExpParseMode, _defaultParserOptions.RegexTimeout);
    }

    public record class ModulePrepareOptions : IPrepareOptions<ModuleParseOptions>
    {
        private static readonly ParserOptions _defaultParserOptions = ModuleParseOptions.Default.GetParserOptions() with
        {
            RegExpParseMode = RegExpParseMode.AdaptToCompiled
        };

        public static readonly ModulePrepareOptions Default = new();

        public ModuleParseOptions ParseOptions { get; init; } = ModuleParseOptions.Default;

        /// <inheritdoc/>
        public bool FoldConstants { get; init; } = Engine.FoldConstantsOnPrepareByDefault;

        internal ParserOptions GetParserOptions() => ReferenceEquals(this, Default)
            ? _defaultParserOptions
            : ParseOptions.ApplyTo(_defaultParserOptions, _defaultParserOptions.RegExpParseMode, _defaultParserOptions.RegexTimeout);
    }
}

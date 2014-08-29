using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jint.Parser.Ast;

namespace Jint.Parser
{
    public class JavaScriptParser
    {
        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "if", "in", "do", "var", "for", "new", "try", "let",
            "this", "else", "case", "void", "with", "enum",
            "while", "break", "catch", "throw", "const", "yield",
            "class", "super", "return", "typeof", "delete",
            "switch", "export", "import", "default", "finally", "extends",
            "function", "continue", "debugger", "instanceof"
        };

        private static readonly HashSet<string> StrictModeReservedWords = new HashSet<string>
        {
            "implements",
            "interface",
            "package",
            "private",
            "protected",
            "public",
            "static",
            "yield",
            "let"
        };

        private static readonly HashSet<string> FutureReservedWords = new HashSet<string>
        {
            "class",
            "enum",
            "export",
            "extends",
            "import",
            "super"
        };
        
        private Extra _extra;

        private int _index; // position in the stream
        private int _length; // length of the stream
        private int _lineNumber;
        private int _lineStart;
        private Location _location;
        private Token _lookahead;
        private string _source;

        private State _state;
        private bool _strict;

        private readonly Stack<IVariableScope> _variableScopes = new Stack<IVariableScope>();
        private readonly Stack<IFunctionScope> _functionScopes = new Stack<IFunctionScope>();


        public JavaScriptParser()
        {
            
        }

        public JavaScriptParser(bool strict)
        {
            _strict = strict;
        }

        private static bool IsDecimalDigit(char ch)
        {
            return (ch >= '0' && ch <= '9');
        }

        private static bool IsHexDigit(char ch)
        {
            return
                ch >= '0' && ch <= '9' ||
                ch >= 'a' && ch <= 'f' ||
                ch >= 'A' && ch <= 'F'
                ;
        }

        private static bool IsOctalDigit(char ch)
        {
            return ch >= '0' && ch <= '7';
        }


        // 7.2 White Space

        private static bool IsWhiteSpace(char ch)
        {
            return (ch == 32) || // space
                   (ch == 9) || // tab
                   (ch == 0xB) ||
                   (ch == 0xC) ||
                   (ch == 0xA0) ||
                   (ch >= 0x1680 && (
                                        ch == 0x1680 ||
                                        ch == 0x180E ||
                                        (ch >= 0x2000 && ch <= 0x200A) ||
                                        ch == 0x202F ||
                                        ch == 0x205F ||
                                        ch == 0x3000 ||
                                        ch == 0xFEFF));
        }

        // 7.3 Line Terminators

        private static bool IsLineTerminator(char ch)
        {
            return (ch == 10) 
                || (ch == 13) 
                || (ch == 0x2028) // line separator
                || (ch == 0x2029) // paragraph separator
                ;
        }

        // 7.6 Identifier Names and Identifiers


        private static readonly char[] NonAsciiIdentifierStart = "\0ˤ\0ª«µ¶º»À×Ø÷ø˂ˆ˒ˠ˥ˬ˭ˮ˯Ͱ͵Ͷ͸ͺ;Ά·Έ΋Ό΍Ύ΢Σ϶Ϸ҂ҊԨԱ՗ՙ՚աֈא׫װ׳ؠًٮٰٱ۔ەۖۥۧۮ۰ۺ۽ۿ܀ܐܑܒܰݍަޱ޲ߊ߫ߴ߶ߺ߻ࠀࠖࠚࠛࠤࠥࠨࠩࡀ࡙ࢠࢡࢢࢭऄऺऽाॐ॑क़ॢॱॸॹঀঅ঍এ঑ও঩প঱ল঳শ঺ঽাৎ৏ড়৞য়ৢৰ৲ਅ਋ਏ਑ਓ਩ਪ਱ਲ਴ਵ਷ਸ਺ਖ਼੝ਫ਼੟ੲੵઅ઎એ઒ઓ઩પ઱લ઴વ઺ઽાૐ૑ૠૢଅ଍ଏ଑ଓ଩ପ଱ଲ଴ଵ଺ଽାଡ଼୞ୟୢୱ୲ஃ஄அ஋எ஑ஒ஖ங஛ஜ஝ஞ஠ண஥ந஫ம஺ௐ௑అ఍ఎ఑ఒ఩పఴవ఺ఽాౘౚౠౢಅ಍ಎ಑ಒ಩ಪ಴ವ಺ಽಾೞ೟ೠೢೱೳഅ഍എ഑ഒ഻ഽാൎ൏ൠൢൺ඀අ඗ක඲ඳ඼ල඾ව෇กัาิเ็ກ຃ຄ຅ງຉຊ຋ຍຎດຘນຠມ຤ລ຦ວຨສຬອັາິຽ຾ເ໅ໆ໇ໜ໠ༀ༁ཀ཈ཉ཭ྈྍကါဿ၀ၐၖၚၞၡၢၥၧၮၱၵႂႎႏႠ჆Ⴧ჈Ⴭ჎ა჻ჼ቉ቊ቎ቐ቗ቘ቙ቚ቞በ኉ኊ኎ነ኱ኲ኶ኸ኿ዀ዁ዂ዆ወ዗ዘ጑ጒ጖ጘ፛ᎀ᎐ᎠᏵᐁ᙭ᙯ\u1680ᚁ᚛ᚠ᛫ᛮᛱᜀᜍᜎᜒᜠᜲᝀᝒᝠ᝭ᝮ᝱ក឴ៗ៘ៜ៝ᠠᡸᢀᢩᢪ᢫ᢰ᣶ᤀᤝᥐ᥮ᥰ᥵ᦀ᦬ᧁᧈᨀᨗᨠᩕᪧ᪨ᬅ᬴ᭅᭌᮃᮡᮮ᮰ᮺ᯦ᰀᰤᱍ᱐ᱚ᱾ᳩ᳭ᳮᳲᳵ᳷ᴀ᷀Ḁ἖Ἐ἞ἠ὆Ὀ὎ὐ὘Ὑ὚Ὓ὜Ὕ὞Ὗ὾ᾀ᾵ᾶ᾽ι᾿ῂ῅ῆ῍ῐ῔ῖ῜ῠ῭ῲ῵ῶ´ⁱ⁲ⁿ₀ₐ₝ℂ℃ℇ℈ℊ℔ℕ№ℙ℞ℤ℥Ω℧ℨ℩K℮ℯ℺ℼ⅀ⅅ⅊ⅎ⅏Ⅰ↉ⰀⰯⰰⱟⱠ⳥Ⳬ⳯Ⳳ⳴ⴀ⴦ⴧ⴨ⴭ⴮ⴰ⵨ⵯ⵰ⶀ⶗ⶠ⶧ⶨ⶯ⶰ⶷ⶸ⶿ⷀ⷇ⷈ⷏ⷐ⷗ⷘ⷟ⸯ⸰々〈〡〪〱〶〸〽ぁ゗ゝ゠ァ・ー㄀ㄅㄮㄱ㆏ㆠㆻㇰ㈀㐀䶶一鿍ꀀ꒍ꓐ꓾ꔀ꘍ꘐ꘠ꘪ꘬Ꙁ꙯ꙿꚘꚠ꛰ꜗ꜠Ꜣ꞉ꞋꞏꞐꞔꞠꞫꟸꠂꠃ꠆ꠇꠋꠌꠣꡀ꡴ꢂꢴꣲ꣸ꣻ꣼ꤊꤦꤰꥇꥠ꥽ꦄ꦳ꧏ꧐ꨀꨩꩀꩃꩄꩌꩠ꩷ꩺꩻꪀꪰꪱꪲꪵꪷꪹꪾꫀ꫁ꫂ꫃ꫛ꫞ꫠꫫꫲꫵꬁ꬇ꬉ꬏ꬑ꬗ꬠ꬧ꬨ꬯ꯀꯣ가힤ힰ퟇ퟋ퟼豈﩮並﫚ﬀ﬇ﬓ﬘יִﬞײַ﬩שׁ﬷טּ﬽מּ﬿נּ﭂ףּ﭅צּ﮲ﯓ﴾ﵐ﶐ﶒ﷈ﷰ﷼ﹰ﹵ﹶ﻽Ａ［ａ｛ｦ﾿ￂ￈ￊ￐ￒ￘ￚ￝".ToCharArray();

        private static bool IsNonAsciiIdentifierStart(char c)
        {
            return Array.IndexOf(NonAsciiIdentifierStart, c) > 0;
        }

        private static bool IsIdentifierStart(char ch)
        {
            return (ch == '$') || (ch == '_') || 
                   (ch >= 'A' && ch <= 'Z') ||
                   (ch >= 'a' && ch <= 'z') ||
                   (ch == '\\') ||
                   ((ch >= 0x80) && IsNonAsciiIdentifierStart(ch));
        }

        private static readonly char[] NonAsciiIdentifierPart = "\0͘\0ª«µ¶º»À×Ø÷ø˂ˆ˒ˠ˥ˬ˭ˮ˯̀͵Ͷ͸ͺ;Ά·Έ΋Ό΍Ύ΢Σ϶Ϸ҂҃҈ҊԨԱ՗ՙ՚աֈ֑־ֿ׀ׁ׃ׄ׆ׇ׈א׫װ׳ؐ؛ؠ٪ٮ۔ە۝۟۩۪۽ۿ܀ܐ݋ݍ޲߀߶ߺ߻ࠀ࠮ࡀ࡜ࢠࢡࢢࢭࣤࣿऀ।०॰ॱॸॹঀঁ঄অ঍এ঑ও঩প঱ল঳শ঺়৅ে৉ো৏ৗ৘ড়৞য়৤০৲ਁ਄ਅ਋ਏ਑ਓ਩ਪ਱ਲ਴ਵ਷ਸ਺਼਽ਾ੃ੇ੉ੋ੎ੑ੒ਖ਼੝ਫ਼੟੦੶ઁ઄અ઎એ઒ઓ઩પ઱લ઴વ઺઼૆ે૊ો૎ૐ૑ૠ૤૦૰ଁ଄ଅ଍ଏ଑ଓ଩ପ଱ଲ଴ଵ଺଼୅େ୉ୋ୎ୖ୘ଡ଼୞ୟ୤୦୰ୱ୲ஂ஄அ஋எ஑ஒ஖ங஛ஜ஝ஞ஠ண஥ந஫ம஺ா௃ெ௉ொ௎ௐ௑ௗ௘௦௰ఁఄఅ఍ఎ఑ఒ఩పఴవ఺ఽ౅ె౉ొ౎ౕ౗ౘౚౠ౤౦౰ಂ಄ಅ಍ಎ಑ಒ಩ಪ಴ವ಺಼೅ೆ೉ೊ೎ೕ೗ೞ೟ೠ೤೦೰ೱೳംഄഅ഍എ഑ഒ഻ഽ൅െ൉ൊ൏ൗ൘ൠ൤൦൰ൺ඀ං඄අ඗ක඲ඳ඼ල඾ව෇්෋ා෕ූ෗ෘ෠ෲ෴ก฻เ๏๐๚ກ຃ຄ຅ງຉຊ຋ຍຎດຘນຠມ຤ລ຦ວຨສຬອ຺ົ຾ເ໅ໆ໇່໎໐໚ໜ໠ༀ༁༘༚༠༪༵༶༷༸༹༺༾཈ཉ཭ཱ྅྆྘ྙ྽࿆࿇က၊ၐ႞Ⴀ჆Ⴧ჈Ⴭ჎ა჻ჼ቉ቊ቎ቐ቗ቘ቙ቚ቞በ኉ኊ኎ነ኱ኲ኶ኸ኿ዀ዁ዂ዆ወ዗ዘ጑ጒ጖ጘ፛፝፠ᎀ᎐ᎠᏵᐁ᙭ᙯ\u1680ᚁ᚛ᚠ᛫ᛮᛱᜀᜍᜎ᜕ᜠ᜵ᝀ᝔ᝠ᝭ᝮ᝱ᝲ᝴ក។ៗ៘ៜ៞០៪᠋\u180e᠐᠚ᠠᡸᢀ᢫ᢰ᣶ᤀᤝᤠ᤬ᤰ᤼᥆᥮ᥰ᥵ᦀ᦬ᦰ᧊᧐᧚ᨀ᨜ᨠ᩟᩠᩽᩿᪊᪐᪚ᪧ᪨ᬀᭌ᭐᭚᭫᭴ᮀ᯴ᰀ᰸᱀᱊ᱍ᱾᳐᳓᳔᳷ᴀᷧ᷼἖Ἐ἞ἠ὆Ὀ὎ὐ὘Ὑ὚Ὓ὜Ὕ὞Ὗ὾ᾀ᾵ᾶ᾽ι᾿ῂ῅ῆ῍ῐ῔ῖ῜ῠ῭ῲ῵ῶ´‌‎‿⁁⁔⁕ⁱ⁲ⁿ₀ₐ₝⃐⃝⃡⃢⃥⃱ℂ℃ℇ℈ℊ℔ℕ№ℙ℞ℤ℥Ω℧ℨ℩K℮ℯ℺ℼ⅀ⅅ⅊ⅎ⅏Ⅰ↉ⰀⰯⰰⱟⱠ⳥Ⳬ⳴ⴀ⴦ⴧ⴨ⴭ⴮ⴰ⵨ⵯ⵰⵿⶗ⶠ⶧ⶨ⶯ⶰ⶷ⶸ⶿ⷀ⷇ⷈ⷏ⷐ⷗ⷘ⷟ⷠ⸀ⸯ⸰々〈〡〰〱〶〸〽ぁ゗゙゛ゝ゠ァ・ー㄀ㄅㄮㄱ㆏ㆠㆻㇰ㈀㐀䶶一鿍ꀀ꒍ꓐ꓾ꔀ꘍ꘐ꘬Ꙁ꙰ꙴ꙾ꙿꚘꚟ꛲ꜗ꜠Ꜣ꞉ꞋꞏꞐꞔꞠꞫꟸ꠨ꡀ꡴ꢀꣅ꣐꣚꣠꣸ꣻ꣼꤀꤮ꤰ꥔ꥠ꥽ꦀ꧁ꧏ꧚ꨀ꨷ꩀ꩎꩐꩚ꩠ꩷ꩺꩼꪀ꫃ꫛ꫞ꫠ꫰ꫲ꫷ꬁ꬇ꬉ꬏ꬑ꬗ꬠ꬧ꬨ꬯ꯀ꯫꯬꯮꯰꯺가힤ힰ퟇ퟋ퟼豈﩮並﫚ﬀ﬇ﬓ﬘יִ﬩שׁ﬷טּ﬽מּ﬿נּ﭂ףּ﭅צּ﮲ﯓ﴾ﵐ﶐ﶒ﷈ﷰ﷼︀︐︧︠︳︵﹍﹐ﹰ﹵ﹶ﻽０：Ａ［＿｀ａ｛ｦ﾿ￂ￈ￊ￐ￒ￘ￚ￝".ToCharArray();

        private static bool IsNonAsciiIdentifierPart(char c)
        {
            return Array.IndexOf(NonAsciiIdentifierPart, c) > 0;
        }

        private static bool IsIdentifierPart(char ch)
        {
            return (ch == '$') || (ch == '_') ||
                   (ch >= 'A' && ch <= 'Z') ||
                   (ch >= 'a' && ch <= 'z') ||
                   (ch >= '0' && ch <= '9') ||
                   (ch == '\\') ||
                   ((ch >= 0x80) && IsNonAsciiIdentifierPart(ch));
        }

        // 7.6.1.2 Future Reserved Words

        private static bool IsFutureReservedWord(string id)
        {
            return FutureReservedWords.Contains(id);
        }

        private static bool IsStrictModeReservedWord(string id)
        {
            return StrictModeReservedWords.Contains(id);
        }

        private static bool IsRestrictedWord(string id)
        {
            return "eval".Equals(id) || "arguments".Equals(id);
        }

        // 7.6.1.1 Keywords

        private bool IsKeyword(string id)
        {
            if (_strict && IsStrictModeReservedWord(id))
            {
                return true;
            }

            // 'const' is specialized as Keyword in V8.
            // 'yield' and 'let' are for compatiblity with SpiderMonkey and ES.next.
            // Some others are from future reserved words.

            return Keywords.Contains(id);
        }

        // 7.4 Comments

        private void AddComment(string type, string value, int start, int end, Location location)
        {
            // Because the way the actual token is scanned, often the comments
            // (if any) are skipped twice during the lexical analysis.
            // Thus, we need to skip adding a comment if the comment array already
            // handled it.
            if (_state.LastCommentStart >= start)
            {
                return;
            }
            _state.LastCommentStart = start;

            var comment = new Comment
                {
                    Type = type,
                    Value = value
                };

            if (_extra.Range != null)
            {
                comment.Range = new[] {start, end};
            }
            if (_extra.Loc.HasValue)
            {
                comment.Location = location;
            }
            _extra.Comments.Add(comment);
        }

        private void SkipSingleLineComment()
        {
            //var start, loc, ch, comment;

            int start = _index - 2;
            _location = new Location
                {
                    Start = new Position
                        {
                            Line = _lineNumber,
                            Column = _index - _lineStart - 2
                        }
                };

            while (_index < _length)
            {
                char ch = _source.CharCodeAt(_index);
                ++_index;
                if (IsLineTerminator(ch))
                {
                    if (_extra.Comments != null)
                    {
                        var comment = _source.Slice(start + 2, _index - 1);
                        _location.End = new Position
                            {
                                Line = _lineNumber,
                                Column = _index - _lineStart - 1
                            };
                        AddComment("Line", comment, start, _index - 1, _location);
                    }
                    if (ch == 13 && _source.CharCodeAt(_index) == 10)
                    {
                        ++_index;
                    }
                    ++_lineNumber;
                    _lineStart = _index;
                    return;
                }
            }

            if (_extra.Comments != null)
            {
                var comment = _source.Slice(start + 2, _index);
                _location.End = new Position
                    {
                        Line = _lineNumber,
                        Column = _index - _lineStart
                    };
                AddComment("Line", comment, start, _index, _location);
            }
        }

        private void SkipMultiLineComment()
        {
            //var start, loc, ch, comment;
            int start = 0;

            if (_extra.Comments != null)
            {
                start = _index - 2;
                _location = new Location
                    {
                        Start = new Position
                            {
                                Line = _lineNumber,
                                Column = _index - _lineStart - 2
                            }
                    };
            }

            while (_index < _length)
            {
                char ch = _source.CharCodeAt(_index);
                if (IsLineTerminator(ch))
                {
                    if (ch == 13 && _source.CharCodeAt(_index + 1) == 10)
                    {
                        ++_index;
                    }
                    ++_lineNumber;
                    ++_index;
                    _lineStart = _index;
                    if (_index >= _length)
                    {
                        ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
                    }
                }
                else if (ch == 42)
                {
                    // Block comment ends with '*/' (char #42, char #47).
                    if (_source.CharCodeAt(_index + 1) == 47)
                    {
                        ++_index;
                        ++_index;
                        if (_extra.Comments != null)
                        {
                            string comment = _source.Slice(start + 2, _index - 2);
                            _location.End = new Position
                                {
                                    Line = _lineNumber,
                                    Column = _index - _lineStart
                                };
                            AddComment("Block", comment, start, _index, _location);
                        }
                        return;
                    }
                    ++_index;
                }
                else
                {
                    ++_index;
                }
            }

            ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
        }

        private void SkipComment()
        {
            while (_index < _length)
            {
                char ch = _source.CharCodeAt(_index);

                if (IsWhiteSpace(ch))
                {
                    ++_index;
                }
                else if (IsLineTerminator(ch))
                {
                    ++_index;
                    if (ch == 13 && _source.CharCodeAt(_index) == 10)
                    {
                        ++_index;
                    }
                    ++_lineNumber;
                    _lineStart = _index;
                }
                else if (ch == 47)
                {
                    // 47 is '/'
                    ch = _source.CharCodeAt(_index + 1);
                    if (ch == 47)
                    {
                        ++_index;
                        ++_index;
                        SkipSingleLineComment();
                    }
                    else if (ch == 42)
                    {
                        // 42 is '*'
                        ++_index;
                        ++_index;
                        SkipMultiLineComment();
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }


        private bool ScanHexEscape(char prefix, out char result)
        {
            int code = char.MinValue;

            int len = (prefix == 'u') ? 4 : 2;
            for (int i = 0; i < len; ++i)
            {
                if (_index < _length && IsHexDigit(_source.CharCodeAt(_index)))
                {
                    char ch = _source.CharCodeAt(_index++);
                    code = code*16 +
                           "0123456789abcdef".IndexOf(ch.ToString(),
                                                      StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    result = char.MinValue;
                    return false;
                }
            }

            result = (char) code;
            return true;
        }

        private string GetEscapedIdentifier()
        {
            char ch = _source.CharCodeAt(_index++);
            var id = new StringBuilder(ch.ToString());

            // '\u' (char #92, char #117) denotes an escaped character.
            if (ch == 92)
            {
                if (_source.CharCodeAt(_index) != 117)
                {
                    ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
                }
                ++_index;

                if (!ScanHexEscape('u', out ch) || ch == '\\' || !IsIdentifierStart(ch))
                {
                    ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
                }
                id = new StringBuilder(ch.ToString());
            }

            while (_index < _length)
            {
                ch = _source.CharCodeAt(_index);
                if (!IsIdentifierPart(ch))
                {
                    break;
                }
                ++_index;
                

                // '\u' (char #92, char #117) denotes an escaped character.
                if (ch == 92)
                {
                    if (_source.CharCodeAt(_index) != 117)
                    {
                        ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
                    }
                    ++_index;

                    if (!ScanHexEscape('u', out ch) || ch == '\\' || !IsIdentifierPart(ch))
                    {
                        ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
                    }
                    id.Append(ch);
                }
                else
                {
                    id.Append(ch);
                }
            }

            return id.ToString();
        }

        private string GetIdentifier()
        {
            int start = _index++;
            while (_index < _length)
            {
                char ch = _source.CharCodeAt(_index);
                if (ch == 92)
                {
                    // Blackslash (char #92) marks Unicode escape sequence.
                    _index = start;
                    return GetEscapedIdentifier();
                }
                if (IsIdentifierPart(ch))
                {
                    ++_index;
                }
                else
                {
                    break;
                }
            }

            return _source.Slice(start, _index);
        }

        private Token ScanIdentifier()
        {
            int start = _index;
            Tokens type;

            // Backslash (char #92) starts an escaped character.
            string id = (_source.CharCodeAt(_index) == 92) ? GetEscapedIdentifier() : GetIdentifier();

            // There is no keyword or literal with only one character.
            // Thus, it must be an identifier.
            if (id.Length == 1)
            {
                type = Tokens.Identifier;
            }
            else if (IsKeyword(id))
            {
                type = Tokens.Keyword;
            }
            else if ("null".Equals(id))
            {
                type = Tokens.NullLiteral;
            }
            else if ("true".Equals(id) || "false".Equals(id))
            {
                type = Tokens.BooleanLiteral;
            }
            else
            {
                type = Tokens.Identifier;
            }

            return new Token
                {
                    Type = type,
                    Value = id,
                    LineNumber = _lineNumber,
                    LineStart = _lineStart,
                    Range = new[] {start, _index}
                };
        }


        // 7.7 Punctuators

        private Token ScanPunctuator()
        {
            int start = _index;
            char code = _source.CharCodeAt(_index);
            char ch1 = _source.CharCodeAt(_index);

            switch ((int) code)
            {
                    // Check for most common single-character punctuators.
                case 46: // . dot
                case 40: // ( open bracket
                case 41: // ) close bracket
                case 59: // ; semicolon
                case 44: // , comma
                case 123: // { open curly brace
                case 125: // } close curly brace
                case 91: // [
                case 93: // ]
                case 58: // :
                case 63: // ?
                case 126: // ~
                    ++_index;

                    return new Token
                        {
                            Type = Tokens.Punctuator,
                            Value = code.ToString(),
                            LineNumber = _lineNumber,
                            LineStart = _lineStart,
                            Range = new[] {start, _index}
                        };

                default:
                    char code2 = _source.CharCodeAt(_index + 1);

                    // '=' (char #61) marks an assignment or comparison operator.
                    if (code2 == 61)
                    {
                        switch ((int) code)
                        {
                            case 37: // %
                            case 38: // &
                            case 42: // *:
                            case 43: // +
                            case 45: // -
                            case 47: // /
                            case 60: // <
                            case 62: // >
                            case 94: // ^
                            case 124: // |
                                _index += 2;
                                return new Token
                                    {
                                        Type = Tokens.Punctuator,
                                        Value =
                                            code.ToString() +
                                            code2.ToString(),
                                        LineNumber = _lineNumber,
                                        LineStart = _lineStart,
                                        Range = new[] {start, _index}
                                    };

                            case 33: // !
                            case 61: // =
                                _index += 2;

                                // !== and ===
                                if (_source.CharCodeAt(_index) == 61)
                                {
                                    ++_index;
                                }
                                return new Token
                                    {
                                        Type = Tokens.Punctuator,
                                        Value = _source.Slice(start, _index),
                                        LineNumber = _lineNumber,
                                        LineStart = _lineStart,
                                        Range = new[] {start, _index}
                                    };
                        }
                    }
                    break;
            }

            // Peek more characters.

            char ch2 = _source.CharCodeAt(_index + 1);
            char ch3 = _source.CharCodeAt(_index + 2);
            char ch4 = _source.CharCodeAt(_index + 3);

            // 4-character punctuator: >>>=

            if (ch1 == '>' && ch2 == '>' && ch3 == '>')
            {
                if (ch4 == '=')
                {
                    _index += 4;
                    return new Token
                        {
                            Type = Tokens.Punctuator,
                            Value = ">>>=",
                            LineNumber = _lineNumber,
                            LineStart = _lineStart,
                            Range = new[] {start, _index}
                        };
                }
            }

            // 3-character punctuators: == !== >>> <<= >>=

            if (ch1 == '>' && ch2 == '>' && ch3 == '>')
            {
                _index += 3;
                return new Token
                    {
                        Type = Tokens.Punctuator,
                        Value = ">>>",
                        LineNumber = _lineNumber,
                        LineStart = _lineStart,
                        Range = new[] {start, _index}
                    };
            }

            if (ch1 == '<' && ch2 == '<' && ch3 == '=')
            {
                _index += 3;
                return new Token
                    {
                        Type = Tokens.Punctuator,
                        Value = "<<=",
                        LineNumber = _lineNumber,
                        LineStart = _lineStart,
                        Range = new[] {start, _index}
                    };
            }

            if (ch1 == '>' && ch2 == '>' && ch3 == '=')
            {
                _index += 3;
                return new Token
                    {
                        Type = Tokens.Punctuator,
                        Value = ">>=",
                        LineNumber = _lineNumber,
                        LineStart = _lineStart,
                        Range = new[] {start, _index}
                    };
            }

            // Other 2-character punctuators: ++ -- << >> && ||

            if (ch1 == ch2 && ("+-<>&|".IndexOf(ch1) >= 0))
            {
                _index += 2;
                return new Token
                    {
                        Type = Tokens.Punctuator,
                        Value = ch1.ToString() + ch2.ToString(),
                        LineNumber = _lineNumber,
                        LineStart = _lineStart,
                        Range = new[] {start, _index}
                    };
            }

            if ("<>=!+-*%&|^/".IndexOf(ch1) >= 0)
            {
                ++_index;
                return new Token
                    {
                        Type = Tokens.Punctuator,
                        Value = ch1.ToString(),
                        LineNumber = _lineNumber,
                        LineStart = _lineStart,
                        Range = new[] {start, _index}
                    };
            }

            ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");

            return null;
        }

        // 7.8.3 Numeric Literals

        private Token ScanHexLiteral(int start)
        {
            string number = "";

            while (_index < _length)
            {
                if (!IsHexDigit(_source.CharCodeAt(_index)))
                {
                    break;
                }
                number += _source.CharCodeAt(_index++).ToString();
            }

            if (number.Length == 0)
            {
                ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
            }

            if (IsIdentifierStart(_source.CharCodeAt(_index)))
            {
                ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
            }

            return new Token
                {
                    Type = Tokens.NumericLiteral,
                    Value = Convert.ToInt64(number, 16),
                    LineNumber = _lineNumber,
                    LineStart = _lineStart,
                    Range = new[] {start, _index}
                };
        }

        private Token ScanOctalLiteral(int start)
        {
            string number = "0" + _source.CharCodeAt(_index++);
            while (_index < _length)
            {
                if (!IsOctalDigit(_source.CharCodeAt(_index)))
                {
                    break;
                }
                number += _source.CharCodeAt(_index++).ToString();
            }

            if (IsIdentifierStart(_source.CharCodeAt(_index)) || IsDecimalDigit(_source.CharCodeAt(_index)))
            {
                ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
            }

            return new Token
                {
                    Type = Tokens.NumericLiteral,
                    Value = Convert.ToInt32(number, 8),
                    Octal = true,
                    LineNumber = _lineNumber,
                    LineStart = _lineStart,
                    Range = new[] {start, _index}
                };
        }

        private Token ScanNumericLiteral()
        {
            char ch = _source.CharCodeAt(_index);

            int start = _index;
            string number = "";
            if (ch != '.')
            {
                number = _source.CharCodeAt(_index++).ToString();
                ch = _source.CharCodeAt(_index);

                // Hex number starts with '0x'.
                // Octal number starts with '0'.
                if (number == "0")
                {
                    if (ch == 'x' || ch == 'X')
                    {
                        ++_index;
                        return ScanHexLiteral(start);
                    }
                    if (IsOctalDigit(ch))
                    {
                        return ScanOctalLiteral(start);
                    }

                    // decimal number starts with '0' such as '09' is illegal.
                    if (ch > 0 && IsDecimalDigit(ch))
                    {
                        ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
                    }
                }

                while (IsDecimalDigit(_source.CharCodeAt(_index)))
                {
                    number += _source.CharCodeAt(_index++).ToString();
                }
                ch = _source.CharCodeAt(_index);
            }

            if (ch == '.')
            {
                number += _source.CharCodeAt(_index++).ToString();
                while (IsDecimalDigit(_source.CharCodeAt(_index)))
                {
                    number += _source.CharCodeAt(_index++).ToString();
                }
                ch = _source.CharCodeAt(_index);
            }

            if (ch == 'e' || ch == 'E')
            {
                number += _source.CharCodeAt(_index++).ToString();

                ch = _source.CharCodeAt(_index);
                if (ch == '+' || ch == '-')
                {
                    number += _source.CharCodeAt(_index++).ToString();
                }
                if (IsDecimalDigit(_source.CharCodeAt(_index)))
                {
                    while (IsDecimalDigit(_source.CharCodeAt(_index)))
                    {
                        number += _source.CharCodeAt(_index++).ToString();
                    }
                }
                else
                {
                    ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
                }
            }

            if (IsIdentifierStart(_source.CharCodeAt(_index)))
            {
                ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
            }

            double n;
            try
            {
                n = Double.Parse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);

                if (n > double.MaxValue)
                {
                    n = double.PositiveInfinity;
                }
                else if (n < -double.MaxValue)
                {
                    n = double.NegativeInfinity;
                }
            }
            catch (OverflowException)
            {
                n = number.Trim().StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity;
            }
            catch (Exception)
            {
                n = double.NaN;
            }

            return new Token
                {
                    Type = Tokens.NumericLiteral,
                    Value = n,
                    LineNumber = _lineNumber,
                    LineStart = _lineStart,
                    Range = new[] {start, _index}
                };
        }

        // 7.8.4 String Literals

        private Token ScanStringLiteral()
        {
            var str = new StringBuilder();
            bool octal = false;

            char quote = _source.CharCodeAt(_index);

            int start = _index;
            ++_index;

            while (_index < _length)
            {
                char ch = _source.CharCodeAt(_index++);

                if (ch == quote)
                {
                    quote = char.MinValue;
                    break;
                }
                
                if (ch == '\\')
                {
                    ch = _source.CharCodeAt(_index++);
                    if (ch == char.MinValue || !IsLineTerminator(ch))
                    {
                        switch (ch)
                        {
                            case 'n':
                                str.Append('\n');
                                break;
                            case 'r':
                                str.Append('\r');
                                break;
                            case 't':
                                str.Append('\t');
                                break;
                            case 'u':
                            case 'x':
                                int restore = _index;
                                char unescaped;
                                if(ScanHexEscape(ch, out unescaped))
                                {
                                    str.Append(unescaped);
                                }
                                else
                                {
                                    _index = restore;
                                    str.Append(ch);
                                }
                                break;
                            case 'b':
                                str.Append("\b");
                                break;
                            case 'f':
                                str.Append("\f");
                                break;
                            case 'v':
                                str.Append("\x0B");
                                break;

                            default:
                                if (IsOctalDigit(ch))
                                {
                                    int code = "01234567".IndexOf(ch);

                                    // \0 is not octal escape sequence
                                    if (code != 0)
                                    {
                                        octal = true;
                                    }

                                    if (_index < _length && IsOctalDigit(_source.CharCodeAt(_index)))
                                    {
                                        octal = true;
                                        code = code * 8 + "01234567".IndexOf(_source.CharCodeAt(_index++));

                                        // 3 digits are only allowed when string starts
                                        // with 0, 1, 2, 3
                                        if ("0123".IndexOf(ch) >= 0 &&
                                            _index < _length &&
                                            IsOctalDigit(_source.CharCodeAt(_index)))
                                        {
                                            code = code * 8 + "01234567".IndexOf(_source.CharCodeAt(_index++));
                                        }
                                    }
                                    str.Append((char)code);
                                }
                                else
                                {
                                    str.Append(ch);
                                }
                                break;
                        }
                    }
                    else
                    {
                        ++_lineNumber;
                        if (ch == '\r' && _source.CharCodeAt(_index) == '\n')
                        {
                            ++_index;
                        }
                    }
                }
                else if (IsLineTerminator(ch))
                {
                    break;
                }
                else
                {
                    str.Append(ch);
                }
            }

            if (quote != 0)
            {
                ThrowError(null, Messages.UnexpectedToken, "ILLEGAL");
            }

            return new Token
                {
                    Type = Tokens.StringLiteral,
                    Value = str.ToString(),
                    Octal = octal,
                    LineNumber = _lineNumber,
                    LineStart = _lineStart,
                    Range = new[] {start, _index}
                };
        }

        private Token ScanRegExp()
        {
            bool classMarker = false;
            bool terminated = false;

            SkipComment();

            int start = _index;
            char ch;

            var str = new StringBuilder(_source.CharCodeAt(_index++).ToString());

            while (_index < _length)
            {
                ch = _source.CharCodeAt(_index++);
                
                str.Append(ch);
                if (ch == '\\')
                {
                    ch = _source.CharCodeAt(_index++);
                    // ECMA-262 7.8.5
                    if (IsLineTerminator(ch))
                    {
                        ThrowError(null, Messages.UnterminatedRegExp);
                    }
                    str.Append(ch);
                }
                else if (IsLineTerminator(ch))
                {
                    ThrowError(null, Messages.UnterminatedRegExp);
                }
                else if (classMarker)
                {
                    if (ch == ']')
                    {
                        classMarker = false;
                    }
                }
                else
                {
                    if (ch == '/')
                    {
                        terminated = true;
                        break;
                    }
                    
                    if (ch == '[')
                    {
                        classMarker = true;
                    }
                }
            }

            if (!terminated)
            {
                ThrowError(null, Messages.UnterminatedRegExp);
            }

            // Exclude leading and trailing slash.
            string pattern = str.ToString().Substring(1, str.Length - 2);

            string flags = "";
            while (_index < _length)
            {
                ch = _source.CharCodeAt(_index);
                if (!IsIdentifierPart(ch))
                {
                    break;
                }

                ++_index;
                if (ch == '\\' && _index < _length)
                {
                    ch = _source.CharCodeAt(_index);
                    if (ch == 'u')
                    {
                        ++_index;
                        int restore = _index;
                        if(ScanHexEscape('u', out ch))
                        {
                            flags += ch.ToString();
                            for (str.Append("\\u"); restore < _index; ++restore)
                            {
                                str.Append(_source.CharCodeAt(restore).ToString());
                            }
                        }
                        else
                        {
                            _index = restore;
                            flags += "u";
                            str.Append("\\u");
                        }
                    }
                    else
                    {
                        str.Append("\\");
                    }
                }
                else
                {
                    flags += ch.ToString();
                    str.Append(ch.ToString());
                }
            }

            Peek();

            return new Token
                {
                    Type = Tokens.RegularExpression,
                    Literal = str.ToString(),
                    Value = pattern + flags,
                    Range = new[] {start, _index}
                };
        }

        private Token CollectRegex()
        {
            SkipComment();

            int pos = _index;
            var loc = new Location
                {
                    Start = new Position
                        {
                            Line = _lineNumber,
                            Column = _index - _lineStart
                        }
                };

            Token regex = ScanRegExp();
            loc.End = new Position
                {
                    Line = _lineNumber,
                    Column = _index - _lineStart
                };

            // Pop the previous token, which is likely '/' or '/='
            if (_extra.Tokens != null)
            {
                Token token = _extra.Tokens[_extra.Tokens.Count - 1];
                if (token.Range[0] == pos && token.Type == Tokens.Punctuator)
                {
                    if ("/".Equals(token.Value) || "/=".Equals(token.Value))
                    {
                        _extra.Tokens.RemoveAt(_extra.Tokens.Count - 1);
                    }
                }

                _extra.Tokens.Add(new Token
                {
                    Type = Tokens.RegularExpression,
                    Value = regex.Literal,
                    Range = new[] { pos, _index },
                    Location = loc
                });
            }

            return regex;
        }

        private bool IsIdentifierName(Token token)
        {
            return token.Type == Tokens.Identifier ||
                   token.Type == Tokens.Keyword ||
                   token.Type == Tokens.BooleanLiteral ||
                   token.Type == Tokens.NullLiteral;
        }

        private Token Advance()
        {
            SkipComment();

            if (_index >= _length)
            {
                return new Token
                    {
                        Type = Tokens.EOF,
                        LineNumber = _lineNumber,
                        LineStart = _lineStart,
                        Range = new[] {_index, _index}
                    };
            }

            char ch = _source.CharCodeAt(_index);

            // Very common: ( and ) and ;
            if (ch == 40 || ch == 41 || ch == 58)
            {
                return ScanPunctuator();
            }

            // String literal starts with single quote (#39) or double quote (#34).
            if (ch == 39 || ch == 34)
            {
                return ScanStringLiteral();
            }

            if (IsIdentifierStart(ch))
            {
                return ScanIdentifier();
            }

            // Dot (.) char #46 can also start a floating-point number, hence the need
            // to check the next character.
            if (ch == 46)
            {
                if (IsDecimalDigit(_source.CharCodeAt(_index + 1)))
                {
                    return ScanNumericLiteral();
                }
                return ScanPunctuator();
            }

            if (IsDecimalDigit(ch))
            {
                return ScanNumericLiteral();
            }

            return ScanPunctuator();
        }

        private Token CollectToken()
        {
            SkipComment();
            _location = new Location
                {
                    Start = new Position
                        {
                            Line = _lineNumber,
                            Column = _index - _lineStart
                        }
                };

            Token token = Advance();
            _location.End = new Position
                {
                    Line = _lineNumber,
                    Column = _index - _lineStart
                };

            if (token.Type != Tokens.EOF)
            {
                var range = new[] {token.Range[0], token.Range[1]};
                string value = _source.Slice(token.Range[0], token.Range[1]);
                _extra.Tokens.Add(new Token
                    {
                        Type = token.Type,
                        Value = value,
                        Range = range,
                        Location = _location
                    });
            }

            return token;
        }

        private Token Lex()
        {
            Token token = _lookahead;
            _index = token.Range[1];
            _lineNumber = token.LineNumber.HasValue ? token.LineNumber.Value : 0;
            _lineStart = token.LineStart;

            _lookahead = (_extra.Tokens != null) ? CollectToken() : Advance();

            _index = token.Range[1];
            _lineNumber = token.LineNumber.HasValue ? token.LineNumber.Value : 0;
            _lineStart = token.LineStart;

            return token;
        }

        private void Peek()
        {
            int pos = _index;
            int line = _lineNumber;
            int start = _lineStart;
            _lookahead = (_extra.Tokens != null) ? CollectToken() : Advance();
            _index = pos;
            _lineNumber = line;
            _lineStart = start;
        }

        private void MarkStart()
        {
            SkipComment();
            if (_extra.Loc.HasValue)
            {
                _state.MarkerStack.Push(_index - _lineStart);
                _state.MarkerStack.Push(_lineNumber);
            }
            if (_extra.Range != null)
            {
                _state.MarkerStack.Push(_index);
            }
        }

        private T MarkEnd<T>(T node) where T : SyntaxNode
        {
            if (_extra.Range != null)
            {
                node.Range = new[] {_state.MarkerStack.Pop(), _index};
            }
            if (_extra.Loc.HasValue)
            {
                node.Location = new Location
                    {
                        Start = new Position
                            {
                                Line = _state.MarkerStack.Pop(),
                                Column = _state.MarkerStack.Pop()
                            },
                        End = new Position
                            {
                                Line = _lineNumber,
                                Column = _index - _lineStart
                            }
                    };
                PostProcess(node);
            }
            return node;
        }

        public T MarkEndIf<T>(T node) where T : SyntaxNode
        {
            if (node.Range != null || node.Location != null)
            {
                if (_extra.Loc.HasValue)
                {
                    _state.MarkerStack.Pop();
                    _state.MarkerStack.Pop();
                }
                if (_extra.Range != null)
                {
                    _state.MarkerStack.Pop();
                }
            }
            else
            {
                MarkEnd(node);
            }
            return node;
        }

        public SyntaxNode PostProcess(SyntaxNode node)
        {
            if (_extra.Source != null)
            {
                node.Location.Source = _extra.Source;
            }
            return node;
        }

        public ArrayExpression CreateArrayExpression(IEnumerable<Expression> elements)
        {
            return new ArrayExpression
                {
                    Type = SyntaxNodes.ArrayExpression,
                    Elements = elements
                };
        }

        public AssignmentExpression CreateAssignmentExpression(string op, Expression left, Expression right)
        {
            return new AssignmentExpression
                {
                    Type = SyntaxNodes.AssignmentExpression,
                    Operator = AssignmentExpression.ParseAssignmentOperator(op),
                    Left = left,
                    Right = right
                };
        }

        public Expression CreateBinaryExpression(string op, Expression left, Expression right)
        {
            
            return (op == "||" || op == "&&")
                       ? (Expression)new LogicalExpression
                           {
                               Type = SyntaxNodes.LogicalExpression,
                               Operator = LogicalExpression.ParseLogicalOperator(op),
                               Left = left,
                               Right = right
                           }
                       : new BinaryExpression
                           {
                               Type = SyntaxNodes.BinaryExpression,
                               Operator = BinaryExpression.ParseBinaryOperator(op),
                               Left = left,
                               Right = right
                           };
        }

        public BlockStatement CreateBlockStatement(IEnumerable<Statement> body)
        {
            return new BlockStatement
                {
                    Type = SyntaxNodes.BlockStatement,
                    Body = body
                };
        }

        public BreakStatement CreateBreakStatement(Identifier label)
        {
            return new BreakStatement
                {
                    Type = SyntaxNodes.BreakStatement,
                    Label = label
                };
        }

        public CallExpression CreateCallExpression(Expression callee, IEnumerable<Expression> args)
        {
            return new CallExpression
                {
                    Type = SyntaxNodes.CallExpression,
                    Callee = callee,
                    Arguments = args
                };
        }

        public CatchClause CreateCatchClause(Identifier param, BlockStatement body)
        {
            return new CatchClause
                {
                    Type = SyntaxNodes.CatchClause,
                    Param = param,
                    Body = body
                };
        }

        public ConditionalExpression CreateConditionalExpression(Expression test, Expression consequent,
                                                                 Expression alternate)
        {
            return new ConditionalExpression
                {
                    Type = SyntaxNodes.ConditionalExpression,
                    Test = test,
                    Consequent = consequent,
                    Alternate = alternate
                };
        }

        public ContinueStatement CreateContinueStatement(Identifier label)
        {
            return new ContinueStatement
                {
                    Type = SyntaxNodes.ContinueStatement,
                    Label = label
                };
        }

        public DebuggerStatement CreateDebuggerStatement()
        {
            return new DebuggerStatement
                {
                    Type = SyntaxNodes.DebuggerStatement
                };
        }

        public DoWhileStatement CreateDoWhileStatement(Statement body, Expression test)
        {
            return new DoWhileStatement
                {
                    Type = SyntaxNodes.DoWhileStatement,
                    Body = body,
                    Test = test
                };
        }

        public EmptyStatement CreateEmptyStatement()
        {
            return new EmptyStatement
                {
                    Type = SyntaxNodes.EmptyStatement
                };
        }

        public ExpressionStatement CreateExpressionStatement(Expression expression)
        {
            return new ExpressionStatement
                {
                    Type = SyntaxNodes.ExpressionStatement,
                    Expression = expression
                };
        }

        public ForStatement CreateForStatement(SyntaxNode init, Expression test, Expression update, Statement body)
        {
            return new ForStatement
                {
                    Type = SyntaxNodes.ForStatement,
                    Init = init,
                    Test = test,
                    Update = update,
                    Body = body
                };
        }

        public ForInStatement CreateForInStatement(SyntaxNode left, Expression right, Statement body)
        {
            return new ForInStatement
                {
                    Type = SyntaxNodes.ForInStatement,
                    Left = left,
                    Right = right,
                    Body = body,
                    Each = false
                };
        }

        public FunctionDeclaration CreateFunctionDeclaration(Identifier id, IEnumerable<Identifier> parameters,
                                                             IEnumerable<Expression> defaults, Statement body, bool strict)
        {
            var functionDeclaration = new FunctionDeclaration
                {
                    Type = SyntaxNodes.FunctionDeclaration,
                    Id = id,
                    Parameters = parameters,
                    Defaults = defaults,
                    Body = body,
                    Strict = strict,
                    Rest = null,
                    Generator = false,
                    Expression = false,
                    VariableDeclarations = LeaveVariableScope(),
                    FunctionDeclarations = LeaveFunctionScope()
                };

            _functionScopes.Peek().FunctionDeclarations.Add(functionDeclaration);

            return functionDeclaration;
        }

        public FunctionExpression CreateFunctionExpression(Identifier id, IEnumerable<Identifier> parameters,
                                                           IEnumerable<Expression> defaults, Statement body, bool strict)
        {
            return new FunctionExpression
                {
                    Type = SyntaxNodes.FunctionExpression,
                    Id = id,
                    Parameters = parameters,
                    Defaults = defaults,
                    Body = body,
                    Strict = strict,
                    Rest = null,
                    Generator = false,
                    Expression = false,
                    VariableDeclarations = LeaveVariableScope(),
                    FunctionDeclarations = LeaveFunctionScope()
                };
        }

        public Identifier CreateIdentifier(string name)
        {
            return new Identifier
                {
                    Type = SyntaxNodes.Identifier,
                    Name = name
                };
        }

        public IfStatement CreateIfStatement(Expression test, Statement consequent, Statement alternate)
        {
            return new IfStatement
                {
                    Type = SyntaxNodes.IfStatement,
                    Test = test,
                    Consequent = consequent,
                    Alternate = alternate
                };
        }

        public LabelledStatement CreateLabeledStatement(Identifier label, Statement body)
        {
            return new LabelledStatement
                {
                    Type = SyntaxNodes.LabeledStatement,
                    Label = label,
                    Body = body
                };
        }

        public Literal CreateLiteral(Token token)
        {
            if (token.Type == Tokens.RegularExpression)
            {
                return new Literal
                {
                    Type = SyntaxNodes.RegularExpressionLiteral,
                    Value = token.Value,
                    Raw = _source.Slice(token.Range[0], token.Range[1])
                };
            }

            return new Literal
                {
                    Type = SyntaxNodes.Literal,
                    Value = token.Value,
                    Raw = _source.Slice(token.Range[0], token.Range[1])
                };
        }

        public MemberExpression CreateMemberExpression(char accessor, Expression obj, Expression property)
        {
            return new MemberExpression
                {
                    Type = SyntaxNodes.MemberExpression,
                    Computed = accessor == '[',
                    Object = obj,
                    Property = property
                };
        }

        public NewExpression CreateNewExpression(Expression callee, IEnumerable<Expression> args)
        {
            return new NewExpression
                {
                    Type = SyntaxNodes.NewExpression,
                    Callee = callee,
                    Arguments = args
                };
        }

        public ObjectExpression CreateObjectExpression(IEnumerable<Property> properties)
        {
            return new ObjectExpression
                {
                    Type = SyntaxNodes.ObjectExpression,
                    Properties = properties
                };
        }

        public UpdateExpression CreatePostfixExpression(string op, Expression argument)
        {
            return new UpdateExpression
                {
                    Type = SyntaxNodes.UpdateExpression,
                    Operator = UnaryExpression.ParseUnaryOperator(op),
                    Argument = argument,
                    Prefix = false
                };
        }

        public Program CreateProgram(ICollection<Statement> body, bool strict)
        {
            return new Program
                {
                    Type = SyntaxNodes.Program,
                    Body = body,
                    Strict = strict,
                    VariableDeclarations = LeaveVariableScope(),
                    FunctionDeclarations = LeaveFunctionScope()
                };
        }

        public Property CreateProperty(PropertyKind kind, IPropertyKeyExpression key, Expression value)
        {
            return new Property
                {
                    Type = SyntaxNodes.Property,
                    Key = key,
                    Value = value,
                    Kind = kind
                };
        }

        public ReturnStatement CreateReturnStatement(Expression argument)
        {
            return new ReturnStatement
                {
                    Type = SyntaxNodes.ReturnStatement,
                    Argument = argument
                };
        }

        public SequenceExpression CreateSequenceExpression(IList<Expression> expressions)
        {
            return new SequenceExpression
                {
                    Type = SyntaxNodes.SequenceExpression,
                    Expressions = expressions
                };
        }

        public SwitchCase CreateSwitchCase(Expression test, IEnumerable<Statement> consequent)
        {
            return new SwitchCase
                {
                    Type = SyntaxNodes.SwitchCase,
                    Test = test,
                    Consequent = consequent
                };
        }

        public SwitchStatement CreateSwitchStatement(Expression discriminant, IEnumerable<SwitchCase> cases)
        {
            return new SwitchStatement
                {
                    Type = SyntaxNodes.SwitchStatement,
                    Discriminant = discriminant,
                    Cases = cases
                };
        }

        public ThisExpression CreateThisExpression()
        {
            return new ThisExpression
                {
                    Type = SyntaxNodes.ThisExpression
                };
        }

        public ThrowStatement CreateThrowStatement(Expression argument)
        {
            return new ThrowStatement
                {
                    Type = SyntaxNodes.ThrowStatement,
                    Argument = argument
                };
        }

        public TryStatement CreateTryStatement(Statement block, IEnumerable<Statement> guardedHandlers,
                                               IEnumerable<CatchClause> handlers, Statement finalizer)
        {
            return new TryStatement
                {
                    Type = SyntaxNodes.TryStatement,
                    Block = block,
                    GuardedHandlers = guardedHandlers,
                    Handlers = handlers,
                    Finalizer = finalizer
                };
        }

        public UnaryExpression CreateUnaryExpression(string op, Expression argument)
        {
            if (op == "++" || op == "--")
            {
                return new UpdateExpression
                {
                    Type = SyntaxNodes.UpdateExpression,
                    Operator = UnaryExpression.ParseUnaryOperator(op),
                    Argument = argument,
                    Prefix = true
                };
            }

            return new UnaryExpression
            {
                Type = SyntaxNodes.UnaryExpression,
                Operator = UnaryExpression.ParseUnaryOperator(op),
                Argument = argument,
                Prefix = true
            };
        }


        public VariableDeclaration CreateVariableDeclaration(IEnumerable<VariableDeclarator> declarations, string kind)
        {
            var variableDeclaration = new VariableDeclaration
                {
                    Type = SyntaxNodes.VariableDeclaration,
                    Declarations = declarations,
                    Kind = kind
                };

            _variableScopes.Peek().VariableDeclarations.Add(variableDeclaration);

            return variableDeclaration;
        }

        public VariableDeclarator CreateVariableDeclarator(Identifier id, Expression init)
        {
            return new VariableDeclarator
                {
                    Type = SyntaxNodes.VariableDeclarator,
                    Id = id,
                    Init = init
                };
        }

        public WhileStatement CreateWhileStatement(Expression test, Statement body)
        {
            return new WhileStatement
                {
                    Type = SyntaxNodes.WhileStatement,
                    Test = test,
                    Body = body
                };
        }

        public WithStatement CreateWithStatement(Expression obj, Statement body)
        {
            return new WithStatement
                {
                    Type = SyntaxNodes.WithStatement,
                    Object = obj,
                    Body = body
                };
        }

        // Return true if there is a line terminator before the next token.

        private bool PeekLineTerminator()
        {
            int pos = _index;
            int line = _lineNumber;
            int start = _lineStart;
            SkipComment();
            bool found = _lineNumber != line;
            _index = pos;
            _lineNumber = line;
            _lineStart = start;

            return found;
        }

        // Throw an exception

        private void ThrowError(Token token, string messageFormat, params object[] arguments)
        {
            ParserException exception;
            string msg = String.Format(messageFormat, arguments);

            if (token != null && token.LineNumber.HasValue)
            {
                exception = new ParserException("Line " + token.LineNumber + ": " + msg)
                    {
                        Index = token.Range[0],
                        LineNumber = token.LineNumber.Value,
                        Column = token.Range[0] - _lineStart + 1,
                        Source = _extra.Source
                    };
            }
            else
            {
                exception = new ParserException("Line " + _lineNumber + ": " + msg)
                    {
                        Index = _index,
                        LineNumber = _lineNumber,
                        Column = _index - _lineStart + 1,
                        Source = _extra.Source
                    };
            }

            exception.Description = msg;
            throw exception;
        }

        private void ThrowErrorTolerant(Token token, string messageFormat, params object[] arguments)
        {
            try
            {
                ThrowError(token, messageFormat, arguments);
            }
            catch (Exception e)
            {
                if (_extra.Errors != null)
                {
                    _extra.Errors.Add(new ParserException(e.Message)
                    {
                        Source = _extra.Source
                    });
                }
                else
                {
                    throw;
                }
            }
        }

        // Throw an exception because of the token.

        private void ThrowUnexpected(Token token)
        {
            if (token.Type == Tokens.EOF)
            {
                ThrowError(token, Messages.UnexpectedEOS);
            }

            if (token.Type == Tokens.NumericLiteral)
            {
                ThrowError(token, Messages.UnexpectedNumber);
            }

            if (token.Type == Tokens.StringLiteral)
            {
                ThrowError(token, Messages.UnexpectedString);
            }

            if (token.Type == Tokens.Identifier)
            {
                ThrowError(token, Messages.UnexpectedIdentifier);
            }

            if (token.Type == Tokens.Keyword)
            {
                if (IsFutureReservedWord(token.Value as string))
                {
                    ThrowError(token, Messages.UnexpectedReserved);
                }
                else if (_strict && IsStrictModeReservedWord(token.Value as string))
                {
                    ThrowErrorTolerant(token, Messages.StrictReservedWord);
                    return;
                }
                ThrowError(token, Messages.UnexpectedToken, token.Value as string);
            }

            // BooleanLiteral, NullLiteral, or Punctuator.
            ThrowError(token, Messages.UnexpectedToken, token.Value as string);
        }

        // Expect the next token to match the specified punctuator.
        // If not, an exception will be thrown.

        private void Expect(string value)
        {
            Token token = Lex();
            if (token.Type != Tokens.Punctuator || !value.Equals(token.Value))
            {
                ThrowUnexpected(token);
            }
        }

        // Expect the next token to match the specified keyword.
        // If not, an exception will be thrown.

        private void ExpectKeyword(string keyword)
        {
            Token token = Lex();
            if (token.Type != Tokens.Keyword || !keyword.Equals(token.Value))
            {
                ThrowUnexpected(token);
            }
        }

        // Return true if the next token matches the specified punctuator.

        private bool Match(string value)
        {
            return _lookahead.Type == Tokens.Punctuator && value.Equals(_lookahead.Value);
        }

        // Return true if the next token matches the specified keyword

        private bool MatchKeyword(object keyword)
        {
            return _lookahead.Type == Tokens.Keyword && keyword.Equals(_lookahead.Value);
        }

        // Return true if the next token is an assignment operator

        private bool MatchAssign()
        {
            if (_lookahead.Type != Tokens.Punctuator)
            {
                return false;
            }
            var op = _lookahead.Value as string;
            return op == "=" ||
                   op == "*=" ||
                   op == "/=" ||
                   op == "%=" ||
                   op == "+=" ||
                   op == "-=" ||
                   op == "<<=" ||
                   op == ">>=" ||
                   op == ">>>=" ||
                   op == "&=" ||
                   op == "^=" ||
                   op == "|=";
        }

        private void ConsumeSemicolon()
        {
            // Catch the very common case first: immediately a semicolon (char #59).
            if (_source.CharCodeAt(_index) == 59)
            {
                Lex();
                return;
            }

            int line = _lineNumber;
            SkipComment();
            if (_lineNumber != line)
            {
                return;
            }

            if (Match(";"))
            {
                Lex();
                return;
            }

            if (_lookahead.Type != Tokens.EOF && !Match("}"))
            {
                ThrowUnexpected(_lookahead);
            }
        }

        // Return true if provided expression is LeftHandSideExpression

        private bool isLeftHandSide(Expression expr)
        {
            return expr.Type == SyntaxNodes.Identifier || expr.Type == SyntaxNodes.MemberExpression;
        }

        // 11.1.4 Array Initialiser

        private ArrayExpression ParseArrayInitialiser()
        {
            var elements = new List<Expression>();

            Expect("[");

            while (!Match("]"))
            {
                if (Match(","))
                {
                    Lex();
                    elements.Add(null);
                }
                else
                {
                    elements.Add(ParseAssignmentExpression());

                    if (!Match("]"))
                    {
                        Expect(",");
                    }
                }
            }

            Expect("]");

            return CreateArrayExpression(elements);
        }

        // 11.1.5 Object Initialiser

        private FunctionExpression ParsePropertyFunction(Identifier[] parameters, Token first = null)
        {
            EnterVariableScope();
            EnterFunctionScope();

            bool previousStrict = _strict;
            MarkStart();
            Statement body = ParseFunctionSourceElements();
            if (first != null && _strict && IsRestrictedWord(parameters[0].Name))
            {
                ThrowErrorTolerant(first, Messages.StrictParamName);
            }
            bool functionStrict = _strict;
            _strict = previousStrict;
            return MarkEnd(CreateFunctionExpression(null, parameters, new Expression[0], body, functionStrict));
        }

        private IPropertyKeyExpression ParseObjectPropertyKey()
        {
            MarkStart();
            Token token = Lex();

            // Note: This function is called only from parseObjectProperty(), where
            // EOF and Punctuator tokens are already filtered out.

            if (token.Type == Tokens.StringLiteral || token.Type == Tokens.NumericLiteral)
            {
                if (_strict && token.Octal)
                {
                    ThrowErrorTolerant(token, Messages.StrictOctalLiteral);
                }
                return MarkEnd(CreateLiteral(token));
            }

            return MarkEnd(CreateIdentifier((string) token.Value));
        }

        private Property ParseObjectProperty()
        {
            Expression value;

            Token token = _lookahead;
            MarkStart();

            if (token.Type == Tokens.Identifier)
            {
                IPropertyKeyExpression id = ParseObjectPropertyKey();

                // Property Assignment: Getter and Setter.

                if ("get".Equals(token.Value) && !Match(":"))
                {
                    var key = ParseObjectPropertyKey();
                    Expect("(");
                    Expect(")");
                    value = ParsePropertyFunction(new Identifier[0]);
                    return MarkEnd(CreateProperty(PropertyKind.Get, key, value));
                }
                if ("set".Equals(token.Value) && !Match(":"))
                {
                    var key = ParseObjectPropertyKey();
                    Expect("(");
                    token = _lookahead;
                    if (token.Type != Tokens.Identifier)
                    {
                        Expect(")");
                        ThrowErrorTolerant(token, Messages.UnexpectedToken, (string) token.Value);
                        value = ParsePropertyFunction(new Identifier[0]);
                    }
                    else
                    {
                        var param = new[] {ParseVariableIdentifier()};
                        Expect(")");
                        value = ParsePropertyFunction(param, token);
                    }
                    return MarkEnd(CreateProperty(PropertyKind.Set, key, value));
                }
                Expect(":");

                value = ParseAssignmentExpression();
                return MarkEnd(CreateProperty(PropertyKind.Data, id, value));
            }
            if (token.Type == Tokens.EOF || token.Type == Tokens.Punctuator)
            {
                ThrowUnexpected(token);
                return null; // can't be reached
            }
            else
            {
                IPropertyKeyExpression key = ParseObjectPropertyKey();
                Expect(":");
                value = ParseAssignmentExpression();
                return MarkEnd(CreateProperty(PropertyKind.Data, key, value));
            }
        }

        private ObjectExpression ParseObjectInitialiser()
        {
            var properties = new List<Property>();
            var map = new Dictionary<string, PropertyKind>();

            Expect("{");

            while (!Match("}"))
            {
                Property property = ParseObjectProperty();

                string name = property.Key.GetKey();

                PropertyKind kind = property.Kind;

                string key = "$" + name;
                if (map.ContainsKey(key))
                {
                    if (map[key] == PropertyKind.Data)
                    {
                        if (_strict && kind == PropertyKind.Data)
                        {
                            ThrowErrorTolerant(Token.Empty, Messages.StrictDuplicateProperty);
                        }
                        else if (kind != PropertyKind.Data)
                        {
                            ThrowErrorTolerant(Token.Empty, Messages.AccessorDataProperty);
                        }
                    }
                    else
                    {
                        if (kind == PropertyKind.Data)
                        {
                            ThrowErrorTolerant(Token.Empty, Messages.AccessorDataProperty);
                        }
                        else if ((map[key] & kind) == kind)
                        {
                            ThrowErrorTolerant(Token.Empty, Messages.AccessorGetSet);
                        }
                    }
                    map[key] |= kind;
                }
                else
                {
                    map[key] = kind;
                }

                properties.Add(property);

                if (!Match("}"))
                {
                    Expect(",");
                }
            }

            Expect("}");

            return CreateObjectExpression(properties);
        }

        // 11.1.6 The Grouping Operator

        private Expression ParseGroupExpression()
        {
            Expect("(");

            Expression expr = ParseExpression();

            Expect(")");

            return expr;
        }


        // 11.1 Primary Expressions

        private Expression ParsePrimaryExpression()
        {
            Expression expr = null;

            if (Match("("))
            {
                return ParseGroupExpression();
            }

            Tokens type = _lookahead.Type;
            MarkStart();

            if (type == Tokens.Identifier)
            {
                expr = CreateIdentifier((string) Lex().Value);
            }
            else if (type == Tokens.StringLiteral || type == Tokens.NumericLiteral)
            {
                if (_strict && _lookahead.Octal)
                {
                    ThrowErrorTolerant(_lookahead, Messages.StrictOctalLiteral);
                }
                expr = CreateLiteral(Lex());
            }
            else if (type == Tokens.Keyword)
            {
                if (MatchKeyword("this"))
                {
                    Lex();
                    expr = CreateThisExpression();
                }
                else if (MatchKeyword("function"))
                {
                    expr = ParseFunctionExpression();
                }
            }
            else if (type == Tokens.BooleanLiteral)
            {
                Token token = Lex();
                token.Value = ("true".Equals(token.Value));
                expr = CreateLiteral(token);
            }
            else if (type == Tokens.NullLiteral)
            {
                Token token = Lex();
                token.Value = null;
                expr = CreateLiteral(token);
            }
            else if (Match("["))
            {
                expr = ParseArrayInitialiser();
            }
            else if (Match("{"))
            {
                expr = ParseObjectInitialiser();
            }
            else if (Match("/") || Match("/="))
            {
                expr = CreateLiteral(_extra.Tokens != null ? CollectRegex() : ScanRegExp());
            }

            if (expr != null)
            {
                return MarkEnd(expr);
            }

            ThrowUnexpected(Lex());
            return null; // can't be reached
        }

        // 11.2 Left-Hand-Side Expressions

        private IEnumerable<Expression> ParseArguments()
        {
            var args = new List<Expression>();

            Expect("(");

            if (!Match(")"))
            {
                while (_index < _length)
                {
                    args.Add(ParseAssignmentExpression());
                    if (Match(")"))
                    {
                        break;
                    }
                    Expect(",");
                }
            }

            Expect(")");

            return args;
        }

        private Identifier ParseNonComputedProperty()
        {
            MarkStart();
            Token token = Lex();

            if (!IsIdentifierName(token))
            {
                ThrowUnexpected(token);
            }

            return MarkEnd(CreateIdentifier((string) token.Value));
        }

        private Identifier ParseNonComputedMember()
        {
            Expect(".");

            return ParseNonComputedProperty();
        }

        private Expression ParseComputedMember()
        {
            Expect("[");

            Expression expr = ParseExpression();

            Expect("]");

            return expr;
        }

        private NewExpression ParseNewExpression()
        {
            MarkStart();
            ExpectKeyword("new");
            Expression callee = ParseLeftHandSideExpression();
            IEnumerable<Expression> args = Match("(") ? ParseArguments() : new AssignmentExpression[0];

            return MarkEnd(CreateNewExpression(callee, args));
        }

        private Expression ParseLeftHandSideExpressionAllowCall()
        {
            LocationMarker marker = CreateLocationMarker();
            
            var previousAllowIn = _state.AllowIn;
            _state.AllowIn = true;
            Expression expr = MatchKeyword("new") ? ParseNewExpression() : ParsePrimaryExpression();
            _state.AllowIn = previousAllowIn;

            while (Match(".") || Match("[") || Match("("))
            {
                if (Match("("))
                {
                    IEnumerable<Expression> args = ParseArguments();
                    expr = CreateCallExpression(expr, args);
                }
                else if (Match("["))
                {
                    Expression property = ParseComputedMember();
                    expr = CreateMemberExpression('[', expr, property);
                }
                else
                {
                    Identifier property = ParseNonComputedMember();
                    expr = CreateMemberExpression('.', expr, property);
                }
                if (marker != null)
                {
                    marker.End(_index, _lineNumber, _lineStart);
                    marker.Apply(expr, _extra, PostProcess);
                }
            }

            return expr;
        }

        private Expression ParseLeftHandSideExpression()
        {
            LocationMarker marker = CreateLocationMarker();

            var previousAllowIn = _state.AllowIn;
            Expression expr = MatchKeyword("new") ? ParseNewExpression() : ParsePrimaryExpression();
            _state.AllowIn = previousAllowIn;

            while (Match(".") || Match("["))
            {
                if (Match("["))
                {
                    Expression property = ParseComputedMember();
                    expr = CreateMemberExpression('[', expr, property);
                }
                else
                {
                    Identifier property = ParseNonComputedMember();
                    expr = CreateMemberExpression('.', expr, property);
                }
                if (marker != null)
                {
                    marker.End(_index, _lineNumber, _lineStart);
                    marker.Apply(expr, _extra, PostProcess);
                }
            }

            return expr;
        }

        // 11.3 Postfix Expressions

        private Expression ParsePostfixExpression()
        {
            MarkStart();
            Expression expr = ParseLeftHandSideExpressionAllowCall();

            if (_lookahead.Type == Tokens.Punctuator)
            {
                if ((Match("++") || Match("--")) && !PeekLineTerminator())
                {
                    // 11.3.1, 11.3.2
                    if (_strict && expr.Type == SyntaxNodes.Identifier && IsRestrictedWord(((Identifier) expr).Name))
                    {
                        ThrowErrorTolerant(Token.Empty, Messages.StrictLHSPostfix);
                    }

                    if (!isLeftHandSide(expr))
                    {
                        ThrowErrorTolerant(Token.Empty, Messages.InvalidLHSInAssignment);
                    }

                    Token token = Lex();
                    expr = CreatePostfixExpression((string) token.Value, expr);
                }
            }

            return MarkEndIf(expr);
        }

        // 11.4 Unary Operators

        private Expression ParseUnaryExpression()
        {
            Expression expr;

            MarkStart();

            if (_lookahead.Type != Tokens.Punctuator && _lookahead.Type != Tokens.Keyword)
            {
                expr = ParsePostfixExpression();
            }
            else if (Match("++") || Match("--"))
            {
                Token token = Lex();
                expr = ParseUnaryExpression();
                // 11.4.4, 11.4.5
                if (_strict && expr.Type == SyntaxNodes.Identifier && IsRestrictedWord(((Identifier) expr).Name))
                {
                    ThrowErrorTolerant(Token.Empty, Messages.StrictLHSPrefix);
                }

                if (!isLeftHandSide(expr))
                {
                    ThrowErrorTolerant(Token.Empty, Messages.InvalidLHSInAssignment);
                }

                expr = CreateUnaryExpression((string) token.Value, expr);
            }
            else if (Match("+") || Match("-") || Match("~") || Match("!"))
            {
                Token token = Lex();
                expr = ParseUnaryExpression();
                expr = CreateUnaryExpression((string) token.Value, expr);
            }
            else if (MatchKeyword("delete") || MatchKeyword("void") || MatchKeyword("typeof"))
            {
                Token token = Lex();
                expr = ParseUnaryExpression();
                UnaryExpression unaryExpr = CreateUnaryExpression((string) token.Value, expr);
                if (_strict && unaryExpr.Operator == UnaryOperator.Delete && unaryExpr.Argument.Type == SyntaxNodes.Identifier)
                {
                    ThrowErrorTolerant(Token.Empty, Messages.StrictDelete);
                }
                expr = unaryExpr;
            }
            else
            {
                expr = ParsePostfixExpression();
            }

            return MarkEndIf(expr);
        }

        private int binaryPrecedence(Token token, bool allowIn)
        {
            int prec = 0;

            if (token.Type != Tokens.Punctuator && token.Type != Tokens.Keyword)
            {
                return 0;
            }

            switch ((string) token.Value)
            {
                case "||":
                    prec = 1;
                    break;

                case "&&":
                    prec = 2;
                    break;

                case "|":
                    prec = 3;
                    break;

                case "^":
                    prec = 4;
                    break;

                case "&":
                    prec = 5;
                    break;

                case "==":
                case "!=":
                case "===":
                case "!==":
                    prec = 6;
                    break;

                case "<":
                case ">":
                case "<=":
                case ">=":
                case "instanceof":
                    prec = 7;
                    break;

                case "in":
                    prec = allowIn ? 7 : 0;
                    break;

                case "<<":
                case ">>":
                case ">>>":
                    prec = 8;
                    break;

                case "+":
                case "-":
                    prec = 9;
                    break;

                case "*":
                case "/":
                case "%":
                    prec = 11;
                    break;
            }

            return prec;
        }

        // 11.5 Multiplicative Operators
        // 11.6 Additive Operators
        // 11.7 Bitwise Shift Operators
        // 11.8 Relational Operators
        // 11.9 Equality Operators
        // 11.10 Binary Bitwise Operators
        // 11.11 Binary Logical Operators

        private Expression ParseBinaryExpression()
        {
            Expression expr;

            LocationMarker marker = CreateLocationMarker();
            Expression left = ParseUnaryExpression();

            Token token = _lookahead;
            int prec = binaryPrecedence(token, _state.AllowIn);
            if (prec == 0)
            {
                return left;
            }
            token.Precedence = prec;
            Lex();

            var markers = new Stack<LocationMarker>( new [] {marker, CreateLocationMarker()});
            Expression right = ParseUnaryExpression();

            var stack = new List<object>( new object[] {left, token, right});

            while ((prec = binaryPrecedence(_lookahead, _state.AllowIn)) > 0)
            {
                // Reduce: make a binary expression from the three topmost entries.
                while ((stack.Count > 2) && (prec <= ((Token) stack[stack.Count - 2]).Precedence))
                {
                    right = (Expression) stack.Pop();
                    var op = (string) ((Token) stack.Pop()).Value;
                    left = (Expression) stack.Pop();
                    expr = CreateBinaryExpression(op, left, right);
                    markers.Pop();
                    marker = markers.Pop();
                    if (marker != null)
                    {
                        marker.End(_index, _lineNumber, _lineStart);
                        marker.Apply(expr, _extra, PostProcess);
                    }
                    stack.Push(expr);
                    markers.Push(marker);
                }

                // Shift.
                token = Lex();
                token.Precedence = prec;
                stack.Push(token);
                markers.Push(CreateLocationMarker());
                expr = ParseUnaryExpression();
                stack.Push(expr);
            }

            // Final reduce to clean-up the stack.
            int i = stack.Count - 1;
            expr = (Expression) stack[i];
            markers.Pop();
            while (i > 1)
            {
                expr = CreateBinaryExpression((string) ((Token) stack[i - 1]).Value, (Expression) stack[i - 2], expr);
                i -= 2;
                marker = markers.Pop();
                if (marker != null)
                {
                    marker.End(_index, _lineNumber, _lineStart);
                    marker.Apply(expr, _extra, PostProcess);
                }
            }

            return expr;
        }


        // 11.12 Conditional Operator

        private Expression ParseConditionalExpression()
        {
            MarkStart();
            Expression expr = ParseBinaryExpression();

            if (Match("?"))
            {
                Lex();
                bool previousAllowIn = _state.AllowIn;
                _state.AllowIn = true;
                Expression consequent = ParseAssignmentExpression();
                _state.AllowIn = previousAllowIn;
                Expect(":");
                Expression alternate = ParseAssignmentExpression();

                expr = MarkEnd(CreateConditionalExpression(expr, consequent, alternate));
            }
            else
            {
                MarkEnd(new SyntaxNode());
            }

            return expr;
        }

        // 11.13 Assignment Operators

        private Expression ParseAssignmentExpression()
        {
            Expression left;

            Token token = _lookahead;
            MarkStart();
            Expression expr = left = ParseConditionalExpression();

            if (MatchAssign())
            {
                // LeftHandSideExpression

                // Ignore issue as it needs to throw a ReferenceError instead of a SyntaxError
                //if (!isLeftHandSide(left))
                //{
                //    ThrowErrorTolerant(Token.Empty, Messages.InvalidLHSInAssignment);
                //}

                // 11.13.1
                if (_strict && left.Type == SyntaxNodes.Identifier && IsRestrictedWord(((Identifier) left).Name))
                {
                    ThrowErrorTolerant(token, Messages.StrictLHSAssignment);
                }

                token = Lex();
                Expression right = ParseAssignmentExpression();
                expr = CreateAssignmentExpression((string) token.Value, left, right);
            }

            return MarkEndIf(expr);
        }

        // 11.14 Comma Operator

        private Expression ParseExpression()
        {
            MarkStart();
            Expression expr = ParseAssignmentExpression();

            if (Match(","))
            {
                expr = CreateSequenceExpression(new List<Expression> {expr});

                while (_index < _length)
                {
                    if (!Match(","))
                    {
                        break;
                    }
                    Lex();
                    ((SequenceExpression) expr).Expressions.Add(ParseAssignmentExpression());
                }
            }

            return MarkEndIf(expr);
        }

        // 12.1 Block

        private IEnumerable<Statement> ParseStatementList()
        {
            var list = new List<Statement>();

            while (_index < _length)
            {
                if (Match("}"))
                {
                    break;
                }
                Statement statement = ParseSourceElement();
                if (statement == null)
                {
                    break;
                }
                list.Add(statement);
            }

            return list;
        }

        private BlockStatement ParseBlock()
        {
            MarkStart();
            Expect("{");

            IEnumerable<Statement> block = ParseStatementList();

            Expect("}");

            return MarkEnd(CreateBlockStatement(block));
        }

        // 12.2 Variable Statement

        private Identifier ParseVariableIdentifier()
        {
            MarkStart();
            Token token = Lex();

            if (token.Type != Tokens.Identifier)
            {
                ThrowUnexpected(token);
            }

            return MarkEnd(CreateIdentifier((string) token.Value));
        }

        private VariableDeclarator ParseVariableDeclaration(string kind)
        {
            Expression init = null;

            MarkStart();
            Identifier id = ParseVariableIdentifier();

            // 12.2.1
            if (_strict && IsRestrictedWord(id.Name))
            {
                ThrowErrorTolerant(Token.Empty, Messages.StrictVarName);
            }

            if ("const".Equals(kind))
            {
                Expect("=");
                init = ParseAssignmentExpression();
            }
            else if (Match("="))
            {
                Lex();
                init = ParseAssignmentExpression();
            }

            return MarkEnd(CreateVariableDeclarator(id, init));
        }

        private IEnumerable<VariableDeclarator> ParseVariableDeclarationList(string kind)
        {
            var list = new List<VariableDeclarator>();

            do
            {
                list.Add(ParseVariableDeclaration(kind));
                if (!Match(","))
                {
                    break;
                }
                Lex();
            } while (_index < _length);

            return list;
        }

        private VariableDeclaration ParseVariableStatement()
        {
            ExpectKeyword("var");

            IEnumerable<VariableDeclarator> declarations = ParseVariableDeclarationList(null);

            ConsumeSemicolon();

            return CreateVariableDeclaration(declarations, "var");
        }

        // kind may be `const` or `let`
        // Both are experimental and not in the specification yet.
        // see http://wiki.ecmascript.org/doku.php?id=harmony:const
        // and http://wiki.ecmascript.org/doku.php?id=harmony:let
        private VariableDeclaration ParseConstLetDeclaration(string kind)
        {
            MarkStart();

            ExpectKeyword(kind);

            IEnumerable<VariableDeclarator> declarations = ParseVariableDeclarationList(kind);

            ConsumeSemicolon();

            return MarkEnd(CreateVariableDeclaration(declarations, kind));
        }

        // 12.3 Empty Statement

        private EmptyStatement ParseEmptyStatement()
        {
            Expect(";");
            return CreateEmptyStatement();
        }

        // 12.4 Expression Statement

        private ExpressionStatement ParseExpressionStatement()
        {
            Expression expr = ParseExpression();
            ConsumeSemicolon();
            return CreateExpressionStatement(expr);
        }

        // 12.5 If statement

        private IfStatement ParseIfStatement()
        {
            Statement alternate;

            ExpectKeyword("if");

            Expect("(");

            Expression test = ParseExpression();

            Expect(")");

            Statement consequent = ParseStatement();

            if (MatchKeyword("else"))
            {
                Lex();
                alternate = ParseStatement();
            }
            else
            {
                alternate = null;
            }

            return CreateIfStatement(test, consequent, alternate);
        }

        // 12.6 Iteration Statements

        private DoWhileStatement ParseDoWhileStatement()
        {
            ExpectKeyword("do");

            bool oldInIteration = _state.InIteration;
            _state.InIteration = true;

            Statement body = ParseStatement();

            _state.InIteration = oldInIteration;

            ExpectKeyword("while");

            Expect("(");

            Expression test = ParseExpression();

            Expect(")");

            if (Match(";"))
            {
                Lex();
            }

            return CreateDoWhileStatement(body, test);
        }

        private WhileStatement ParseWhileStatement()
        {
            ExpectKeyword("while");

            Expect("(");

            Expression test = ParseExpression();

            Expect(")");

            bool oldInIteration = _state.InIteration;
            _state.InIteration = true;

            Statement body = ParseStatement();

            _state.InIteration = oldInIteration;

            return CreateWhileStatement(test, body);
        }

        private VariableDeclaration ParseForVariableDeclaration()
        {
            MarkStart();
            Token token = Lex();
            IEnumerable<VariableDeclarator> declarations = ParseVariableDeclarationList(null);

            return MarkEnd(CreateVariableDeclaration(declarations, (string) token.Value));
        }

        private Statement ParseForStatement()
        {
            SyntaxNode init = null, left = null;
            Expression right = null;
            Expression test = null, update = null;

            ExpectKeyword("for");

            Expect("(");

            if (Match(";"))
            {
                Lex();
            }
            else
            {
                if (MatchKeyword("var") || MatchKeyword("let"))
                {
                    _state.AllowIn = false;
                    init = ParseForVariableDeclaration();
                    _state.AllowIn = true;

                    if (init.As<VariableDeclaration>().Declarations.Count() == 1 && MatchKeyword("in"))
                    {
                        Lex();
                        left = init;
                        right = ParseExpression();
                        init = null;
                    }
                }
                else
                {
                    _state.AllowIn = false;
                    init = ParseExpression();
                    _state.AllowIn = true;

                    if (MatchKeyword("in"))
                    {
                        // LeftHandSideExpression
                        if (!isLeftHandSide((Expression) init))
                        {
                            ThrowErrorTolerant(Token.Empty, Messages.InvalidLHSInForIn);
                        }

                        Lex();
                        left = init;
                        right = ParseExpression();
                        init = null;
                    }
                }

                if (left == null)
                {
                    Expect(";");
                }
            }

            if (left == null)
            {
                if (!Match(";"))
                {
                    test = ParseExpression();
                }
                Expect(";");

                if (!Match(")"))
                {
                    update = ParseExpression();
                }
            }

            Expect(")");

            bool oldInIteration = _state.InIteration;
            _state.InIteration = true;

            Statement body = ParseStatement();

            _state.InIteration = oldInIteration;

            return (left == null)
                       ? (Statement) CreateForStatement(init, test, update, body)
                       : CreateForInStatement(left, right, body);
        }

        // 12.7 The continue statement

        private Statement ParseContinueStatement()
        {
            Identifier label = null;

            ExpectKeyword("continue");

            // Optimize the most common form: 'continue;'.
            if (_source.CharCodeAt(_index) == 59)
            {
                Lex();

                if (!_state.InIteration)
                {
                    ThrowError(Token.Empty, Messages.IllegalContinue);
                }

                return CreateContinueStatement(null);
            }

            if (PeekLineTerminator())
            {
                if (!_state.InIteration)
                {
                    ThrowError(Token.Empty, Messages.IllegalContinue);
                }

                return CreateContinueStatement(null);
            }

            if (_lookahead.Type == Tokens.Identifier)
            {
                label = ParseVariableIdentifier();

                string key = "$" + label.Name;
                if (!_state.LabelSet.Contains(key))
                {
                    ThrowError(Token.Empty, Messages.UnknownLabel, label.Name);
                }
            }

            ConsumeSemicolon();

            if (label == null && !_state.InIteration)
            {
                ThrowError(Token.Empty, Messages.IllegalContinue);
            }

            return CreateContinueStatement(label);
        }

        // 12.8 The break statement

        private BreakStatement ParseBreakStatement()
        {
            Identifier label = null;

            ExpectKeyword("break");

            // Catch the very common case first: immediately a semicolon (char #59).
            if (_source.CharCodeAt(_index) == 59)
            {
                Lex();

                if (!(_state.InIteration || _state.InSwitch))
                {
                    ThrowError(Token.Empty, Messages.IllegalBreak);
                }

                return CreateBreakStatement(null);
            }

            if (PeekLineTerminator())
            {
                if (!(_state.InIteration || _state.InSwitch))
                {
                    ThrowError(Token.Empty, Messages.IllegalBreak);
                }

                return CreateBreakStatement(null);
            }

            if (_lookahead.Type == Tokens.Identifier)
            {
                label = ParseVariableIdentifier();

                string key = "$" + label.Name;
                if (!_state.LabelSet.Contains(key))
                {
                    ThrowError(Token.Empty, Messages.UnknownLabel, label.Name);
                }
            }

            ConsumeSemicolon();

            if (label == null && !(_state.InIteration || _state.InSwitch))
            {
                ThrowError(Token.Empty, Messages.IllegalBreak);
            }

            return CreateBreakStatement(label);
        }

        // 12.9 The return statement

        private ReturnStatement ParseReturnStatement()
        {
            Expression argument = null;

            ExpectKeyword("return");

            if (!_state.InFunctionBody)
            {
 //               ThrowErrorTolerant(Token.Empty, Messages.IllegalReturn);
            }

            // 'return' followed by a space and an identifier is very common.
            if (_source.CharCodeAt(_index) == 32)
            {
                if (IsIdentifierStart(_source.CharCodeAt(_index + 1)))
                {
                    argument = ParseExpression();
                    ConsumeSemicolon();
                    return CreateReturnStatement(argument);
                }
            }

            if (PeekLineTerminator())
            {
                return CreateReturnStatement(null);
            }

            if (!Match(";"))
            {
                if (!Match("}") && _lookahead.Type != Tokens.EOF)
                {
                    argument = ParseExpression();
                }
            }

            ConsumeSemicolon();

            return CreateReturnStatement(argument);
        }

        // 12.10 The with statement

        private WithStatement ParseWithStatement()
        {
            if (_strict)
            {
                ThrowErrorTolerant(Token.Empty, Messages.StrictModeWith);
            }

            ExpectKeyword("with");

            Expect("(");

            Expression obj = ParseExpression();

            Expect(")");

            Statement body = ParseStatement();

            return CreateWithStatement(obj, body);
        }

        // 12.10 The swith statement

        private SwitchCase ParseSwitchCase()
        {
            Expression test;
            var consequent = new List<Statement>();

            MarkStart();
            if (MatchKeyword("default"))
            {
                Lex();
                test = null;
            }
            else
            {
                ExpectKeyword("case");
                test = ParseExpression();
            }
            Expect(":");

            while (_index < _length)
            {
                if (Match("}") || MatchKeyword("default") || MatchKeyword("case"))
                {
                    break;
                }
                Statement statement = ParseStatement();
                consequent.Add(statement);
            }

            return MarkEnd(CreateSwitchCase(test, consequent));
        }

        private SwitchStatement ParseSwitchStatement()
        {
            ExpectKeyword("switch");

            Expect("(");

            Expression discriminant = ParseExpression();

            Expect(")");

            Expect("{");

            var cases = new List<SwitchCase>();

            if (Match("}"))
            {
                Lex();
                return CreateSwitchStatement(discriminant, cases);
            }

            bool oldInSwitch = _state.InSwitch;
            _state.InSwitch = true;
            bool defaultFound = false;

            while (_index < _length)
            {
                if (Match("}"))
                {
                    break;
                }
                SwitchCase clause = ParseSwitchCase();
                if (clause.Test == null)
                {
                    if (defaultFound)
                    {
                        ThrowError(Token.Empty, Messages.MultipleDefaultsInSwitch);
                    }
                    defaultFound = true;
                }
                cases.Add(clause);
            }

            _state.InSwitch = oldInSwitch;

            Expect("}");

            return CreateSwitchStatement(discriminant, cases);
        }

        // 12.13 The throw statement

        private ThrowStatement ParseThrowStatement()
        {
            ExpectKeyword("throw");

            if (PeekLineTerminator())
            {
                ThrowError(Token.Empty, Messages.NewlineAfterThrow);
            }

            Expression argument = ParseExpression();

            ConsumeSemicolon();

            return CreateThrowStatement(argument);
        }

        // 12.14 The try statement

        private CatchClause ParseCatchClause()
        {
            MarkStart();
            ExpectKeyword("catch");

            Expect("(");
            if (Match(")"))
            {
                ThrowUnexpected(_lookahead);
            }

            Identifier param = ParseVariableIdentifier();
            // 12.14.1
            if (_strict && IsRestrictedWord(param.Name))
            {
                ThrowErrorTolerant(Token.Empty, Messages.StrictCatchVariable);
            }

            Expect(")");
            BlockStatement body = ParseBlock();
            return MarkEnd(CreateCatchClause(param, body));
        }

        private TryStatement ParseTryStatement()
        {
            var handlers = new List<CatchClause>();
            Statement finalizer = null;

            ExpectKeyword("try");

            BlockStatement block = ParseBlock();

            if (MatchKeyword("catch"))
            {
                handlers.Add(ParseCatchClause());
            }

            if (MatchKeyword("finally"))
            {
                Lex();
                finalizer = ParseBlock();
            }

            if (handlers.Count == 0 && finalizer == null)
            {
                ThrowError(Token.Empty, Messages.NoCatchOrFinally);
            }

            return CreateTryStatement(block, new Statement[0], handlers, finalizer);
        }

        // 12.15 The debugger statement

        private DebuggerStatement ParseDebuggerStatement()
        {
            ExpectKeyword("debugger");

            ConsumeSemicolon();

            return CreateDebuggerStatement();
        }

        // 12 Statements

        private Statement ParseStatement()
        {
            Tokens type = _lookahead.Type;

            if (type == Tokens.EOF)
            {
                ThrowUnexpected(_lookahead);
            }

            MarkStart();

            if (type == Tokens.Punctuator)
            {
                switch ((string) _lookahead.Value)
                {
                    case ";":
                        return MarkEnd(ParseEmptyStatement());
                    case "{":
                        return MarkEnd(ParseBlock());
                    case "(":
                        return MarkEnd(ParseExpressionStatement());
                }
            }

            if (type == Tokens.Keyword)
            {
                switch ((string) _lookahead.Value)
                {
                    case "break":
                        return MarkEnd(ParseBreakStatement());
                    case "continue":
                        return MarkEnd(ParseContinueStatement());
                    case "debugger":
                        return MarkEnd(ParseDebuggerStatement());
                    case "do":
                        return MarkEnd(ParseDoWhileStatement());
                    case "for":
                        return MarkEnd(ParseForStatement());
                    case "function":
                        return MarkEnd(ParseFunctionDeclaration());
                    case "if":
                        return MarkEnd(ParseIfStatement());
                    case "return":
                        return MarkEnd(ParseReturnStatement());
                    case "switch":
                        return MarkEnd(ParseSwitchStatement());
                    case "throw":
                        return MarkEnd(ParseThrowStatement());
                    case "try":
                        return MarkEnd(ParseTryStatement());
                    case "var":
                        return MarkEnd(ParseVariableStatement());
                    case "while":
                        return MarkEnd(ParseWhileStatement());
                    case "with":
                        return MarkEnd(ParseWithStatement());
                }
            }

            Expression expr = ParseExpression();

            // 12.12 Labelled Statements
            if ((expr.Type == SyntaxNodes.Identifier) && Match(":"))
            {
                Lex();

                string key = "$" + ((Identifier) expr).Name;
                if (_state.LabelSet.Contains(key))
                {
                    ThrowError(Token.Empty, Messages.Redeclaration, "Label", ((Identifier) expr).Name);
                }

                _state.LabelSet.Add(key);
                Statement labeledBody = ParseStatement();
                _state.LabelSet.Remove(key);
                return MarkEnd(CreateLabeledStatement((Identifier) expr, labeledBody));
            }

            ConsumeSemicolon();

            return MarkEnd(CreateExpressionStatement(expr));
        }

        // 13 Function Definition

        private Statement ParseFunctionSourceElements()
        {
            Token firstRestricted = Token.Empty;

            var sourceElements = new List<Statement>();

            MarkStart();
            Expect("{");

            while (_index < _length)
            {
                if (_lookahead.Type != Tokens.StringLiteral)
                {
                    break;
                }
                Token token = _lookahead;

                Statement sourceElement = ParseSourceElement();
                sourceElements.Add(sourceElement);
                if (((ExpressionStatement) sourceElement).Expression.Type != SyntaxNodes.Literal)
                {
                    // this is not directive
                    break;
                }
                string directive = _source.Slice(token.Range[0] + 1, token.Range[1] - 1);
                if (directive == "use strict")
                {
                    _strict = true;
                    if (firstRestricted != Token.Empty)
                    {
                        ThrowErrorTolerant(firstRestricted, Messages.StrictOctalLiteral);
                    }
                }
                else
                {
                    if (firstRestricted == Token.Empty && token.Octal)
                    {
                        firstRestricted = token;
                    }
                }
            }

            HashSet<string> oldLabelSet = _state.LabelSet;
            bool oldInIteration = _state.InIteration;
            bool oldInSwitch = _state.InSwitch;
            bool oldInFunctionBody = _state.InFunctionBody;

            _state.LabelSet = new HashSet<string>();
            _state.InIteration = false;
            _state.InSwitch = false;
            _state.InFunctionBody = true;

            while (_index < _length)
            {
                if (Match("}"))
                {
                    break;
                }
                Statement sourceElement = ParseSourceElement();
                if (sourceElement == null)
                {
                    break;
                }
                sourceElements.Add(sourceElement);
            }

            Expect("}");

            _state.LabelSet = oldLabelSet;
            _state.InIteration = oldInIteration;
            _state.InSwitch = oldInSwitch;
            _state.InFunctionBody = oldInFunctionBody;

            return MarkEnd(CreateBlockStatement(sourceElements));
        }

        private ParsedParameters ParseParams(Token firstRestricted)
        {
            string message = null;
            Token stricted = Token.Empty;
            var parameters = new List<Identifier>();

            Expect("(");

            if (!Match(")"))
            {
                var paramSet = new HashSet<string>();
                while (_index < _length)
                {
                    Token token = _lookahead;
                    Identifier param = ParseVariableIdentifier();
                    string key = '$' + (string) token.Value;
                    if (_strict)
                    {
                        if (IsRestrictedWord((string) token.Value))
                        {
                            stricted = token;
                            message = Messages.StrictParamName;
                        }
                        if (paramSet.Contains(key))
                        {
                            stricted = token;
                            message = Messages.StrictParamDupe;
                        }
                    }
                    else if (firstRestricted == Token.Empty)
                    {
                        if (IsRestrictedWord((string) token.Value))
                        {
                            firstRestricted = token;
                            message = Messages.StrictParamName;
                        }
                        else if (IsStrictModeReservedWord((string) token.Value))
                        {
                            firstRestricted = token;
                            message = Messages.StrictReservedWord;
                        }
                        else if (paramSet.Contains(key))
                        {
                            firstRestricted = token;
                            message = Messages.StrictParamDupe;
                        }
                    }
                    parameters.Add(param);
                    paramSet.Add(key);
                    if (Match(")"))
                    {
                        break;
                    }
                    Expect(",");
                }
            }

            Expect(")");

            return new ParsedParameters
                {
                    Parameters = parameters,
                    Stricted = stricted,
                    FirstRestricted = firstRestricted,
                    Message = message
                };
        }

        private Statement ParseFunctionDeclaration()
        {
            EnterVariableScope();
            EnterFunctionScope();

            Token firstRestricted = Token.Empty;
            string message = null;

            MarkStart();

            ExpectKeyword("function");
            Token token = _lookahead;
            Identifier id = ParseVariableIdentifier();
            if (_strict)
            {
                if (IsRestrictedWord((string) token.Value))
                {
                    ThrowErrorTolerant(token, Messages.StrictFunctionName);
                }
            }
            else
            {
                if (IsRestrictedWord((string) token.Value))
                {
                    firstRestricted = token;
                    message = Messages.StrictFunctionName;
                }
                else if (IsStrictModeReservedWord((string) token.Value))
                {
                    firstRestricted = token;
                    message = Messages.StrictReservedWord;
                }
            }

            ParsedParameters tmp = ParseParams(firstRestricted);
            IEnumerable<Identifier> parameters = tmp.Parameters;
            Token stricted = tmp.Stricted;
            firstRestricted = tmp.FirstRestricted;
            if (tmp.Message != null)
            {
                message = tmp.Message;
            }

            bool previousStrict = _strict;
            Statement body = ParseFunctionSourceElements();
            if (_strict && firstRestricted != Token.Empty)
            {
                ThrowError(firstRestricted, message);
            }
            if (_strict && stricted != Token.Empty)
            {
                ThrowErrorTolerant(stricted, message);
            }
            bool functionStrict = _strict;
            _strict = previousStrict;

            return MarkEnd(CreateFunctionDeclaration(id, parameters, new Expression[0], body, functionStrict));
        }

        private void EnterVariableScope()
        {
            _variableScopes.Push(new VariableScope());
        }

        private IList<VariableDeclaration> LeaveVariableScope()
        {
            return _variableScopes.Pop().VariableDeclarations;
        }

        private void EnterFunctionScope()
        {
            _functionScopes.Push(new FunctionScope());
        }

        private IList<FunctionDeclaration> LeaveFunctionScope()
        {
            return _functionScopes.Pop().FunctionDeclarations;
        }

        private FunctionExpression ParseFunctionExpression()
        {
            EnterVariableScope();
            EnterFunctionScope();

            Token firstRestricted = Token.Empty;
            string message = null;
            Identifier id = null;

            MarkStart();
            ExpectKeyword("function");

            if (!Match("("))
            {
                Token token = _lookahead;
                id = ParseVariableIdentifier();
                if (_strict)
                {
                    if (IsRestrictedWord((string) token.Value))
                    {
                        ThrowErrorTolerant(token, Messages.StrictFunctionName);
                    }
                }
                else
                {
                    if (IsRestrictedWord((string) token.Value))
                    {
                        firstRestricted = token;
                        message = Messages.StrictFunctionName;
                    }
                    else if (IsStrictModeReservedWord((string) token.Value))
                    {
                        firstRestricted = token;
                        message = Messages.StrictReservedWord;
                    }
                }
            }

            ParsedParameters tmp = ParseParams(firstRestricted);
            IEnumerable<Identifier> parameters = tmp.Parameters;
            Token stricted = tmp.Stricted;
            firstRestricted = tmp.FirstRestricted;
            if (tmp.Message != null)
            {
                message = tmp.Message;
            }

            bool previousStrict = _strict;
            Statement body = ParseFunctionSourceElements();
            if (_strict && firstRestricted != Token.Empty)
            {
                ThrowError(firstRestricted, message);
            }
            if (_strict && stricted != Token.Empty)
            {
                ThrowErrorTolerant(stricted, message);
            }
            bool functionStrict = _strict;
            _strict = previousStrict;

            return MarkEnd(CreateFunctionExpression(id, parameters, new Expression[0], body, functionStrict));
        }

        // 14 Program

        private Statement ParseSourceElement()
        {
            if (_lookahead.Type == Tokens.Keyword)
            {
                switch ((string) _lookahead.Value)
                {
                    case "const":
                    case "let":
                        return ParseConstLetDeclaration((string) _lookahead.Value);
                    case "function":
                        return ParseFunctionDeclaration();
                    default:
                        return ParseStatement();
                }
            }

            if (_lookahead.Type != Tokens.EOF)
            {
                return ParseStatement();
            }

            return null;
        }

        private ICollection<Statement> ParseSourceElements()
        {
            var sourceElements = new List<Statement>();
            Token firstRestricted = Token.Empty;
            Statement sourceElement;

            while (_index < _length)
            {
                Token token = _lookahead;
                if (token.Type != Tokens.StringLiteral)
                {
                    break;
                }

                sourceElement = ParseSourceElement();
                sourceElements.Add(sourceElement);
                if (((ExpressionStatement) sourceElement).Expression.Type != SyntaxNodes.Literal)
                {
                    // this is not directive
                    break;
                }
                string directive = _source.Slice(token.Range[0] + 1, token.Range[1] - 1);
                if (directive == "use strict")
                {
                    _strict = true;
                    if (firstRestricted != Token.Empty)
                    {
                        ThrowErrorTolerant(firstRestricted, Messages.StrictOctalLiteral);
                    }
                }
                else
                {
                    if (firstRestricted == Token.Empty && token.Octal)
                    {
                        firstRestricted = token;
                    }
                }
            }

            while (_index < _length)
            {
                sourceElement = ParseSourceElement();
                if (sourceElement == null)
                {
                    break;
                }
                sourceElements.Add(sourceElement);
            }
            return sourceElements;
        }

        private Program ParseProgram()
        {
            EnterVariableScope();
            EnterFunctionScope();
            
            MarkStart();
            Peek();
            ICollection<Statement> body = ParseSourceElements();
            return MarkEnd(CreateProgram(body, _strict));
        }

        private LocationMarker CreateLocationMarker()
        {
            if (!_extra.Loc.HasValue && _extra.Range.Length == 0)
            {
                return null;
            }

            SkipComment();

            return new LocationMarker(_index, _lineNumber, _lineStart);
        }

        public Program Parse(string code)
        {
            return Parse(code, null);
        }

        public Program Parse(string code, ParserOptions options)
        {
            Program program;

            _source = code;
            _index = 0;
            _lineNumber = (_source.Length > 0) ? 1 : 0;
            _lineStart = 0;
            _length = _source.Length;
            _lookahead = null;
            _state = new State
            {
                AllowIn = true,
                LabelSet = new HashSet<string>(),
                InFunctionBody = false,
                InIteration = false,
                InSwitch = false,
                LastCommentStart = -1,
                MarkerStack = new Stack<int>()
            };

            _extra = new Extra
            {
                Range = new int[0],
                Loc = 0,

            };

            if (options != null)
            {
                if (!String.IsNullOrEmpty(options.Source))
                {
                    _extra.Source = options.Source;
                }

                if (options.Tokens)
                {
                    _extra.Tokens = new List<Token>();
                }

                if (options.Comment)
                {
                    _extra.Comments = new List<Comment>();
                }

                if (options.Tolerant)
                {
                    _extra.Errors = new List<ParserException>();
                }
            }

            try
            {
                program = ParseProgram();

                if (_extra.Comments != null)
                {
                    program.Comments = _extra.Comments;
                }

                if (_extra.Tokens != null)
                {
                    program.Tokens = _extra.Tokens;
                }

                if (_extra.Errors != null)
                {
                    program.Errors = _extra.Errors;
                }
            }
            finally
            {
                _extra = new Extra();
            }

            return program;
        }

        public FunctionExpression ParseFunctionExpression(string functionExpression)
        {
            _source = functionExpression;
            _index = 0;
            _lineNumber = (_source.Length > 0) ? 1 : 0;
            _lineStart = 0;
            _length = _source.Length;
            _lookahead = null;
            _state = new State
            {
                AllowIn = true,
                LabelSet = new HashSet<string>(),
                InFunctionBody = true,
                InIteration = false,
                InSwitch = false,
                LastCommentStart = -1,
                MarkerStack = new Stack<int>()
            };

            _extra = new Extra
            {
                Range = new int[0],
                Loc = 0,

            };

            _strict = false;
            Peek();
            return ParseFunctionExpression();
        }

        private class Extra
        {
            public int? Loc;
            public int[] Range;
            public string Source;

            public List<Comment> Comments;
            public List<Token> Tokens;
            public List<ParserException> Errors;
        }

        private class LocationMarker
        {
            private readonly int[] _marker;

            public LocationMarker(int index, int lineNumber, int lineStart)
            {
                _marker = new[] {index, lineNumber, index - lineStart, 0, 0, 0};
            }

            public void End(int index, int lineNumber, int lineStart)
            {
                _marker[3] = index;
                _marker[4] = lineNumber;
                _marker[5] = index - lineStart;
            }

            public void Apply(SyntaxNode node, Extra extra, Func<SyntaxNode, SyntaxNode> postProcess)
            {
                if (extra.Range.Length > 0)
                {
                    node.Range = new[] {_marker[0], _marker[3]};
                }
                if (extra.Loc.HasValue)
                {
                    node.Location = new Location
                        {
                            Start = new Position
                                {
                                    Line = _marker[1],
                                    Column = _marker[2]
                                },
                            End = new Position
                                {
                                    Line = _marker[4],
                                    Column = _marker[5]
                                }
                        };
                }

                node = postProcess(node);
            }
        };

        private struct ParsedParameters
        {
            public Token FirstRestricted;
            public string Message;
            public IEnumerable<Identifier> Parameters;
            public Token Stricted;
        }
    }
}

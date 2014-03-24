namespace Jint.Parser
{
    public class SourceReader
    {
        private readonly string _source;
        private readonly char[] _reader;

        public SourceReader(string source)
        {
            _source = source;
            _reader = source.ToCharArray();
        }

        public char CharCodeAt(int index)
        {
            return _reader[index];
        }

        public string Slice(int start, int end)
        {
            return _source.Substring(start, end - start);
        }

    }
}

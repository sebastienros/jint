using Esprima;
using System.Collections;
using System.Collections.Generic;

namespace Jint.Runtime.Debugger
{
    public class BreakPointCollection : ICollection<BreakPoint>
    {
        private readonly List<BreakPoint> _breakPoints = new List<BreakPoint>();

        public BreakPointCollection()
        {
        }

        public int Count => _breakPoints.Count;

        public bool IsReadOnly => false;

        public void Add(BreakPoint breakPoint)
        {
            _breakPoints.Add(breakPoint);
        }

        public bool Remove(BreakPoint breakPoint)
        {
            return _breakPoints.Remove(breakPoint);
        }

        public void Clear()
        {
            _breakPoints.Clear();
        }

        public bool Contains(BreakPoint item)
        {
            return _breakPoints.Contains(item);
        }

        public void CopyTo(BreakPoint[] array, int arrayIndex)
        {
            _breakPoints.CopyTo(array, arrayIndex);
        }

        public IEnumerator<BreakPoint> GetEnumerator()
        {
            return _breakPoints.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _breakPoints.GetEnumerator();
        }

        internal BreakPoint FindMatch(Engine engine, Location location)
        {
            foreach (var breakPoint in _breakPoints)
            {
                if (breakPoint.Source != null)
                {
                    if (breakPoint.Source != location.Source)
                    {
                        continue;
                    }
                }

                bool afterStart = breakPoint.Line == location.Start.Line &&
                                 breakPoint.Column >= location.Start.Column;

                if (!afterStart)
                {
                    continue;
                }

                bool beforeEnd = breakPoint.Line < location.End.Line
                            || (breakPoint.Line == location.End.Line &&
                                breakPoint.Column <= location.End.Column);

                if (!beforeEnd)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(breakPoint.Condition))
                {
                    var completionValue = engine.Evaluate(breakPoint.Condition);
                    if (!completionValue.AsBoolean())
                    {
                        continue;
                    }
                }

                return breakPoint;
            }

            return null;
        }
    }
}

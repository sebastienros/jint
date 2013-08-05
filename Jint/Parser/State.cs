using System.Collections.Generic;

namespace Jint.Parser
{
    public struct State
    {
        public int LastCommentStart;
        public bool AllowIn;
        public HashSet<string> LabelSet;
        public bool InFunctionBody;
        public bool InIteration;
        public bool InSwitch;
        public Stack<int> MarkerStack;
    }
}

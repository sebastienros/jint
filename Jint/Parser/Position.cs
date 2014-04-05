namespace Jint.Parser
{
    public class Position
    {
        public int Line = -1;
        public int Column = -1;

        public Position Clone()
        {
            Position ret = new Position();

            ret.Line = this.Line;
            ret.Column = this.Column;

            return ret;
        }
    }
}

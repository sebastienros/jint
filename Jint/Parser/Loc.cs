namespace Jint.Parser
{
    public class Location
    {
        public Position Start = new Position();
        public Position End = new Position();
        public string Source = string.Empty;

        public Location Clone()
        {
            Location ret = new Location();

            ret.Source = this.Source;
            ret.Start = this.Start.Clone();
            ret.End = this.End.Clone();

            return ret;
        }

        public bool IsInitialized
        {
            get
            {
                return (this.Start.Line >= 0 && this.Start.Column >= 0);
            }
        }
    }
}

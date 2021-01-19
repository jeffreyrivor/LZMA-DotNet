namespace Lzma
{
    struct State
    {
        public uint Index { get; private set; }

        public bool IsCharState() => Index < 7;

        public void UpdateChar()
        {
            if (Index < 4)
            {
                Index = 0;
            }
            else
            {
                Index -= Index < 10 ? 3u : 6u;
            }
        }

        public void UpdateMatch()
        {
            Index = Index < 7 ? 7u : 10u;
        }

        public void UpdateRep()
        {
            Index = Index < 7 ? 8u : 11u;
        }

        public void UpdateShortRep()
        {
            Index = Index < 7 ? 9u : 11u;
        }
    }
}

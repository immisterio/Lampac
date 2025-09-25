namespace Shared.Models.Templates
{
    public class SegmentTpl
    {
        private readonly List<(int start, int end)> ads;
        private readonly List<(int start, int end)> skips;
        
        public bool IsEmpty() => ads.Count == 0 && skips.Count == 0;

        public void ad(int start, int end)
        {
            if (start >= 0 && end >= 0 && end >= start)
                ads.Add((start, end));
        }

        public void skip(int start, int end)
        {
            if (start >= 0 && end >= 0 && end >= start)
                skips.Add((start, end));
        }

        public Dictionary<string, List<(int start, int end)>> ToObject()
        {
            if (IsEmpty())
                return null;

            return new Dictionary<string, List<(int start, int end)>>()
            {
                { "ad", ads },
                { "skip", skips }
            };
        }
    }
}

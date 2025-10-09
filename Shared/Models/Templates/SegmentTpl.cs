namespace Shared.Models.Templates
{
    public struct SegmentTpl
    {
        public List<(int start, int end)> ads { get; set; }

        public List<(int start, int end)> skips { get; set; }

        public SegmentTpl()
        {
            ads = new List<(int, int)>();
            skips = new List<(int, int)>();
        }

        public bool IsEmpty() => ads.Count == 0 && skips.Count == 0;

        public void ad(int start, int end)
        {
            if (start >= 0 && end >= 0 && end >= start)
                ads.Add((start == 0 ? 1 : start, end));
        }

        public void skip(int start, int end)
        {
            if (start >= 0 && end >= 0 && end >= start)
                skips.Add((start == 0 ? 1 : start, end));
        }

        public object ToObject()
        {
            if (IsEmpty())
                return null;

            var adList = ads.Select(i => new { i.start, i.end }).ToList();
            var skipList = skips.Select(i => new { i.start, i.end }).ToList();

            return new { ad = adList, skip = skipList };
        }
    }
}

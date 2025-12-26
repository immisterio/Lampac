namespace Shared.Models.Templates
{
    public interface ITplResult
    {
        public bool IsEmpty();

        public string ToHtml();

        public string ToJson();
    }
}

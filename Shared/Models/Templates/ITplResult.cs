namespace Shared.Models.Templates
{
    public interface ITplResult
    {
        public string ToHtml();

        public string ToJson();
    }
}

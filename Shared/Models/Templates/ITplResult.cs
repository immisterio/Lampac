using System.Text;

namespace Shared.Models.Templates
{
    public interface ITplResult
    {
        public bool IsEmpty { get; }

        public int Length { get; }

        public string ToHtml();

        public StringBuilder ToBuilderHtml();

        public string ToJson();

        public StringBuilder ToBuilderJson();
    }
}

using Shared.Model.Base;

namespace Shared.Model.SISI.NextHUB
{
    public class NxtSettings : BaseSettings
    {
        public MenuSettings menu { get; set; }

        public ListSettings search { get; set; }

        public ListSettings list { get; set; }

        public ContentParseSettings contentParse { get; set; }

        public ViewSettings view { get; set; }
    }
}

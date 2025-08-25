using Shared.Models.Base;

namespace Shared.Models.Module
{
    public class InitializationModel
    {
        public InitializationModel(BaseSettings init, bool? rch)
        {
            this.init = init;
            this.rch = rch;
        }

        public BaseSettings init { get; set; }
        public bool? rch { get; set; }
    }
}

namespace Shared.Model.SISI
{
    public struct OnErrorResult
    {
        public OnErrorResult(in string msg)
        {
            this.msg = msg;
        }

        public bool error { get; set; } = true;

        public string msg { get; set; }
    }
}

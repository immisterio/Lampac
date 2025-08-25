namespace Shared.Models.SISI.OnResult
{
    public struct OnErrorResult
    {
        public OnErrorResult(string msg)
        {
            this.msg = msg;
        }

        public bool error { get; set; } = true;

        public string msg { get; set; }
    }
}

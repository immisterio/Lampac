namespace Shared.Models
{
    public class CacheResult<T>
    {
        public bool IsSuccess { get; set; }

        public string ErrorMsg { get; set; }

        public T Value { get; set; }


        public CacheResult<T> Fail(string msg)
        {
            return new CacheResult<T>() { IsSuccess = false, ErrorMsg = msg };
        }
    }
}

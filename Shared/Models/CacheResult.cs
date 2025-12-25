namespace Shared.Models
{
    public class CacheResult<T>
    {
        public bool IsSuccess { get; set; }

        public string ErrorMsg { get; set; }

        public T Value { get; set; }

        public bool refresh_proxy { get; set; }


        public CacheResult<T> Fail(string msg, bool refresh_proxy = false)
        {
            return new CacheResult<T>() 
            { 
                IsSuccess = false, 
                ErrorMsg = msg,
                refresh_proxy = refresh_proxy
            };
        }

        public CacheResult<T> Success(T val, bool refresh_proxy = false)
        {
            return new CacheResult<T>()
            {
                IsSuccess = true,
                Value = val,
                refresh_proxy = refresh_proxy
            };
        }
    }
}

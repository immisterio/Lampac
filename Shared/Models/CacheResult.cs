namespace Shared.Models
{
    public class CacheResult<T>
    {
        public bool IsSuccess { get; set; }

        public string ErrorMsg { get; set; }

        public T Value { get; set; }


        public CacheResult<T> Success(T val)
        {
            if (val == null)
                return new CacheResult<T>() { IsSuccess = false, ErrorMsg = "null" };

            if (val.Equals(default(T)))
                return new CacheResult<T>() { IsSuccess = false, ErrorMsg = "default" };

            if (typeof(T) == typeof(string) && string.IsNullOrEmpty(val.ToString()))
                return new CacheResult<T>() { IsSuccess = false, ErrorMsg = "empty" };

            return new CacheResult<T>() { IsSuccess = true, Value = val };
        }

        public CacheResult<T> Fail(string msg)
        {
            return new CacheResult<T>() { IsSuccess = false, ErrorMsg = msg };
        }
    }
}

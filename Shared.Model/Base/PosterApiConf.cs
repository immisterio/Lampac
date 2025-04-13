namespace Shared.Model.Base
{
    public class PosterApiConf
    {
        public string? host { get; set; }

        public bool rsize { get; set; }

        public int height { get; set; }

        public int width { get; set; }


        /// <summary>
        /// Проксить без изменения размера
        /// </summary>
        public string? bypass { get; set; }

        /// <summary>
        /// Не проксить
        /// </summary>
        public string? disable_rsize { get; set; }
    }
}

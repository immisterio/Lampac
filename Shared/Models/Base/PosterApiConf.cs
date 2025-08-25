namespace Shared.Models.Base
{
    public class PosterApiConf : Iproxy
    {
        public string host { get; set; }

        public bool rsize { get; set; }

        public int height { get; set; }

        public int width { get; set; }


        /// <summary>
        /// Проксить без изменения размера
        /// </summary>
        public string bypass { get; set; }

        /// <summary>
        /// Не проксить
        /// </summary>
        public string disable_rsize { get; set; }


        #region proxy
        public bool useproxy { get; set; }

        public bool useproxystream { get; set; }

        public string globalnameproxy { get; set; }

        public ProxySettings proxy { get; set; }
        #endregion
    }
}

﻿using Lampac.Models.LITE;

namespace Shared.Model.Online.Rezka
{
    public class MovieModel
    {
        /// <summary>
        /// Rezka
        /// </summary>
        public List<ApiModel> links { get; set; }

        /// <summary>
        /// Voidboos
        /// </summary>
        public string url { get; set; }

        public string? subtitlehtml { get; set; }
    }
}

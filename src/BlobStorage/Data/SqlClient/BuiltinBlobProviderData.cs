﻿namespace SenseNet.ContentRepository.Storage.Data.SqlClient
{
    /// <summary>
    /// Blob provider data for the built-in blob provider.
    /// </summary>
    internal class BuiltinBlobProviderData
    {
        /// <summary>
        /// Custom data for the Filestream column.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("FileStreamData")]
        public FileStreamData FileStreamData { get; set; }
    }
}

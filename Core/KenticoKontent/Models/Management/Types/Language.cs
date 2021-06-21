using Core.KenticoKontent.Models.Management.References;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.KenticoKontent.Models.Management.Types
{
    public class Language
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Codename { get; set; }

        [JsonProperty("external_id")]
        public string? ExternalId { get; set; }

        [JsonProperty("is_active")]
        public bool IsActive { get; set; }

        [JsonProperty("is_default")]
        public bool IsDefault { get; set; }

        [JsonProperty("fallback_language")]
        public Reference? FallbackLanguage { get; set; }
    }
}

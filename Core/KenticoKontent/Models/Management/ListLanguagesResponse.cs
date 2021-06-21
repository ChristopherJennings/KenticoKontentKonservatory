using System.Collections.Generic;

using Core.KenticoKontent.Models.Management.Types;

namespace Core.KenticoKontent.Models.Management
{
    public class ListLanguagesResponse : AbstractListingResponse
    {
        public IEnumerable<Language>? Languages { get; set; }
    }
}
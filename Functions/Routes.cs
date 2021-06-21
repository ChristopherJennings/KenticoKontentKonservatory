namespace Functions
{
    public static class Routes
    {
        public const string KontentAzureTranslate = "webhooks/translate";
        public const string HubSpotGetForms = "integrations/hubSpot/getForms";
        public const string KontentDeepClone = "elements/deepClone/{itemCodename}/{languageCodename}";
        public const string KontentChangeType = "elements/changeType/{itemCodename}/{languageCodename}";
        public const string KontentChangeTypeGetTypes = "elements/changeType/getTypes/{itemCodename}";
    }
}
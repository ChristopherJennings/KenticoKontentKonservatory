using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

using Core;
using Core.AzureTranslator.Services;
using Core.KenticoKontent.Models.Management.Elements;
using Core.KenticoKontent.Models.Management.Items;
using Core.KenticoKontent.Models.Management.References;
using Core.KenticoKontent.Models.Webhook;
using Core.KenticoKontent.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Functions.Webhooks
{
    public class KontentAzureTranslate : BaseFunction
    {
        private readonly IWebhookValidator webhookValidator;
        private readonly IKontentRepository kontentRepository;
        private readonly ITranslationService translationService;
        private readonly ITextAnalyzer textAnalyzer;

        public KontentAzureTranslate(
            ILogger<KontentAzureTranslate> logger,
            IWebhookValidator webhookValidator,
            IKontentRepository kontentRepository,
            ITranslationService translationService,
            ITextAnalyzer textAnalyzer
            ) : base(logger)
        {
            this.webhookValidator = webhookValidator;
            this.kontentRepository = kontentRepository;
            this.translationService = translationService;
            this.textAnalyzer = textAnalyzer;
        }

        [FunctionName(nameof(KontentAzureTranslate))]
        public async Task<IActionResult> Run(
            [HttpTrigger(
                "post",
                Route = Routes.KontentAzureTranslate
            )] string body,
            IDictionary<string, string> headers
            )
        {
            try
            {
                var (valid, getWebhook) = webhookValidator.ValidateWebhook(body, headers);

                //if (!valid) return LogUnauthorized();

                var (data, message) = getWebhook();

                if (data.Items == null)
                {
                    throw new ArgumentNullException(nameof(data.Items));
                }

                switch (message.Type)
                {
                    case "content_item_variant":
                        switch (message.Operation)
                        {
                            case "change_workflow_step":

                                var languages = await kontentRepository.ListLanguages();
                                var defaultLanguage = languages.FirstOrDefault(languages => languages.IsDefault);

                                var workflowSteps = await kontentRepository.RetrieveWorkflowSteps();
                                var translationReview = workflowSteps.FirstOrDefault(wf => wf.Name == "Translation Review");
                                var translationReviewId = new IdReference(translationReview.Id!);

                                foreach (var item in data.Items)
                                {
                                    if (item == null || item.Item == null) { break; }

                                    var ItemIsDefaultLanguage = defaultLanguage?.Id == item?.Language?.Value;
                                    if (!ItemIsDefaultLanguage)
                                    {
                                        break;
                                    }

                                    foreach (var language in languages)
                                    {
                                        var contentItem = await kontentRepository.RetrieveContentItem(item.Item);
                                        var languageVariant = await kontentRepository.RetrieveLanguageVariant(new RetrieveLanguageVariantParameters
                                        {
                                            ItemReference = item.Item,
                                            LanguageReference = item.Language,
                                            TypeReference = contentItem.TypeReference
                                        }, true);

                                        if (language != null && language.Codename != null && languageVariant != null)
                                        {
                                            await TranslateItem(languageVariant, language.Codename, translationReviewId, language.IsDefault);
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                }

                return LogOk();
            }
            catch (ArgumentNullException ex)
            {
                return LogOkException(ex);
            }
            catch (ApiException ex)
            {
                return LogOkException(ex);
            }
            catch (Exception ex)
            {
                return LogException(ex);
            }
        }

        private async Task TranslateItem(LanguageVariant languageVariant, string languageCodename, Reference? targetWorkflowStep, bool isDefault)
        {
            if (!isDefault)
            {
                var translationLanguage = new CultureInfo(languageCodename).TwoLetterISOLanguageName;

                if (languageVariant == null)
                {
                    throw new NotImplementedException("Variant could not be retrieved.");
                }

                if (languageVariant.Elements == null)
                {
                    throw new NotImplementedException("Variant does not have elements.");
                }

                foreach (var element in languageVariant.Elements)
                {
                    switch (element)
                    {
                        case RichTextElement richTextElement:
                            {
                                var value = richTextElement.Value;

                                if (value?.Length >= 5000)
                                {
                                    var parts = textAnalyzer.SplitHtml(value);
                                    var longResult = "";

                                    foreach (var part in parts)
                                    {
                                        var (translated, translation) = await Translate(part, translationLanguage);

                                        if (translated)
                                        {
                                            longResult += translation;
                                        };
                                    }

                                    if (!string.IsNullOrWhiteSpace(longResult))
                                    {
                                        richTextElement.Value = longResult;
                                    }

                                    break;
                                }

                                var result = await Translate(richTextElement.Value, translationLanguage);

                                if (result.translated)
                                {
                                    richTextElement.Value = result.translation;
                                };
                                break;
                            }

                        case UrlSlugElement urlSlugElement:
                            {
                                var (translated, translation) = await Translate(urlSlugElement.Value, translationLanguage);

                                if (translated)
                                {
                                    urlSlugElement.Value = translation.Replace(" ", "-");
                                };
                                break;
                            }

                        case TextElement textElement:
                            {
                                var (translated, translation) = await Translate(textElement.Value, translationLanguage);

                                if (translated)
                                {
                                    textElement.Value = translation;
                                };
                                break;
                            }
                    }
                }

                await kontentRepository.UpsertLanguageVariant(new UpsertLanguageVariantParameters
                {
                    LanguageReference = new CodenameReference(languageCodename),
                    Variant = languageVariant
                });
            }

            await kontentRepository.ChangeWorkflowStepLanguageVariant(new ChangeWorkflowStepParameters
            {
                LanguageReference = new CodenameReference(languageCodename),
                Variant = languageVariant,
                WorkflowStepReference = targetWorkflowStep
            });
        }

        private async Task<(bool translated, string translation)> Translate(string? original, string translationLanguage)
        {
            if (string.IsNullOrWhiteSpace(original))
            {
                return (false, string.Empty);
            }

            var translation = (await translationService.Translate(original, translationLanguage)).Translations.First().Text;

            if (string.IsNullOrWhiteSpace(translation))
            {
                return (false, string.Empty);
            }

            return (true, translation);
        }
    }
}
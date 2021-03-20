<script lang="ts">
  import { deliveryClient } from "../../../shared/kontent";
  import translations from "./_resources";
  import { json2csv } from "json-2-csv";
  import { translate } from "../../../utilities/translateStore";
  import type {
    ContentItem,
    ContentTypeSystemAttributes,
    DeliveryError,
    GenericElement,
    Language,
  } from "@kentico/kontent-delivery";
  import { delay, fromPairs, replace } from "lodash";
  import Filter from "../../../shared/components/filter.svelte";
  import { localStorage } from "../../../utilities/localStore";
  import { fade } from "svelte/transition";
  import jwt_decode from "jwt-decode";

  enum LocalStorageKeys {
    SavedExport = "ExportCsv:SavedExport:",
  }

  interface ISuperType {
    system: ContentTypeSystemAttributes;
    elements: GenericElement[];
  }

  const systemElements: GenericElement[] = [
    { codename: "id", name: "Id", type: "system", options: [] },
    {
      codename: "codename",
      name: "Codename",
      type: "system",
      options: [],
    },
    {
      codename: "content_type",
      name: "Content type",
      type: "system",
      options: [],
    },
    {
      codename: "last_modified",
      name: "Last modified date",
      type: "system",
      options: [],
    },
    { codename: "url", name: "Preview URL", type: "system", options: [] },
    { codename: "collection", name: "Collection", type: "system", options: [] },
    {
      codename: "cms_link",
      name: "Link to item in Kontent",
      type: "system",
      options: [],
    },
  ];

  let genericInput: string = "";
  let projectId: string = "";
  let securedDelivery: boolean = false;
  let securedDeliveryApiKey: string = "";
  let superTypes: ISuperType[];
  let languages: Language[];

  let superTypesToExport: ISuperType[] = [];
  let exportSchema: {
    [key: string]: {
      superType: ISuperType;
      elements: GenericElement[];
      previewUrl: string;
    };
  } = {};

  $: {
    for (const superType of superTypesToExport) {
      if (!exportSchema.hasOwnProperty(superType.system.codename)) {
        exportSchema[superType.system.codename] = {
          superType,
          elements: systemElements,
          previewUrl: "",
        };
      }
    }

    for (const exportItem of Object.values(exportSchema)) {
      const { superType } = exportItem;

      if (!superTypesToExport.includes(superType)) {
        delete exportSchema[superType.system.codename];
      }
    }

    exportSchema = exportSchema;
  }

  let saving: boolean = false;

  $: savedExport =
    superTypes &&
    localStorage<{
      [key: string]: {
        elementCodenames: string[];
        previewUrl: string;
      };
    }>(LocalStorageKeys.SavedExport + projectId);

  let result: string = "";

  $: if (genericInput !== undefined) {
    // projectId = "";
    // securedDeliveryApiKey = "";
    superTypes = undefined;
    languages = undefined;
    superTypesToExport = [];
    exportSchema = {};

    try {
      securedDeliveryApiKey = genericInput;
      projectId = jwt_decode<{ project_id: string }>(
        securedDeliveryApiKey
      ).project_id.replace(
        /([a-f0-9]{8})([a-f0-9]{4})([a-f0-9]{4})([a-f0-9]{4})([a-f0-9]{12})/,
        "$1-$2-$3-$4-$5"
      );
      securedDelivery = true;
    } catch (error) {
      securedDeliveryApiKey = "";
      projectId = genericInput;
      securedDelivery = false;
    }

    if (projectId !== "") {
      loadKontent();
    }
  }

  const loadKontent = async () => {
    try {
      await loadTypesLanguagesAndSchema();
    } catch (error) {
      if (error instanceof DeliveryError && error.errorCode === 3) {
        securedDelivery = true;
      }
    }
  };

  const loadTypesLanguagesAndSchema = async () => {
    const typesRequest = deliveryClient({
      projectId,
      securedDeliveryApiKey,
    }).types();

    superTypes = (await typesRequest.toPromise()).types.map((type) => ({
      system: type.system,
      elements: [...systemElements, ...type.elements],
    }));

    const languagesRequest = deliveryClient({
      projectId,
      securedDeliveryApiKey,
    }).languages();

    languages = (await languagesRequest.toPromise()).languages;

    if ($savedExport) {
      for (const savedItem of Object.entries($savedExport)) {
        const [id, { elementCodenames, previewUrl }] = savedItem;

        const superType = superTypes.find(
          (superType) => superType.system.id === id
        );

        if (superType) {
          superTypesToExport.push(superType);

          const elements = [];

          for (const elementCodename of elementCodenames) {
            const element = superType.elements.find(
              (element) => element.codename === elementCodename
            );

            if (element) {
              elements.push(element);
            }
          }

          exportSchema[superType.system.codename] = {
            superType,
            elements,
            previewUrl,
          };
        }
      }

      exportSchema = exportSchema;
    }
  };

  const saveData = () => {
    saving = true;

    const saveSchema = Object.values(exportSchema).map(
      ({ superType, elements, previewUrl }) => [
        superType.system.id,
        {
          elementCodenames: elements.map((element) => element.codename),
          previewUrl,
        },
      ]
    );

    savedExport.set(fromPairs(saveSchema));

    delay(() => (saving = false), 150);
  };

  const exportData = async () => {
    result = "";

    let request = deliveryClient({
      projectId,
      securedDeliveryApiKey,
    }).items();

    if (superTypesToExport.length > 0) {
      request = request.types(
        superTypesToExport.map((type) => type.system.codename)
      );
    }

    const allElements = new Set<string>();

    for (const exportItem of Object.values(exportSchema)) {
      const { superType, elements } = exportItem;

      for (const element of elements) {
        if (element.type === "system") {
          if (element.codename === systemElements[4].codename) {
            const urlSlugElement = superType.elements.find(
              (element) => element.type === "url_slug"
            );

            if (urlSlugElement) {
              allElements.add(urlSlugElement.codename);
            }
          }

          continue;
        }

        allElements.add(element.codename);
      }
    }

    if (allElements.size > 0) {
      request = request.elementsParameter([...allElements.values()]);
    }

    const reduceItem = (item: ContentItem) => {
      const reducedItem = {};

      const { elements, previewUrl } = exportSchema[item.system.type];

      for (const element of elements) {
        let value = item._raw.elements[element.codename]?.value;

        if (element.type === "system") {
          switch (element.codename) {
            case systemElements[0].codename:
              value = item.system.id;
              break;
            case systemElements[1].codename:
              value = item.system.codename;
              break;
            case systemElements[2].codename:
              value = item.system.type;
              break;
            case systemElements[3].codename:
              value = item.system.lastModified.toLocaleDateString();
              break;
            case systemElements[4].codename:
              value = previewUrl;

              value = replace(
                value,
                new RegExp("{URLslug}", "gi"),
                Object.values(item._raw.elements).find(
                  (element) => element.type === "url_slug"
                )?.value
              );
              value = replace(
                value,
                new RegExp("{Lang}", "gi"),
                item.system.language
              );
              value = replace(
                value,
                new RegExp("{Codename}", "gi"),
                item.system.codename
              );
              value = replace(
                value,
                new RegExp("{ItemId}", "gi"),
                item.system.id
              );
              break;
            case systemElements[5].codename:
              value = item.system.collection;
              break;
            case systemElements[6].codename:
              const language = languages.find(
                (language) => language.system.codename === item.system.language
              );

              value = `https://app.kontent.ai/${projectId}/content-inventory/${language.system.id}/content/${item.system.id}`;
              break;
          }
        }

        if (value !== undefined) {
          reducedItem[element.codename] = value;
        }
      }

      return reducedItem;
    };

    const data = (await request.toPromise()).items.map(reduceItem);

    json2csv(data, (error, csv) => (result = csv), {
      emptyFieldValue: "",
      excelBOM: true,
    });
  };

  const getFilename = (prefix: string, middle: string, suffix: string) => {
    const sanitize = (value: string) =>
      replace(value, /[\/, ]/g, "_").slice(0, 50);

    return `${sanitize(prefix)}_${sanitize(middle)}_${sanitize(suffix)}`;
  };

  const downloadFile = (body: string, filename: string, extension = "csv") => {
    const blob = new Blob([body]);

    const link = document.createElement("a");

    link.href = URL.createObjectURL(blob);
    link.download = `${filename}.${extension}`;
    link.style.visibility = "hidden";

    document.body.appendChild(link);

    link.click();

    document.body.removeChild(link);

    URL.revokeObjectURL(link.href);
  };

  const t = translate(translations);
</script>

<section>
  <div class="group">
    <div class="group column item">
      <h2>{$t("export")}</h2>
      <p>
        {@html $t("exportDescription")}
      </p>
      <label class="group column">
        <b>
          {$t("projectId")}
        </b>
        <input
          type="text"
          placeholder={$t("projectIdPlaceholder")}
          bind:value={genericInput} />
      </label>
      {#if securedDelivery && !superTypes}
        <b>
          {$t("securedDeliveryDescription")}
        </b>
      {/if}
      {#key superTypes}
        {#if superTypes && languages}
          <div class="group column">
            <p>
              {$t("exportUrlsDescription")}
              <a
                href={`https://app.kontent.ai/${projectId}/settings/preview-urls`}
                target="_blank">{$t("exportUrlsLink")}</a
              >.
            </p>
            <label class="group column item" for="contentType">
              <b>
                {$t("exportTypes")}
              </b>
              <Filter
                bind:value={superTypesToExport}
                id="contentType"
                placeholder={$t("exportTypesPlaceholder")}
                values={superTypes}
                mapOption={(contentType) => ({
                  display: contentType.system.name,
                  key: contentType.system.id,
                  filter: contentType.system.codename.toLowerCase(),
                })}
                getValue={(rawValue) =>
                  superTypes.find(
                    (type) => type.system.codename === rawValue
                  )} />
            </label>
            {#each Object.values(exportSchema) as { superType, elements, previewUrl } (superType)}
              <div class="export" transition:fade={{ duration: 150 }}>
                <h3>{superType.system.name}</h3>
                <label class="group column item" for={superType.system.id}>
                  <b>
                    {$t("exportElements")}
                  </b>
                  <Filter
                    bind:value={elements}
                    id={superType.system.id}
                    placeholder={$t("exportElementsPlaceholder")}
                    values={superType.elements}
                    mapOption={(element) => ({
                      display: element.name,
                      key: `${element.type}${element.codename}`,
                      filter: element.codename.toLowerCase(),
                    })}
                    getValue={(rawValue) =>
                      superType.elements.find(
                        (element) => element.codename === rawValue
                      )}
                    setValueOverride={(setValue, newValue, add) => {
                      setValue(newValue, add);
                      exportSchema = exportSchema;
                    }} />
                </label>
                <label class="group column">
                  <b>
                    {$t("exportUrl")}
                  </b>
                  <input
                    type="text"
                    bind:value={previewUrl}
                    placeholder={$t("exportUrlPlaceholder")} />
                </label>
              </div>
            {/each}
          </div>
          <div class="group">
            <button
              class="item"
              on:click={exportData}
              disabled={superTypesToExport.length === 0 ||
                !Object.values(exportSchema).some(
                  (exportItem) => exportItem.elements.length > 0
                )}>{$t("export")}</button>
            <button
              class="item"
              on:click={saveData}
              disabled={superTypesToExport.length === 0 ||
                saving ||
                !Object.values(exportSchema).some(
                  (exportItem) => exportItem.elements.length > 0
                )}>{$t("save")}</button>
          </div>
        {/if}
      {/key}
    </div>
    <div class="result group column item">
      {#if superTypes && languages && result !== ""}
        <div class="group column item">
          <h2>{$t("result")}</h2>
          <textarea class="item" readonly bind:value={result} />
        </div>
        <div class="group">
          <button
            class="item"
            on:click={() =>
              downloadFile(
                result,
                getFilename(
                  $t("filename"),
                  superTypesToExport.map((type) => type.system.name).join("_"),
                  new Date().toLocaleString()
                )
              )}>{$t("download")}</button>
        </div>
      {/if}
    </div>
  </div>
</section>

<style>
  section {
    margin-top: 1em;
  }

  h2 + p {
    margin-top: 0;
  }

  .group :global(sup) {
    display: inline-block;
    border-style: solid;
    color: #4c4d52;
    border-color: #919194;
    padding: 0.1em 0.2em;
    font-size: 0.85em;
    border-width: 0.1em;
    border-radius: 0.25em;
    vertical-align: initial;
    line-height: 1.1em;
  }

  input[type="text"] {
    padding: 0em 0.2em 0.3em;
    border: none;
    outline: none;
    border-bottom: 0.15em solid #969696;
    margin: 0.2em 0em 0em 0em;
    font-size: 1em;
    height: 1.5em;
    font-family: Roboto, -apple-system, BlinkMacSystemFont, Segoe UI, Oxygen,
      Ubuntu, Cantarell, Fira Sans, Droid Sans, Helvetica Neue, sans-serif;
  }

  input:focus {
    border-color: #81d272;
  }

  ::-webkit-input-placeholder {
    font-style: italic;
    opacity: 0.5;
  }

  ::-moz-placeholder {
    font-style: italic;
    opacity: 0.5;
  }

  :-ms-input-placeholder {
    font-style: italic;
    opacity: 0.5;
  }

  :-moz-placeholder {
    font-style: italic;
    opacity: 0.5;
  }

  .export {
    padding: 0.5em 0.5em 0.3em;
    margin: 0.5em 0 0.5em 0.5em;
    border-radius: 0.5em;
    box-shadow: #afafaf 0em 0.2em 0.4em;
  }

  button {
    background: #81d272;
    border-radius: 50vh;
    padding: 0.2em 0.6em;
    color: #ffffff;
    fill: #ffffff;
    font-weight: 700;
    text-decoration: none;
    border: none;
    font-size: 1.3em;
    font-family: Roboto, -apple-system, BlinkMacSystemFont, Segoe UI, Oxygen,
      Ubuntu, Cantarell, Fira Sans, Droid Sans, Helvetica Neue, sans-serif;
    outline: none;
  }

  button + button {
    margin-left: 1em;
  }

  button:hover {
    background: #5d9b52;
    cursor: pointer;
  }

  button:disabled {
    color: #ffffff;
    background-color: #adadad;
  }

  button:hover:disabled {
    cursor: not-allowed;
  }

  textarea {
    resize: none;
    font-size: 1em;
    font-family: inherit;
    border-radius: 1em;
    border-width: 0.2em;
    padding: 0.5em;
  }

  textarea:focus {
    border-color: #5d9b52;
    outline: none;
  }

  .result {
    padding: 0 0 0 1em;
  }
</style>

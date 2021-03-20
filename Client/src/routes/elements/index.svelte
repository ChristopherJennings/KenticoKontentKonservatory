<script context="module" lang="ts">
  import type { Preload } from "@sapper/app";
  import type { ISession, SvelteConstructor } from "../../shared/kontent";
  import {
    deliveryClient,
    extractComponents,
    replaceComponents,
  } from "../../shared/kontent";
  import type { ICode } from "../../shared/models/Code";
  import { Code as CodeModel } from "../../shared/models/Code";
  import type { ICustomElement } from "../../shared/models/CustomElement";
  import { CustomElement } from "../../shared/models/CustomElement";
  import { Icon } from "../../shared/models/Icon";
  import type { IIcon } from "../../shared/models/Icon";

  export const preload: Preload<
    {
      customElements: ICustomElement[];
      components: Map<string, ICode>;
      icons: IIcon[];
    },
    ISession
  > = async function (this, page, session) {
    const components = new Map<string, ICode>();
    const richTextResolver = extractComponents(
      components,
      new Map([[CodeModel.codename, (item) => (item as CodeModel).getModel()]])
    );

    const customElements = (
      await deliveryClient(session.kontent)
        .items<CustomElement>()
        .type(CustomElement.codename)
        .queryConfig({
          richTextResolver,
        })
        .toPromise()
    ).items;

    const icons = (
      await deliveryClient(session.kontent)
        .items<Icon>()
        .type(Icon.codename)
        .toPromise()
    ).items;

        const translations = (
      await deliveryClient(session.kontent)
        .items<Translation>()
        .type(Translation.codename)
        .toPromise()
    ).items.reduce((translations, translation) => {
      translations[translation.system.codename] = translation.content.value;
      return translations;
    }, {});

    session.kontent.translations = { en_us: { translation: translations } };

    return {
      customElements: customElements.map((element) => element.getModel()),
      components,
      icons: icons.map((icon) => icon.getModel()),
    };
  };
</script>

<script lang="ts">
  import sortArray from "sort-array";
  import { onMount, tick } from "svelte";
  import Code from "../../shared/components/code.svelte";
  import { translate } from "../../utilities/translateStore";
  import { stores } from "@sapper/app";
  import { fly } from "svelte/transition";
  import jwt_decode from "jwt-decode";
  import wretch from "wretch";
  import Filter from "../../shared/components/filter.svelte";

  export let customElements: ICustomElement[];
  export let components: Map<string, ICode>;
  export let icons: IIcon[];

  let selectedElement: ICustomElement;

  const { session } = stores<ISession>();

  const replaceMap = new Map<string, SvelteConstructor>([
    [CodeModel.codename, (args) => new Code(args)],
  ]);

  onMount(() => {
    if (window.location.hash) {
      selectedElement = customElements.find(
        (customElement) =>
          customElement.codename == window.location.hash.slice(1)
      );
    }
  });

  $: selectedElement && scrollToElement();

  const scrollToElement = async () => {
    if (selectedElement) {
      const listItem = document.getElementById(selectedElement.codename);

      if (listItem) {
        await tick();

        window.scrollTo({
          top: listItem.offsetTop,
          behavior: "smooth",
        });
      }
    }
  };

  let filter: string = "";

  $: if (filter) {
    selectedElement = undefined;
    history.replaceState(
      undefined,
      undefined,
      `${window.location.origin}${window.location.pathname}`
    );
  }

  $: sortedElements = sortArray(
    customElements.filter((customElement) => {
      if (filter === "") {
        return true;
      }

      const matches = (value: string) => value.match(new RegExp(filter, "gi"));

      if (matches(customElement.name)) {
        return true;
      }

      if (
        customElement.tags.some(
          (tag) => matches(tag.name) || matches(tag.synonyms)
        )
      ) {
        return true;
      }

      if (matches(customElement.description)) {
        return true;
      }

      return false;
    }),
    {
      by: ["name"],
    }
  );

  const sampleIcon = icons.find((icon) => icon.codename == "code");
  const gitHubIcon = icons.find((icon) => icon.codename == "github_icon");
  const installIcon = icons.find((icon) => icon.codename == "install_icon");

  const t = translate($session.kontent.translations);

  interface IListTypesResponse {
    types: IType[];
  }

  interface IType {
    id: string;
    codename: string;
    lastModified: Date;
    name: string;
    elements: IElement[];
  }

  interface IElement {
    id?: string;
    name: string;
    type: string;
    external_id?: string;
    source_url?: string;
    depends_on?: any;
  }

  let overlay: boolean = false;
  let managementApiKey: string = "";
  let projectId: string;
  let contentTypes: IType[];
  let contentType: IType;
  let open: boolean = false;
  let newOrClone: "new" | "clone" = "new";
  let clone: boolean = true;
  let updatedTypeId: string;

  const openOverlay = () => {
    overlay = true;
    document.body.style.top = `-${document.body.parentElement.scrollTop}px`;
    document.body.classList.add("noscroll");

    managementApiKey = "";
    projectId = undefined;
    contentType = undefined;
    open = false;
    newOrClone = "new";
    clone = true;
    updatedTypeId = undefined;
  };

  $: if (managementApiKey !== "") {
    try {
      projectId = jwt_decode<{ project_id: string }>(
        managementApiKey
      ).project_id.replace(
        /([a-f0-9]{8})([a-f0-9]{4})([a-f0-9]{4})([a-f0-9]{4})([a-f0-9]{12})/,
        "$1-$2-$3-$4-$5"
      );
    } catch (error) {
      console.log(error);
    }
  }

  $: projectId && loadTypes();

  const loadTypes = async () => {
    const getTypesEndpoint = new URL(
      `https://manage.kontent.ai/v2/projects/${projectId}/types`
    );

    const request = wretch(getTypesEndpoint.toString())
      .auth(`Bearer ${managementApiKey}`)
      .get()
      .json<IListTypesResponse>();

    try {
      contentTypes = (await request).types;
    } catch (error) {
      console.log(error);
    }
  };

  $: if (contentType) {
    if (contentTypes.some((type) => type.codename === contentType.codename)) {
      newOrClone = "clone";
    } else {
      newOrClone = "new";
      clone = true;
    }
  }

  const install = async () => {
    let request: Promise<{
      id: string;
    }>;

    const newElement = {
      name: `${selectedElement.name}`,
      type: "custom",
      source_url: `${location.origin}/${selectedElement.route}`,
      json_parameters: selectedElement.demoConfig,
    };

    if (newOrClone === "new") {
      const newTypeEndpoint = new URL(
        `https://manage.kontent.ai/v2/projects/${projectId}/types`
      );

      request = wretch(newTypeEndpoint.toString())
        .auth(`Bearer ${managementApiKey}`)
        .post({
          name: contentType.name.slice(0, 50),
          elements: [newElement],
        })
        .json<{ id: string }>();
    } else if (newOrClone === "clone" && clone) {
      const newTypeEndpoint = new URL(
        `https://manage.kontent.ai/v2/projects/${projectId}/types`
      );

      const existingType = contentTypes.find(
        (type) => type.codename === contentType.codename
      );

      existingType.name = `${existingType.name} ${$t("clone")} ${
        selectedElement.name
      } ${new Date().toLocaleString()}`.slice(0, 50);
      delete existingType.codename;

      for (const element of existingType.elements) {
        element.external_id = element.id;
        delete element.id;

        if (element.depends_on?.element) {
          element.depends_on.element.external_id =
            element.depends_on.element.id;
          delete element.depends_on.element.id;
        }
      }

      existingType.elements.push(newElement);

      request = wretch(newTypeEndpoint.toString())
        .auth(`Bearer ${managementApiKey}`)
        .post(existingType)
        .json<{ id: string }>();
    } else if (newOrClone === "clone" && !clone) {
      const modifyTypeEndpoint = new URL(
        `https://manage.kontent.ai/v2/projects/${projectId}/types/codename/${contentType.codename}`
      );

      request = wretch(modifyTypeEndpoint.toString())
        .auth(`Bearer ${managementApiKey}`)
        .patch([
          {
            op: "addInto",
            path: "/elements",
            value: newElement,
          },
        ])
        .json<{ id: string }>();
    }

    try {
      updatedTypeId = (await request).id;
    } catch (error) {
      console.log(error);
    }
  };

  const closeOverlay = () => {
    overlay = false;
    document.body.classList.remove("noscroll");
    document.body.parentElement.scrollTop = parseInt(
      document.body.style.top.slice(1, -2)
    );
    document.body.style.top = "";
  };
</script>

<section>
  {#if selectedElement}
    <div class="hidden" class:overlay on:click={() => (open = false)}>
      <div class="modal group column">
        {#if updatedTypeId === undefined}
          <div class="item">
            <h4>{`${$t("install_demo_of")} ${selectedElement.name}`}</h4>
            <p>
              {$t("install_description")}
            </p>
          </div>
          <div class="item">
            <label class="group column">
              <b>
                {$t("management_api_key")}
              </b>
              <input
                type="password"
                class="key"
                bind:value={managementApiKey} />
            </label>
          </div>
          {#if projectId && contentTypes}
            <div class="item group">
              <div class="item">
                <label class="group column" for="contentType">
                  <b>
                    {$t("content_type")}
                  </b>
                  <div class="group">
                    <div class="typeFilter">
                      <Filter
                        bind:value={contentType}
                        id="contentType"
                        values={contentTypes}
                        mapOption={(contentType) => ({
                          display: contentType.name,
                          key: contentType.id,
                          filter: contentType.codename.toLowerCase(),
                        })}
                        getValue={(rawValue) => {
                          const foundType = contentTypes.find(
                            (contentType) => contentType.codename === rawValue
                          );

                          if (foundType) {
                            return foundType;
                          }

                          return {
                            id: rawValue,
                            codename: rawValue,
                            name: rawValue,
                          };
                        }}
                        allowCustom />
                    </div>
                    {#if contentType}
                      <div class="typeCheckbox item">
                        <label class="newOrClone group column">
                          <input
                            class="checkbox"
                            type="checkbox"
                            disabled={newOrClone === "new"}
                            bind:checked={clone} />
                          {#if newOrClone === "new"}
                            {$t("make_new")}
                          {:else}
                            {$t("add_clone")}
                          {/if}
                        </label>
                      </div>
                    {/if}
                  </div>
                </label>
              </div>
            </div>
          {/if}
          {#if contentType}
            <div class="item install">
              <button class="install" on:click={install}
                >{$t("install")}</button>
            </div>
          {/if}
        {:else}
          <div class="item">
            <h4>{`${$t("install_demo_of")} ${selectedElement.name}`}</h4>
          </div>
          <div class="item">
            <p>{$t("install_success")}</p>
            <p>
              <a
                class="badge"
                href={`https://app.kontent.ai/${projectId}/content-models/types/edit/${updatedTypeId}`}
                target="_blank">{$t("edit_type")}</a>
              <a
                class="badge"
                href={`https://app.kontent.ai/${projectId}/content-inventory`}
                target="_blank">{$t("open_inventory")}</a>
            </p>
          </div>
        {/if}
      </div>
      <div class="blocker" on:click={closeOverlay} />
    </div>
  {/if}
  <div class="list">
    <div class="filter">
      <input
        type="text"
        placeholder={$t("filter_custom_elements")}
        bind:value={filter} />
    </div>
    {#each sortedElements as customElement (customElement.name)}
      <div
        class="group"
        id={customElement.codename}
        class:selected={selectedElement == customElement}>
        <div
          class="content"
          on:click={() => {
            if (selectedElement !== customElement) {
              selectedElement = customElement;
              history.replaceState(
                undefined,
                undefined,
                `${window.location.origin}${window.location.pathname}#${customElement.codename}`
              );
            }
          }}>
          <h2 class="name">{customElement.name}</h2>
          {#if selectedElement == customElement}
            {#each sortArray(customElement.tags, {
              by: ["name"],
            }) as tag (tag.codename)}
              <span class="tag">{tag.name}</span>
            {/each}
            <div
              class="description"
              use:replaceComponents={{ components, replaceMap }}>
              {@html customElement.description}
            </div>
            <a class="badge" href={customElement.route} target="_blank"
              >{@html sampleIcon.svg}{$t("sample")}</a>
            <a class="badge" href={customElement.github} target="_blank"
              >{@html gitHubIcon.svg}{$t("github")}</a>
            {#if customElement.options.some((option) => option === "demo")}
              <button class="badge" on:click={openOverlay}
                >{@html installIcon.svg}{$t("install_demo")}</button>
            {/if}
          {/if}
        </div>
        <div class="image">
          {#key selectedElement}
            {#if selectedElement == customElement}
              <img
                in:fly={{ y: 100, delay: 200 }}
                src={selectedElement.image.src}
                alt={selectedElement.image.alt} />
            {/if}
          {/key}
        </div>
      </div>
    {/each}
  </div>
</section>

<style>
  :global(.noscroll) {
    position: fixed;
    width: 100%;
    overflow-y: scroll;
  }

  .hidden:not(.overlay) {
    display: none;
  }

  .overlay {
    position: fixed;
    display: flex;
    z-index: 10;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    flex-direction: column;
    justify-content: center;
  }

  .overlay .modal {
    margin: 0 auto;
    width: 40em;
    border-radius: 0.5em;
    background: white;
    z-index: 1;
  }

  .overlay .blocker {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: #4c4d5279;
  }

  .modal .group {
    margin: 0;
  }

  .modal > .item {
    padding: 1em;
  }

  .modal > .item + .item {
    padding-top: 0em;
  }

  .modal p {
    margin-top: 0;
  }

  .modal .key {
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

  .modal .key:focus {
    border-color: #81d272;
  }

  .modal .typeFilter {
    position: relative;
    margin-right: 0.5em;
  }

  .modal .typeCheckbox {
    align-self: center;
  }

  .modal .newOrClone {
    flex-direction: row;
    height: 1.5em;
    align-items: baseline;
  }

  .modal .newOrClone .checkbox {
    margin: 0.5em 0.5em;
    font-size: 0.5em;
  }

  .modal .install {
    text-align: center;
  }

  .modal .install button {
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

  .modal .install button:hover {
    background: #5d9b52;
    cursor: pointer;
  }

  .filter {
    display: flex;
  }

  .filter input {
    flex: 1;
    margin: 0.2em 0.2em 0.5em;
    padding: 0.3em;
    font-size: 1.2em;
    border: none;
    border-bottom: 0.15em solid #969696;
  }

  .filter input:focus {
    outline: none;
    border-color: #81d272;
  }

  .list {
    flex: 1;
    z-index: 1;
  }

  .group .content {
    flex: 2;
    padding: 1em;
    position: relative;
    overflow: hidden;
    border-radius: 1em;
  }

  .group:not(.selected) .content:before {
    width: 300%;
    height: 300%;
    content: "";
    left: -1em;
    top: -1em;
    position: absolute;
    background: linear-gradient(
      160deg,
      white,
      gainsboro 35.9%,
      #81d272 36%,
      white
    );
    transition: all 0.5s;
    transform: translate(0%, 0%);
    z-index: -1;
  }

  .group:not(.selected) .content .description {
    display: none;
  }

  .group:not(.selected) .content:hover {
    cursor: pointer;
  }

  .group:not(.selected) .content:hover:before {
    transform: translate(-45%, -45%);
  }

  .group.selected .content {
    box-shadow: #afafaf 0em 0.2em 0.4em;
  }

  .group.selected .content .description {
    display: block;
  }

  .name {
    margin: 0;
  }

  .group.selected .name {
    margin-bottom: 0.5em;
  }

  .tag {
    font-size: 0.8em;
    background: #d0d0d0;
    margin-right: 0.2em;
    text-transform: uppercase;
    border-radius: 50vh;
    padding: 0.2em 0.6em;
    color: white;
    font-weight: 600;
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

  .badge {
    background: #81d272;
    margin-right: 0.2em;
    border-radius: 50vh;
    padding: 0.2em 0.6em;
    color: #ffffff;
    fill: #ffffff;
    font-weight: 700;
    text-decoration: none;
    border: none;
    font-size: 1em;
    font-family: Roboto, -apple-system, BlinkMacSystemFont, Segoe UI, Oxygen,
      Ubuntu, Cantarell, Fira Sans, Droid Sans, Helvetica Neue, sans-serif;
    outline: none;
  }

  .badge:hover {
    background: #5d9b52;
    cursor: pointer;
  }

  .group :global(.badge svg) {
    height: 0.9em;
    padding: 0 0.4em 0 0;
  }

  .image {
    padding-left: 1em;
    flex: 3;
  }

  .image img {
    max-width: 100%;
  }

  @media (max-width: 800px) {
    .image {
      padding: 1em 0 0;
    }

    .image {
      position: relative;
      left: initial;
      max-width: 100%;
    }
  }
</style>

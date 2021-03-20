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
  import type { IWebhook } from "../../shared/models/Webhook";
  import { Webhook } from "../../shared/models/Webhook";
  import { Icon } from "../../shared/models/Icon";
  import type { IIcon } from "../../shared/models/Icon";
  import { Translation } from "../../shared/models/Translation";
  import type { ISite } from "../../shared/models/Site";
  import { Site } from "../../shared/models/Site";

  export const preload: Preload<
    {
      webhooks: IWebhook[];
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

    const webhooks = (
      await deliveryClient(session.kontent)
        .items<Webhook>()
        .type(Webhook.codename)
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

    const site = (
      await deliveryClient(session.kontent)
        .item<Site>(Site.codename)
        .depthParameter(6)
        .toPromise()
    ).item;

    return {
      site: site.getModel(),
      webhooks: webhooks.map((webhook) => webhook.getModel()),
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

  export let site: ISite;
  export let webhooks: IWebhook[];
  export let components: Map<string, ICode>;
  export let icons: IIcon[];

  let selectedWebhook: IWebhook;

  const { session } = stores<ISession>();

  const replaceMap = new Map<string, SvelteConstructor>([
    [CodeModel.codename, (args) => new Code(args)],
  ]);

  onMount(() => {
    if (window.location.hash) {
      selectedWebhook = webhooks.find(
        (customElement) =>
          customElement.codename == window.location.hash.slice(1)
      );
    }
  });

  $: selectedWebhook && scrollToElement();

  const scrollToElement = async () => {
    if (selectedWebhook) {
      const listItem = document.getElementById(selectedWebhook.codename);

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
    selectedWebhook = undefined;
    history.replaceState(
      undefined,
      undefined,
      `${window.location.origin}${window.location.pathname}`
    );
  }

  $: sortedWebhooks = sortArray(
    webhooks.filter((webhook) => {
      if (filter === "") {
        return true;
      }

      const matches = (value: string) => value.match(new RegExp(filter, "gi"));

      if (matches(webhook.name)) {
        return true;
      }

      if (
        webhook.tags.some((tag) => matches(tag.name) || matches(tag.synonyms))
      ) {
        return true;
      }

      if (matches(webhook.description)) {
        return true;
      }

      return false;
    }),
    {
      by: ["name"],
    }
  );

  const gitHubIcon = icons.find((icon) => icon.codename == "github_icon");

  const t = translate($session.kontent.translations);
</script>

<svelte:head>
  <title>{site.name}</title>
</svelte:head>

<h1><a href="/">{site.name}</a></h1>
<section>
  <div class="list">
    <div class="filter">
      <input
        type="text"
        placeholder={$t("filter_webhooks")}
        bind:value={filter} />
    </div>
    {#each sortedWebhooks as customElement (customElement.name)}
      <div
        class="group"
        id={customElement.codename}
        class:selected={selectedWebhook == customElement}>
        <div
          class="content"
          on:click={() => {
            if (selectedWebhook !== customElement) {
              selectedWebhook = customElement;
              history.replaceState(
                undefined,
                undefined,
                `${window.location.origin}${window.location.pathname}#${customElement.codename}`
              );
            }
          }}>
          <h2 class="name">{customElement.name}</h2>
          {#if selectedWebhook == customElement}
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
            <a class="badge" href={customElement.github}
              >{@html gitHubIcon.svg}{$t("github")}</a>
          {/if}
        </div>
        <div class="image" />
      </div>
    {/each}
  </div>
</section>

<style>
  h1 {
    text-align: center;
    font-size: 5em;
    text-transform: uppercase;
    font-weight: 700;
    margin: 0 0 0.5em 0;
  }

  h1 a {
    text-decoration: none;
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
    border-bottom: #cacaca solid;
  }

  .filter input:focus {
    outline: none;
    border-bottom: #727272 solid;
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
  }

  .badge:hover {
    background: #5d9b52;
  }

  .group :global(.badge svg) {
    height: 0.9em;
    padding: 0 0.4em 0 0;
  }

  .image {
    padding-left: 1em;
    flex: 3;
  }

  @media (max-width: 800px) {
    .group {
      flex-direction: column;
    }

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

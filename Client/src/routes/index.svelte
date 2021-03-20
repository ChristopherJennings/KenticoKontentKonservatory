<script context="module" lang="ts">
  import type { Preload } from "@sapper/app";
  import type { ISession } from "../shared/kontent";
  import { deliveryClient } from "../shared/kontent";
  import type { ISite } from "../shared/models/Site";
  import { Site } from "../shared/models/Site";

  export const preload: Preload<{}, ISession> = async function (
    this,
    page,
    session
  ) {
    if (!session.kontent.projectId) {
      return { site: { name: "", routes: [] } };
    }

    const site = (
      await deliveryClient(session.kontent)
        .item<Site>(Site.codename)
        .depthParameter(6)
        .toPromise()
    ).item;

    return { site: site.getModel() };
  };
</script>

<script lang="ts">
  export let site: ISite;
</script>

<svelte:head>
  <title>{site.name}</title>
</svelte:head>

<h1><a href="/">{site.name}</a></h1>
<section>
  <div class="list">
    {#each site.routes as route}
      <a class="item" href={route.route}>
        <div>
          {#if route.icon}
            {@html route.icon.svg}
          {/if}
          <h2>{route.name}</h2>
        </div>
      </a>
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

  section {
    flex-direction: row;
  }

  .list {
    display: flex;
    flex-direction: column;
    min-width: 40em;
  }

  .item {
    height: 10em;
    border-radius: 1em;
    margin-bottom: 1em;
    display: flex;
    text-decoration: none;
    overflow: hidden;
    position: relative;
  }

  .item:before {
    width: 300%;
    height: 300%;
    content: "";
    position: absolute;
    background: linear-gradient(
      160deg,
      white,
      gainsboro 32.9%,
      #81d272 33%,
      white
    );
    transition: all 0.5s;
    transform: translate(0%, 0%);
  }

  .item:hover:before {
    transform: translate(-45%, -45%);
  }

  .item div {
    margin: auto;
    z-index: 1;
    display: flex;
  }

  .item div :global(svg) {
    height: 2em;
    padding: 0.4em 0.5em 0 0;
  }

  .item div h2 {
    font-size: 2em;
  }

  @media (max-width: 800px) {
    .list {
      margin: 0 auto;
    }
  }
</style>

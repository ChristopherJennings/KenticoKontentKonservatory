<script lang="ts">
  import { fade } from "svelte/transition";
</script>

<div class="root" in:fade>
  <div class="loading">
    <div class="bubble" />
  </div>
</div>

<style>
  .root {
    display: flex;
    height: 100px;
    flex-grow: 1;
    flex-direction: column;
    justify-content: center;
    align-items: center;
  }
  .loading {
    width: 200px;
    height: 5px;
    overflow: hidden;
    background-color: #d0cfce;
    border-radius: 5px;
    animation: loader 6.2s ease;
    transform-origin: 50% 100%;
  }
  .bubble {
    width: 200px;
    height: 5px;
    background-color: #0a68f5;
    animation: bubble 1.7s ease infinite;
    transform-origin: 0 100%;
    animation-delay: 0.55s;
  }

  @keyframes loader {
    0% {
      opacity: 0;
      transform: translate(0) scaleX(0.01);
    }

    10% {
      opacity: 0.2;
      transform: translate(0) scaleX(0.01);
    }

    14% {
      opacity: 1;
      transform: translate(0) scale(1);
    }

    to {
      opacity: 1;
      transform: translate(0) scale(1);
    }
  }

  @keyframes bubble {
    0% {
      transform: translate(-195px);
    }

    50% {
      transform: translate(195px);
    }

    to {
      transform: translate(-195px);
    }
  }
</style>

import { ContentItem, Elements } from '@kentico/kontent-delivery';

import type { CustomElement } from "./CustomElement";

export interface ISite {
  name: string;
}

export class Site extends ContentItem {
  static codename = "site";
  name!: Elements.TextElement;
  custom_elements!: Elements.LinkedItemsElement<CustomElement>;
}

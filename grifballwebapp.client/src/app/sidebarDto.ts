import { Signal } from "@angular/core";

export interface SideBarDto {
    title: string;
    path: string;
  show: Signal<boolean>;
  //show: boolean;
  }

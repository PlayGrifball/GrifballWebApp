import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiClientService } from '../api/apiClient.service';
import { TeamResponseDto } from '../api/dtos/teamResponseDto';
import { PlayerDto } from '../api/dtos/playerDto';
import {
  CdkDrag,
  CdkDragDrop,
  CdkDropList,
  CdkDropListGroup,
  moveItemInArray,
  transferArrayItem,
} from '@angular/cdk/drag-drop';
import { DndModule } from 'ngx-drag-drop';

@Component({
  selector: 'app-teams',
  standalone: true,
  imports: [
    CommonModule,
    CdkDropListGroup,
    CdkDropList,
    CdkDrag,
    DndModule
  ],
  templateUrl: './teams.component.html',
  styleUrl: './teams.component.scss',
})
export class TeamsComponent {
  private seasonID : number = 0;
  teams! : TeamResponseDto[];
  playerPool! : PlayerDto[];

  playerChosen : PlayerDto[] = [];

  constructor(private route: ActivatedRoute, private api: ApiClientService) {}

  drop(event: CdkDragDrop<PlayerDto[]>) {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(
        event.previousContainer.data,
        event.container.data,
        event.previousIndex,
        event.currentIndex,
      );
    }
  }
  
  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));

    if (this.seasonID === 0) {
      this.api.getCurrentSeasonID()
        .subscribe({
          next: (result) => {
            this.seasonID = result;
            this.load();
          },
        });
    }
    else {
      this.load();
    }
  }

  load(): void {
    this.api.getTeams(this.seasonID)
        .subscribe({
          next: (result) => this.teams = result,
        });
    this.api.getPlayerPool(this.seasonID)
        .subscribe({
          next: (result) => this.playerPool = result,
        });
  }
}

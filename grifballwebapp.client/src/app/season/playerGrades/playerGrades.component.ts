import { CommonModule, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { GradesDto, Letter, LetterDto, PerMinuteDto } from './gradesDto';
import { MatTableModule } from '@angular/material/table';

@Component({
  selector: 'app-player-grades',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
  ],
  providers: [DecimalPipe],
  templateUrl: './playerGrades.component.html',
  styleUrl: './playerGrades.component.scss'
})
export class PlayerGradesComponent implements OnInit {
  private seasonID: number = 0;

  pm: PerMinuteDto[] = [];
  letters: LetterDto[] = [];

  public pmDisplayedColumns: string[] = ['gamertag', 'goals', 'kdSpread', 'punches', 'sprees', 'doubleKills', 'tripleKills', 'multiKills', 'xFactor', 'kills'];

  pmColumns = [
    {
      columnDef: 'gamertag',
      header: 'Gamertag',
      cell: (element: PerMinuteDto) => `${element.gamertag}`,
    },
    {
      columnDef: 'goals',
      header: 'Goals',
      cell: (element: PerMinuteDto) => `${element.goalsPM}`,
    },
    {
      columnDef: 'kdSpread',
      header: 'KD Spread',
      cell: (element: PerMinuteDto) => `${element.kdSpreadPM}`,
    },
    {
      columnDef: 'punches',
      header: 'Punches',
      cell: (element: PerMinuteDto) => `${element.punchesPM}`,
    },
    {
      columnDef: 'sprees',
      header: 'Sprees',
      cell: (element: PerMinuteDto) => `${element.spreesPM}`,
    },
    {
      columnDef: 'doubleKills',
      header: 'Double Kills',
      cell: (element: PerMinuteDto) => `${element.doubleKillsPM}`,
    },
    {
      columnDef: 'tripleKills',
      header: 'Triple Kills',
      cell: (element: PerMinuteDto) => `${element.tripleKillsPM}`,
    },
    {
      columnDef: 'multiKills',
      header: 'MultiKills',
      cell: (element: PerMinuteDto) => `${element.multiKillsPM}`,
    },
    {
      columnDef: 'xFactor',
      header: 'X Factor',
      cell: (element: PerMinuteDto) => `${element.xFactorPM}`,
    },
    {
      columnDef: 'kills',
      header: 'Kills',
      cell: (element: PerMinuteDto) => `${element.killsPM}`,
    },
  ];

  test: string = 'black';

  public letterDisplayedColumns: string[] = ['gamertag', 'goals', 'kdSpread', 'punches', 'sprees', 'doubleKills', 'tripleKills', 'multiKills', 'xFactor', 'kills', 'gradeAvgMath', 'gradeAvg'];

  letterColumns = [
    {
      columnDef: 'gamertag',
      header: 'Gamertag',
      cell: (element: LetterDto) => `${element.gamertag}`,
      style: (element: LetterDto) => '',
      color: '',
    },
    {
      columnDef: 'goals',
      header: 'Goals',
      cell: (element: LetterDto) => `${element.goals}`,
      style: (element: LetterDto) => this.getColor(element.goals),
      color: 'Black',
    },
    {
      columnDef: 'kdSpread',
      header: 'KD Spread',
      cell: (element: LetterDto) => `${element.kdSpread}`,
      style: (element: LetterDto) => this.getColor(element.kdSpread),
      color: 'Black',
    },
    {
      columnDef: 'punches',
      header: 'Punches',
      cell: (element: LetterDto) => `${element.punches}`,
      style: (element: LetterDto) => this.getColor(element.punches),
      color: 'Black',
    },
    {
      columnDef: 'sprees',
      header: 'Sprees',
      cell: (element: LetterDto) => `${element.sprees}`,
      style: (element: LetterDto) => this.getColor(element.sprees),
      color: 'Black',
    },
    {
      columnDef: 'doubleKills',
      header: 'Double Kills',
      cell: (element: LetterDto) => `${element.doubleKills}`,
      style: (element: LetterDto) => this.getColor(element.doubleKills),
      color: 'Black',
    },
    {
      columnDef: 'tripleKills',
      header: 'Triple Kills',
      cell: (element: LetterDto) => `${element.tripleKills}`,
      style: (element: LetterDto) => this.getColor(element.tripleKills),
      color: 'Black',
    },
    {
      columnDef: 'multiKills',
      header: 'MultiKills',
      cell: (element: LetterDto) => `${element.multiKills}`,
      style: (element: LetterDto) => this.getColor(element.multiKills),
      color: 'Black',
    },
    {
      columnDef: 'xFactor',
      header: 'X Factor',
      cell: (element: LetterDto) => `${element.xFactor}`,
      style: (element: LetterDto) => this.getColor(element.xFactor),
      color: 'Black',
    },
    {
      columnDef: 'kills',
      header: 'Kills',
      cell: (element: LetterDto) => `${element.kills}`,
      style: (element: LetterDto) => this.getColor(element.kills),
      color: 'Black',
    },
    {
      columnDef: 'gradeAvgMath',
      header: 'Grade Avg Math',
      cell: (element: LetterDto) => this.pipe.transform(element.gradeAvgMath, '1.2-2'),
      style: (element: LetterDto) => this.getColor(element.gradeAvg),
      color: 'Black',
    },
    {
      columnDef: 'gradeAvg',
      header: 'Grade Avg',
      cell: (element: LetterDto) => `${element.gradeAvg}`,
      style: (element: LetterDto) => this.getColor(element.gradeAvg),
      color: 'Black',
    },
  ];

  constructor(private route: ActivatedRoute, private http: HttpClient, private pipe: DecimalPipe) {
  }

  ngOnInit(): void {
    this.seasonID = Number(this.route.snapshot.paramMap.get('seasonID'));
    this.getGrades();
  }

  getGrades(): void {
    this.http.get<GradesDto>('/api/Grades/GetGrades/' + this.seasonID)
      .subscribe({
        next: result => {
          this.pm = result.perMinutes;
          this.letters = result.letters;
        },
      });
  }

  getColor(letter: Letter): string {
    switch (letter) {
      case "S+":
        return '#FFD700'
      case "S":
        return '#FFD700'
      case "S-":
        return '#FFD700'
      case "A+":
        return '#C9DAF8'
      case "A":
        return '#C9DAF8'
      case "A-":
        return '#C9DAF8'
      case "B+":
        return '#D9D2E9'
      case "B":
        return '#D9D2E9'
      case "B-":
        return '#D9D2E9'
      case "C+":
        return '#B6D7A8'
      case "C":
        return '#B6D7A8'
      case "C-":
        return '#B6D7A8'
      case "D+":
        return '#6FA8DC'
      case "D":
        return '#6FA8DC'
      case "D-":
        return '#6FA8DC'
      case "E+":
        return '#C27BA0'
      case "E":
        return '#C27BA0'
      case "E-":
        return '#C27BA0'
      case "F+":
        return '#A61C00'
      case "F":
        return '#A61C00'
      case "F-":
        return '#A61C00'
      default:
        return ''
    }
  }
}

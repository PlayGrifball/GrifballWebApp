import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
    selector: 'app-not-found',
    imports: [
        CommonModule,
    ],
    templateUrl: './notFound.component.html',
    styleUrl: './notFound.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class NotFoundComponent { }

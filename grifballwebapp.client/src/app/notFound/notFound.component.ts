import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
    selector: 'app-not-found',
    imports: [],
    templateUrl: './notFound.component.html',
    styleUrl: './notFound.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class NotFoundComponent { }

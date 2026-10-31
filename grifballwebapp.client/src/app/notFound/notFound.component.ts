import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
    selector: 'app-not-found',
    imports: [RouterLink],
    templateUrl: './notFound.component.html',
    styleUrl: './notFound.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class NotFoundComponent { }

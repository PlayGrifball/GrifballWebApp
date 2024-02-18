import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { TopStatsComponent } from './top-stats/top-stats.component';
import { LoginComponent } from './login/login.component';

const routes: Routes = [
  {
    path: '',
    component: TopStatsComponent,
    title: 'Top Stats'
  },
  {
    path: 'login',
    component: LoginComponent,
    title: 'Login'
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }

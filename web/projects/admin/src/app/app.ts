import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { DialogHost } from '../../../customer/src/app/core/dialog';

@Component({
  selector: 'ot-root',
  imports: [RouterOutlet, DialogHost],
  template: '<router-outlet /><ot-dialog-host />',
})
export class App {}

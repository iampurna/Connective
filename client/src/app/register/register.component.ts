import { Component, inject } from '@angular/core';
import { AuthserviceService } from '../services/authservice.service';

@Component({
  selector: 'app-register',
  imports: [],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css',
})
export class RegisterComponent {
  email!: string;
  password!: string;
  FullName!: string;
  profilePicture!: string;
  profileImage: File | null = null;

  authService = inject(AuthserviceService);
}

import { TestBed } from '@angular/core/testing';
import { PasswordInput } from './password-input';

describe('PasswordInput', () => {
  it('shows and hides the password with the eye button', async () => {
    const fixture = TestBed.createComponent(PasswordInput);
    fixture.componentRef.setInput('inputId', 'password');
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    const input = element.querySelector('input')!;
    const button = element.querySelector('button')!;

    expect(input.type).toBe('password');
    expect(button.getAttribute('aria-label')).toBe('Show password');

    button.click();
    await fixture.whenStable();
    expect(input.type).toBe('text');
    expect(button.getAttribute('aria-label')).toBe('Hide password');
  });
});

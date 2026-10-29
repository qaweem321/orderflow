const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });

// One fixed time zone (UTC) and an unambiguous month name: "Oct 29, 2026, 09:14 UTC".
const dateTime = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone: 'UTC',
});

export function formatMoney(cents: number): string {
  return money.format(cents / 100);
}

export function formatDateTime(iso: string): string {
  return `${dateTime.format(new Date(iso))} UTC`;
}

/** Only same-site paths are allowed as a post-login target, so ?next= can't send users to another site. */
export function safeNextPath(next: string | null | undefined): string {
  return next && next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\') ? next : '/';
}

/** The message from an API problem response, or a generic one. */
export function errorMessage(error: unknown): string {
  const body = (error as { error?: { title?: string; errors?: Record<string, string[]> } })?.error;
  const firstFieldError = body?.errors ? Object.values(body.errors).flat()[0] : undefined;
  return firstFieldError ?? body?.title ?? 'Something went wrong. Please try again.';
}

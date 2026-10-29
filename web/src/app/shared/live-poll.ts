import { DestroyRef } from '@angular/core';

export interface LivePoll {
  /** Load now and restart the timer (e.g. after the page switched to another record). */
  refresh(): void;
}

/**
 * Loads now, then again every 1.5 s while busy() is true (every 30 s otherwise), pausing in hidden
 * tabs and refreshing as soon as the tab is visible again.
 */
export function livePoll(destroyRef: DestroyRef, load: () => Promise<void>, busy: () => boolean): LivePoll {
  let timer: ReturnType<typeof setTimeout> | undefined;
  let stopped = false;
  let run = 0;

  const schedule = (myRun: number) => {
    clearTimeout(timer);
    if (!stopped && !document.hidden && myRun === run) {
      timer = setTimeout(() => void tick(), busy() ? 1500 : 30_000);
    }
  };
  // Each tick belongs to a run; a refresh starts a new run so an older in-flight load can't reschedule.
  const tick = async () => {
    const myRun = ++run;
    await load();
    schedule(myRun);
  };
  const onVisibility = () => (document.hidden ? clearTimeout(timer) : void tick());

  document.addEventListener('visibilitychange', onVisibility);
  destroyRef.onDestroy(() => {
    stopped = true;
    clearTimeout(timer);
    document.removeEventListener('visibilitychange', onVisibility);
  });
  void tick();
  return { refresh: () => void tick() };
}

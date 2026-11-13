/** OnDemand: started from its page with an input (a season match's pull), never on a schedule. */
export type JobState = 'Scheduled' | 'Paused' | 'Off' | 'OnDemand' | 'Error' | 'Blocked';

/** A job: its group and name, each a path segment of its resource. */
export interface JobKey {
  group: string;
  name: string;
}

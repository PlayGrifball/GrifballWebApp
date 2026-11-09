/**
 * `text` (a request's URL, say) as it may go into a log line: its line breaks as spaces, so whatever
 * a request carries can't start a line of its own and pass for another entry.
 */
export function forLog(text: string): string {
  return text.replaceAll(/[\r\n]/g, ' ');
}

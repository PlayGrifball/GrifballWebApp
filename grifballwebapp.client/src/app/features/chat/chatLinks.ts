/** A run of a chat reply: plain text, or the site's page it names (a path the router opens). */
export interface ChatPart {
  text: string;
  link?: string;
}

const id = '\\d{1,9}';
const guid = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}';

/**
 * The site's pages a reply may link to: the ones `get_site_link` gives (UrlService), and no other path, so words the model
 * wrote (or text from elsewhere that it repeated) never become a link to anywhere else on the site.
 */
const pages = [
  'seasons', 'leaderboards', 'late-league', `late-league/${id}`, `season/${id}`, `season/${id}/signups`, `season/${id}/draft`,
  `season/${id}/playergrades`, `season/${id}/team/${id}`, `history/team/${id}`, `seasonmatch/${id}`, `match/${guid}`, `profile/${id}`, `xbox/\\d{1,20}`,
];

// Not part of a longer word, address or path: nothing before it but a space or opening mark, nothing after it that continues it
const sitePath = new RegExp(`(?<![\\w/:.-])/(?:${pages.join('|')})(?![\\w/-])`, 'gi');

/** <paramref name="text"/> split into plain text and the site's pages it names, in order. */
export function chatParts(text: string): ChatPart[] {
  const parts: ChatPart[] = [];
  let from = 0;
  for (const match of text.matchAll(sitePath)) {
    if (match.index > from) parts.push({ text: text.slice(from, match.index) });
    parts.push({ text: match[0], link: match[0] });
    from = match.index + match[0].length;
  }
  if (from < text.length) parts.push({ text: text.slice(from) });
  return parts;
}

/** A run of a chat reply: plain text, the site's page it names (a path the router opens), or a player's picture the site keeps (its address). */
export interface ChatPart {
  text: string;
  link?: string;
  image?: string;
}

const id = '\\d{1,9}';
const guid = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}';

/**
 * The site's pages a reply may link to: the ones `get_site_link` gives (UrlService), and no other path, so words the model
 * wrote (or text from elsewhere that it repeated) never become a link to anywhere else on the site.
 */
const pages = [
  'seasons', 'leaderboards', 'late-league', `late-league/${id}`, `season/${id}`, `season/${id}/signups`, `season/${id}/draft`,
  `season/${id}/playergrades`, `season/${id}/team/${id}`, `history/team/${id}`, `seasonmatch/${id}`, `match/${guid}`, `profile/${id}`, `xbox/\\d{1,20}`,
];

// The pictures `get_player_image` gives (the API's own endpoints), and no other address, so a reply never loads an image from elsewhere
const pictures = 'api/users/\\d{1,9}/(?:spartan-headshot|halo-emblem|gamer-picture)';

// Not part of a longer word, address or path: nothing before it but a space or opening mark, nothing after it that continues it
const sitePath = new RegExp(`(?<![\\w/:.-])/(?:${pictures}|${pages.join('|')})(?![\\w/-])`, 'g');

/** <paramref name="text"/> split into plain text, the site's pages it names and the pictures it shows, in order. */
export function chatParts(text: string): ChatPart[] {
  const parts: ChatPart[] = [];
  let from = 0;
  for (const match of text.matchAll(sitePath)) {
    if (match.index > from) parts.push({ text: text.slice(from, match.index) });
    parts.push(match[0].startsWith('/api/') ? { text: match[0], image: match[0] } : { text: match[0], link: match[0] });
    from = match.index + match[0].length;
  }
  if (from < text.length) parts.push({ text: text.slice(from) });
  return parts;
}

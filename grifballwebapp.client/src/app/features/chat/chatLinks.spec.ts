import { chatParts } from './chatLinks';

describe('chatParts', () => {
  const game = '11111111-2222-3333-4444-555555555555';

  it('leaves text with no site path as one plain part', () => {
    expect(chatParts('Flipyap no know.')).toEqual([{ text: 'Flipyap no know.' }]);
    expect(chatParts('')).toEqual([]);
  });

  it('turns each of the site\'s pages into a link, keeping the words around it', () => {
    expect(chatParts('See /season/12, friend!')).toEqual([{ text: 'See ' }, { text: '/season/12', link: '/season/12' }, { text: ', friend!' }]);
    for (const path of ['/seasons', '/leaderboards', '/late-league', '/late-league/4', '/season/12/signups', '/season/12/draft',
      '/season/12/playergrades', '/season/12/team/5', '/history/team/5', '/seasonmatch/8', `/match/${game}`, '/profile/3', '/xbox/2533274790000001']) {
      expect(chatParts(`go ${path}.`)).toEqual([{ text: 'go ' }, { text: path, link: path }, { text: '.' }]);
    }
  });

  it('links several, and the same one twice', () => {
    expect(chatParts('/seasons or /leaderboards or /seasons').filter(p => p.link).map(p => p.link)).toEqual(['/seasons', '/leaderboards', '/seasons']);
  });

  it('shows each of a player\'s pictures the site keeps as an image, and no other address', () => {
    for (const kind of ['spartan-headshot', 'halo-emblem', 'gamer-picture']) {
      const path = `/api/users/12/${kind}`;
      const alt = `Player ${kind.replace('-', ' ')}`;
      expect(chatParts(`See ${path}`)).toEqual([{ text: 'See ' }, { text: path, image: path, alt }]);
    }
    for (const text of ['/api/users/12/profile-picture', '/api/users/x/halo-emblem', 'https://evil.test/api/users/12/halo-emblem', '/api/seasons', '/api/users/12/halo-emblem/more']) {
      expect(chatParts(text).some(p => p.image)).withContext(text).toBeFalse();
    }
  });

  it('does not link a path the site has no page for, part of a longer word or address, or the end of a longer path', () => {
    for (const text of ['/admin/12', '/sysadmin/jobs', 'https://grif.test/season/12', 'x/season/12', '/season/12/secret', '/season/abc', '/seasonsfoo', '/profile/3/edit', '/season/12/team', '/Season/12', '/Leaderboards']) {
      expect(chatParts(text).some(p => p.link)).withContext(text).toBeFalse();
    }
  });
});

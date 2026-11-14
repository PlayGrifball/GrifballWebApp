import type { SeasonMatchPageDto as ApiSeasonMatchPageDto } from '@api/models';
import { parseSeasonMatchPage } from './seasonMatchPageDto';

// What the server's DateTimeJsonConverter writes: UTC with a Z.
const UTC = '2026-01-02T03:04:00Z';

describe('parseSeasonMatchPage', () => {
  it('parseSeasonMatchPage converts its time', () => {
    expect(parseSeasonMatchPage({ seasonID: 1, scheduledTime: UTC } as ApiSeasonMatchPageDto)!.scheduledTime!.toMillis()).toBe(Date.parse(UTC));
    expect(parseSeasonMatchPage({ seasonID: 1, scheduledTime: null } as ApiSeasonMatchPageDto)!.scheduledTime).toBeNull();
  });

  it('keeps a null body (204: no such match) null', () => {
    expect(parseSeasonMatchPage(null)).toBeNull();
  });
});

import type { DeploymentFinish } from '@api/models';
import { deploymentFinishLabel } from './deploymentFinish';

describe('deploymentFinishLabel', () => {
  it('names each finish, a champion with every tier it won', () => {
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: ['Pro'] })).toBe('Pro Champion');
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: ['Open'] })).toBe('Open Champion');
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: ['Amateur', 'Pro'] })).toBe('Amateur & Pro Champion');
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: [] })).toBe('Champion');
    expect(deploymentFinishLabel({ finish: 'Champion' } as Parameters<typeof deploymentFinishLabel>[0])).toBe('Champion');
    expect(deploymentFinishLabel({ finish: 'Playoffs', championshipTiers: [] })).toBe('Playoffs');
    expect(deploymentFinishLabel({ finish: 'Eliminated', championshipTiers: [] })).toBe('Eliminated');
    expect(deploymentFinishLabel({ finish: 'RegularSeason', championshipTiers: [] })).toBe('Regular season');
    expect(deploymentFinishLabel({ finish: 'InProgress', championshipTiers: [] })).toBe('In progress');
  });

  it('shows a finish it does not know by its name', () => {
    expect(deploymentFinishLabel({ finish: 'Relegated' as DeploymentFinish, championshipTiers: [] })).toBe('Relegated');
  });
});
